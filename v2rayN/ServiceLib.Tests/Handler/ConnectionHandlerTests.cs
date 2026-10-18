using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Handler;

public class ConnectionHandlerTests
{
    [Test]
    [Arguments(200, 503, true)]
    [Arguments(503, 200, true)]
    [Arguments(503, 503, false)]
    [Arguments(204, 204, true)]
    [Arguments(200, -1, true)]
    [Arguments(-1, 200, true)]
    [Arguments(-1, -1, false)]
    public async Task RealPing_ShouldKeepSuccessfulSamplesAndRejectHttpErrors(int firstStatus, int secondStatus, bool success)
    {
        await using var server = new PingServer(firstStatus, secondStatus);
        var delay = await Measure(server);
        await (delay > 0).Should().BeEqualTo(success);
        await server.RequestCount.Should().BeEqualTo(2);
    }

    [Test]
    public async Task RealPing_ShouldKeepSuccessWhenSecondSampleTimesOut()
    {
        await using var server = new PingServer(200, 0);
        await (await Measure(server)).Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RealPing_ShouldPropagateCallerCancellationEvenAfterSuccess()
    {
        await using var server = new PingServer(200, 0);
        using var cts = new CancellationTokenSource();
        var pending = Measure(server, cts.Token);
        await server.SecondRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        var cancelled = false;
        try
        {
            await pending;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        await cancelled.Should().BeTrue();
    }

    private static async Task<int> Measure(PingServer server, CancellationToken ct = default)
    {
        var previous = AppManager.Instance.Config;
        var config = CoreConfigTestFactory.CreateConfig();
        config.SpeedTestItem.SpeedPingTestUrl = server.Url;
        CoreConfigTestFactory.BindAppManagerConfig(config);
        try
        {
            return await ConnectionHandler.GetRealPingTime(null, ct);
        }
        finally
        {
            CoreConfigTestFactory.BindAppManagerConfig(previous);
        }
    }

    private sealed class PingServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;
        private int _requestCount;

        public string Url { get; }
        public int RequestCount => Volatile.Read(ref _requestCount);
        public TaskCompletionSource SecondRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PingServer(int firstStatus, int secondStatus)
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/ping";
            _task = Serve(firstStatus, secondStatus);
        }

        private async Task Serve(int firstStatus, int secondStatus)
        {
            foreach (var status in new[] { firstStatus, secondStatus })
            {
                using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                while (await reader.ReadLineAsync(_cts.Token) is { Length: > 0 })
                {
                }
                if (Interlocked.Increment(ref _requestCount) == 2) SecondRequest.TrySetResult();
                if (status == 0)
                {
                    await Task.Delay(Timeout.Infinite, _cts.Token);
                }
                else if (status > 0)
                {
                    await Task.Delay(20, _cts.Token);
                    var response = $"HTTP/1.1 {status} Test\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _cts.Token);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            try
            {
                await _task;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}
