namespace ServiceLib.Models.Dto;

public enum PingFailure { None, Configuration, Proxy, Dns, Connection, Tls, HttpStatus, Timeout, Cancelled }
public record PingSample(int Delay, PingFailure Failure, int? StatusCode = null);
public record PingProbeResult(IReadOnlyList<PingSample> Samples, string Target)
{
    public int Delay => Samples.Where(s => s.Delay > 0).Select(s => s.Delay).DefaultIfEmpty(-1).Min();
    public string Describe() => string.Format(ProtocolCapabilities.Text("MsgPingProbe"), Target,
        string.Join("; ", Samples.Select((sample, index) => $"{index + 1}: {ProtocolCapabilities.Text($"PingFailure{sample.Failure}")}" +
            (sample.Delay > 0 ? $" {sample.Delay} ms" : sample.StatusCode is int code ? $" {code}" : ""))));
}

public static class PingDiagnostics
{
    // Userinfo, path/query and fragments may all contain secrets. Exceptions and headers never enter diagnostics.
    public static string SafeTarget(string? target) => Uri.TryCreate(target, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" ? $"{uri.Scheme}://{uri.IdnHost}:{uri.Port}" : "[invalid target]";

    public static PingFailure Classify(HttpRequestException error) => error.HttpRequestError switch
    {
        HttpRequestError.NameResolutionError => PingFailure.Dns,
        HttpRequestError.SecureConnectionError => PingFailure.Tls,
        HttpRequestError.ProxyTunnelError => PingFailure.Proxy,
        HttpRequestError.ConnectionError => PingFailure.Connection,
        _ => PingFailure.Connection
    };
}
