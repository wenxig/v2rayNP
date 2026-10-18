namespace ServiceLib.Services;

public interface ICoreProcess : IDisposable
{
    bool HasExited { get; }
    Task StopAsync();
    Task WaitForExitAsync(CancellationToken cancellationToken);
}

public record CoreLaunchStep(ECoreType CoreType, string Stage, int? SocksPort, Func<CancellationToken, Task<ICoreProcess>> Start, bool OptionalAfterReady = false);
public sealed record CoreLaunchPlan(IReadOnlyList<CoreLaunchStep> Steps, Func<Task> Cleanup);

public sealed class CoreLaunchException(CoreStartResult result) : Exception
{
    public CoreStartResult Result { get; } = result;
}

/// <summary>Owns only the supplied processes. State and proxy transitions share the lifecycle lock.</summary>
public sealed class CoreLifecycleService(Func<ECoreType?, CoreStartResult?, Task> stateChanged)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _requestLock = new();
    private CancellationTokenSource? _pending;
    private CancellationTokenSource? _monitor;
    private readonly List<ICoreProcess> _processes = [];
    private readonly HashSet<ICoreProcess> _optional = [];
    private CoreLaunchPlan? _plan;
    public bool IsRunning { get; private set; }

    public async Task<CoreStartResult> LoadAsync(Func<CancellationToken, Task<CoreLaunchPlan>> prepare, CancellationToken cancellationToken = default)
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_requestLock)
        {
            _pending?.Cancel();
            _pending = request;
        }
        var entered = false;
        var replaced = false;
        CoreLaunchPlan? prepared = null;
        var stage = "Configuration";
        ECoreType? core = null;
        try
        {
            await _gate.WaitAsync(request.Token);
            entered = true;
            prepared = await prepare(request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (prepared.Steps.Count == 0) throw new CoreLaunchException(new(CoreStartFailure.Configuration, stage));
            await StopOwnedAsync();
            replaced = true;
            _plan = prepared;
            foreach (var step in prepared.Steps)
            {
                stage = step.Stage;
                core = step.CoreType;
                request.Token.ThrowIfCancellationRequested();
                if (step.SocksPort is int port)
                {
                    // Diagnoses existing listeners. The child still owns bind and can lose the race.
                    var listener = new TcpListener(IPAddress.Loopback, port);
                    try { listener.Start(); }
                    catch (SocketException) { throw new CoreLaunchException(new(CoreStartFailure.PortInUse, stage, core)); }
                    finally { listener.Stop(); }
                }
                var process = await step.Start(request.Token);
                _processes.Add(process);
                if (step.OptionalAfterReady) _optional.Add(process);
                var failure = step.SocksPort is int socksPort
                    ? await SocksReadiness.WaitAsync(socksPort, () => process.HasExited, Global.LocalFetch, request.Token)
                    : process.HasExited ? CoreStartFailure.ProcessExited : CoreStartFailure.None;
                if (failure != CoreStartFailure.None) throw new CoreLaunchException(new(failure, stage, core));
                if (_processes.Any(p => p.HasExited)) throw new CoreLaunchException(new(CoreStartFailure.ProcessExited, stage, core));
            }
            request.Token.ThrowIfCancellationRequested();
            IsRunning = true;
            await stateChanged(core, null);
            _monitor = new CancellationTokenSource();
            _ = MonitorAsync(prepared, _processes.ToArray(), _monitor.Token);
            return new(CoreStartFailure.None, "Running", core);
        }
        catch (Exception ex)
        {
            var result = ex switch
            {
                CoreLaunchException launch => launch.Result,
                OperationCanceledException => new CoreStartResult(CoreStartFailure.Cancelled, stage, core),
                _ => new CoreStartResult(replaced ? CoreStartFailure.Launch : CoreStartFailure.Configuration, stage, core)
            };
            if (entered && replaced)
            {
                await StopOwnedAsync();
                await stateChanged(null, result);
            }
            else if (prepared != null) await prepared.Cleanup();
            return result with { PreviousChainPreserved = !replaced && IsRunning };
        }
        finally
        {
            lock (_requestLock)
            {
                if (ReferenceEquals(_pending, request)) _pending = null;
            }
            if (entered) _gate.Release();
        }
    }

    public void CancelPending()
    {
        lock (_requestLock) _pending?.Cancel();
    }

    public async Task StopAsync()
    {
        lock (_requestLock) _pending?.Cancel();
        await _gate.WaitAsync();
        try { await StopOwnedAsync(); }
        finally { _gate.Release(); }
    }

    public async Task<bool> WhenRunningAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            if (!IsRunning || _processes.Any(p => !_optional.Contains(p) && p.HasExited)) return false;
            await action();
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task StopOwnedAsync()
    {
        _monitor?.Cancel();
        _monitor?.Dispose();
        _monitor = null;
        var wasRunning = IsRunning;
        IsRunning = false;
        foreach (var process in _processes.AsEnumerable().Reverse())
        {
            try { await process.StopAsync(); }
            catch (Exception ex) { Logging.SaveLog($"Core cleanup: {ex.GetType().Name}"); }
            finally
            {
                try { process.Dispose(); }
                catch (Exception ex) { Logging.SaveLog($"Core dispose: {ex.GetType().Name}"); }
            }
        }
        _processes.Clear();
        _optional.Clear();
        if (_plan != null)
        {
            var plan = _plan;
            _plan = null;
            try { await plan.Cleanup(); }
            catch (Exception ex) { Logging.SaveLog($"Core config cleanup: {ex.GetType().Name}"); }
        }
        if (wasRunning) await stateChanged(null, null);
    }

    private async Task MonitorAsync(CoreLaunchPlan plan, ICoreProcess[] processes, CancellationToken token)
    {
        try
        {
            var exits = processes.Select(p => p.WaitForExitAsync(token)).ToArray();
            var pending = exits.ToList();
            while (pending.Count > 0)
            {
                var first = await Task.WhenAny(pending);
                await first;
                pending.Remove(first);
                await _gate.WaitAsync(token);
                try
                {
                    if (!ReferenceEquals(_plan, plan) || token.IsCancellationRequested) return;
                    var step = plan.Steps[Array.IndexOf(exits, first)];
                    var failure = new CoreStartResult(CoreStartFailure.ProcessExited, step.Stage, step.CoreType);
                    if (step.OptionalAfterReady)
                    {
                        await stateChanged(plan.Steps[^1].CoreType, failure);
                        continue;
                    }
                    await StopOwnedAsync();
                    await stateChanged(null, failure);
                    return;
                }
                finally { _gate.Release(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Logging.SaveLog($"Core monitor: {ex.GetType().Name}"); }
    }
}

public static class SocksReadiness
{
    public static async Task<CoreStartFailure> WaitAsync(int port, Func<bool> hasExited, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (hasExited()) return CoreStartFailure.ProcessExited;
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                attempt.CancelAfter(TimeSpan.FromMilliseconds(200));
                using var tcp = new TcpClient();
                try
                {
                    await tcp.ConnectAsync(Global.Loopback, port, attempt.Token);
                    var stream = tcp.GetStream();
                    await stream.WriteAsync(new byte[] { 5, 1, 0 }, attempt.Token);
                    var response = new byte[2];
                    await stream.ReadExactlyAsync(response, attempt.Token);
                    if (hasExited()) return CoreStartFailure.ProcessExited;
                    return response[0] == 5 && response[1] == 0 ? CoreStartFailure.None : CoreStartFailure.Handshake;
                }
                catch (EndOfStreamException) { return CoreStartFailure.Handshake; }
                catch (IOException) { return CoreStartFailure.Handshake; }
                catch (SocketException) { }
                catch (OperationCanceledException) when (!linked.IsCancellationRequested) { }
                await Task.Delay(25, linked.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested ? CoreStartFailure.Cancelled : CoreStartFailure.Timeout;
        }
    }
}
