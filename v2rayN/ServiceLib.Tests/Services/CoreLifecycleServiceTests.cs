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
