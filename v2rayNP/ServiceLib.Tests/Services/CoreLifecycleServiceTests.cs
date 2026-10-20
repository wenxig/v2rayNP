namespace ServiceLib.Tests.Services;

public class CoreLifecycleServiceTests
{
    private sealed class FakeProcess(string name, List<string> stopped) : ICoreProcess
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HasExited => _exit.Task.IsCompleted;
        public void Exit() => _exit.TrySetResult();
        public Task WaitForExitAsync(CancellationToken cancellationToken) => _exit.Task.WaitAsync(cancellationToken);
        public Task StopAsync() { stopped.Add(name); Exit(); return Task.CompletedTask; }
        public void Dispose() { }
    }

    private static CoreLaunchStep Step(FakeProcess process, string stage = "Backend", int? port = null) =>
        new(ECoreType.shadowquic, stage, port, _ => Task.FromResult<ICoreProcess>(process));

    private static Task<CoreLaunchPlan> Plan(params CoreLaunchStep[] steps) => Task.FromResult(new CoreLaunchPlan(steps, () => Task.CompletedTask));

    [Test]
    public async Task ShutdownCancelsPendingAndRejectsAnyLaterLoad()
    {
        var preparing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new CoreLifecycleService((_, _) => Task.CompletedTask);
        var pending = lifecycle.LoadAsync(async token =>
        {
            preparing.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return await Plan();
        });
        await preparing.Task;
        await lifecycle.ShutdownAsync();
        await (await pending).Failure.Should().BeEqualTo(CoreStartFailure.Cancelled);
        var prepared = false;
        var result = await lifecycle.LoadAsync(_ => { prepared = true; return Plan(); });
        await result.Failure.Should().BeEqualTo(CoreStartFailure.Cancelled);
        await prepared.Should().BeFalse();
        await lifecycle.IsRunning.Should().BeFalse();
    }

    [Test]
    public async Task BackendExitDuringFrontendStartupRollsBackEvenWhenOptionalAfterReady()
    {
        var stopped = new List<string>();
        var backend = new FakeProcess("backend", stopped);
        var front = new FakeProcess("front", stopped);
        var lifecycle = new CoreLifecycleService((_, _) => Task.CompletedTask);
        var result = await lifecycle.LoadAsync(_ => Plan(
            Step(backend) with { OptionalAfterReady = true },
            new(ECoreType.sing_box, "Frontend", null, _ =>
            {
                backend.Exit();
                return Task.FromResult<ICoreProcess>(front);
            })));
        await result.Failure.Should().BeEqualTo(CoreStartFailure.ProcessExited);
        await lifecycle.IsRunning.Should().BeFalse();
        await stopped.SequenceEqual(new[] { "front", "backend" }).Should().BeTrue();
    }

    [Test]
    public async Task HundredGroupSwitchesCleanEveryOwnedProcessAndConfigExactlyOnce()
    {
        var stopped = new List<string>();
        var cleanup = 0;
        var lifecycle = new CoreLifecycleService((_, _) => Task.CompletedTask);
        try
        {
            for (var i = 0; i < 100; i++)
            {
                var one = new FakeProcess($"{i}-one", stopped);
                var two = new FakeProcess($"{i}-two", stopped);
                var front = new FakeProcess($"{i}-front", stopped);
                var result = await lifecycle.LoadAsync(_ => Task.FromResult(new CoreLaunchPlan(
                    [Step(one) with { OptionalAfterReady = true }, Step(two) with { OptionalAfterReady = true }, Step(front, "Frontend")],
                    () => { cleanup++; return Task.CompletedTask; })));
                await result.Success.Should().BeTrue();
            }
        }
        finally { await lifecycle.StopAsync(); }
        await cleanup.Should().BeEqualTo(100);
        await stopped.Count.Should().BeEqualTo(300);
        await stopped.Distinct().Count().Should().BeEqualTo(300);
        await stopped.Take(3).SequenceEqual(new[] { "0-front", "0-two", "0-one" }).Should().BeTrue();
    }

    [Test]
    public async Task OptionalBackendExitKeepsFrontendAndStopStillCleansWholeGroup()
    {
        var stopped = new List<string>();
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new CoreLifecycleService((_, failure) => { if (failure != null) notified.TrySetResult(); return Task.CompletedTask; });
        var one = new FakeProcess("one", stopped);
        var two = new FakeProcess("two", stopped);
        var front = new FakeProcess("front", stopped);
        try
        {
            var result = await lifecycle.LoadAsync(_ => Plan(Step(one) with { OptionalAfterReady = true }, Step(two) with { OptionalAfterReady = true }, Step(front, "Frontend")));
            await result.Success.Should().BeTrue();
            one.Exit();
            await notified.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await lifecycle.IsRunning.Should().BeTrue();
            await two.HasExited.Should().BeFalse();
            await front.HasExited.Should().BeFalse();
            await (await lifecycle.WhenRunningAsync(() => Task.CompletedTask)).Should().BeTrue();
        }
        finally { await lifecycle.StopAsync(); }
        await stopped.SequenceEqual(new[] { "front", "two", "one" }).Should().BeTrue();
    }

    [Test]
    public async Task InvalidPreparationPreservesRunningChainAndDoesNotPublishNewState()
    {
        var stopped = new List<string>();
        var states = new List<ECoreType?>();
        var lifecycle = new CoreLifecycleService((core, _) => { states.Add(core); return Task.CompletedTask; });
        var original = new FakeProcess("original", stopped);
        try
        {
            await (await lifecycle.LoadAsync(_ => Plan(Step(original)))).Success.Should().BeTrue();
            var result = await lifecycle.LoadAsync(_ => throw new CoreLaunchException(new(CoreStartFailure.Configuration, "Configuration")));
            await result.PreviousChainPreserved.Should().BeTrue();
            await original.HasExited.Should().BeFalse();
            await states.Count.Should().BeEqualTo(1);
        }
        finally { await lifecycle.StopAsync(); }
    }

    [Test]
    public async Task FrontendFailureRollsBackBackendAndNeverAppliesProxy()
    {
        var stopped = new List<string>();
        var lifecycle = new CoreLifecycleService((_, _) => Task.CompletedTask);
        var backend = new FakeProcess("backend", stopped);
        var result = await lifecycle.LoadAsync(_ => Plan(Step(backend), new(ECoreType.sing_box, "Frontend", null,
            _ => throw new CoreLaunchException(new(CoreStartFailure.Launch, "Frontend", ECoreType.sing_box)))));
        await result.Failure.Should().BeEqualTo(CoreStartFailure.Launch);
        await backend.HasExited.Should().BeTrue();
        var proxyApplied = false;
        await (await lifecycle.WhenRunningAsync(() => { proxyApplied = true; return Task.CompletedTask; })).Should().BeFalse();
        await proxyApplied.Should().BeFalse();
        await stopped.SequenceEqual(new[] { "backend" }).Should().BeTrue();
    }

    [Test]
    public async Task RequiredProcessExitInvalidatesStateAndStopsFrontendBeforeBackend()
    {
        var stopped = new List<string>();
        var failed = new TaskCompletionSource<CoreStartResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new CoreLifecycleService((_, result) => { if (result != null) failed.TrySetResult(result); return Task.CompletedTask; });
        var backend = new FakeProcess("backend", stopped);
        var frontend = new FakeProcess("frontend", stopped);
        try
        {
            await (await lifecycle.LoadAsync(_ => Plan(Step(backend), Step(frontend, "Frontend")))).Success.Should().BeTrue();
            backend.Exit();
            await (await failed.Task.WaitAsync(TimeSpan.FromSeconds(3))).Failure.Should().BeEqualTo(CoreStartFailure.ProcessExited);
            await lifecycle.IsRunning.Should().BeFalse();
            await stopped.SequenceEqual(new[] { "frontend", "backend" }).Should().BeTrue();
        }
        finally { await lifecycle.StopAsync(); }
    }

    [Test]
    public async Task NewLoadCancelsPendingLoadAndOnlyPublishesNewestChain()
    {
        var stopped = new List<string>();
        var preparing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new CoreLifecycleService((_, _) => Task.CompletedTask);
        var first = lifecycle.LoadAsync(async token =>
        {
            preparing.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return await Plan();
        });
        await preparing.Task;
        var current = new FakeProcess("current", stopped);
        try
        {
            await (await lifecycle.LoadAsync(_ => Plan(Step(current)))).Success.Should().BeTrue();
            await (await first).Failure.Should().BeEqualTo(CoreStartFailure.Cancelled);
            await lifecycle.IsRunning.Should().BeTrue();
            await current.HasExited.Should().BeFalse();
        }
        finally { await lifecycle.StopAsync(); }
    }

    [Test]
    public async Task OccupiedPortPreventsLaunchAndMissingCorePreservesState()
    {
        var lifecycle = new CoreLifecycleService((_, _) => Task.CompletedTask);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var launched = false;
        var step = new CoreLaunchStep(ECoreType.shadowquic, "Backend", ((IPEndPoint)listener.LocalEndpoint).Port, _ =>
        {
            launched = true;
            throw new InvalidOperationException();
        });
        await (await lifecycle.LoadAsync(_ => Plan(step))).Failure.Should().BeEqualTo(CoreStartFailure.PortInUse);
        await launched.Should().BeFalse();
        await (await lifecycle.LoadAsync(_ => throw new CoreLaunchException(new(CoreStartFailure.MissingCore, "Backend")))).Failure.Should().BeEqualTo(CoreStartFailure.MissingCore);
    }

    [Test]
    [Arguments(0, CoreStartFailure.None)]
    [Arguments(255, CoreStartFailure.Handshake)]
    [Arguments(2, CoreStartFailure.Handshake)]
    public async Task SocksProbeRequiresCompleteNoAuthReplyAndAcceptsFragmentedResponse(int method, CoreStartFailure expected)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync();
            var stream = peer.GetStream();
            await stream.ReadExactlyAsync(new byte[3]);
            await stream.WriteAsync(new byte[] { 5 });
            await Task.Delay(20);
            await stream.WriteAsync(new byte[] { (byte)method });
        });
        await (await SocksReadiness.WaitAsync(port, () => false, TimeSpan.FromSeconds(2))).Should().BeEqualTo(expected);
        await server;
    }

    [Test]
    public async Task SocksProbeDistinguishesTimeoutCancellationAndProcessExit()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await (await SocksReadiness.WaitAsync(port, () => false, TimeSpan.FromMilliseconds(80))).Should().BeEqualTo(CoreStartFailure.Timeout);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await (await SocksReadiness.WaitAsync(port, () => false, TimeSpan.FromSeconds(2), cancelled.Token)).Should().BeEqualTo(CoreStartFailure.Cancelled);
        await (await SocksReadiness.WaitAsync(port, () => true, TimeSpan.FromSeconds(2))).Should().BeEqualTo(CoreStartFailure.ProcessExited);
    }
}
