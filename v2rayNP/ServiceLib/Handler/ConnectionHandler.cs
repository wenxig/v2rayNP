namespace ServiceLib.Handler;

public static class ConnectionHandler
{
    private static readonly string _tag = "ConnectionHandler";

    /// <summary>
    /// Runs ping and IP checks.
    /// </summary>
    public static async Task<AvailabilityCheckResult> RunAvailabilityCheck()
    {
        var time = await GetRealPingTimeInfo();
        var ip = time > 0 ? await GetIPInfo() : Global.None;

        return new AvailabilityCheckResult(time, ip);
    }

    /// <summary>
    /// Gets IP information using the default local proxy.
    /// </summary>
    private static async Task<string?> GetIPInfo()
    {
        var webProxy = await GetWebProxy();

        var ipInfo = await GetIPInfo(webProxy);
        return ipInfo?.ToString() ?? Global.None;
    }

    /// <summary>
    /// Measures real ping time using configured test URL.
    /// </summary>
    private static async Task<int> GetRealPingTimeInfo()
    {
        var responseTime = -1;
        try
        {
            var webProxy = await GetWebProxy();

            for (var i = 0; i < 2; i++)
            {
                responseTime = await GetRealPingTime(webProxy);
                if (responseTime > 0)
                {
                    break;
                }
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return -1;
        }
        return responseTime;
    }

    /// <summary>
    /// Creates local SOCKS proxy instance.
    /// </summary>
    private static async Task<WebProxy?> GetWebProxy()
    {
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    /// <summary>
    /// Measures response time by sending HTTP requests through proxy.
    /// </summary>
    public static async Task<int> GetRealPingTime(IWebProxy? webProxy, CancellationToken cancellationToken = default)
        => (await ProbeAsync(webProxy, cancellationToken)).Delay;

    public static async Task<PingProbeResult> ProbeAsync(IWebProxy? webProxy, CancellationToken cancellationToken = default)
    {
        var url = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;
        var samples = new List<PingSample>();
        using var deadline = new CancellationTokenSource(Global.LocalFetch);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var client = new HttpClient(new SocketsHttpHandler
        {
            Proxy = webProxy,
            UseProxy = webProxy != null,
            ConnectTimeout = Global.LocalFetch,
        });
        for (var i = 0; i < 2; i++)
        {
            try
            {
                var timer = Stopwatch.StartNew();
                using var response = await client.GetAsync(url, linked.Token).ConfigureAwait(false);
                samples.Add(response.IsSuccessStatusCode
                    ? new PingSample(Math.Max(1, (int)timer.Elapsed.TotalMilliseconds), PingFailure.None)
                    : new PingSample(-1, PingFailure.HttpStatus, (int)response.StatusCode));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                samples.Add(new(-1, PingFailure.Timeout));
                break;
            }
            catch (HttpRequestException ex) { samples.Add(new(-1, PingDiagnostics.Classify(ex))); }
            catch (Exception) { samples.Add(new(-1, PingFailure.Configuration)); }
            if (i == 0)
            {
                try { await Task.Delay(100, linked.Token); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (OperationCanceledException) { samples.Add(new(-1, PingFailure.Timeout)); break; }
            }
        }
        var result = new PingProbeResult(samples, PingDiagnostics.SafeTarget(url));
        Logging.SaveLog(result.Describe());
        return result;
    }

    /// <summary>
    /// Gets IP and country information through specified proxy.
    /// </summary>
    public static async Task<IpInfoResult?> GetIPInfo(IWebProxy? webProxy, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = AppManager.Instance.Config.SpeedTestItem.IPAPIUrl;
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            var downloadHandle = new DownloadService();
            var result = await downloadHandle.TryDownloadString(url, webProxy, "", cancellationToken);
            if (result == null)
            {
                return null;
            }

            var ipInfo = JsonUtils.Deserialize<IPAPIInfo>(result);
            if (ipInfo == null)
            {
                return null;
            }

            var ip = ipInfo.ip ?? ipInfo.clientIp ?? ipInfo.ip_addr ?? ipInfo.query;
            var country = ipInfo.country_code ?? ipInfo.country ?? ipInfo.countryCode ?? ipInfo.location?.country_code ?? "unknown";

            return new IpInfoResult(country, ip);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
