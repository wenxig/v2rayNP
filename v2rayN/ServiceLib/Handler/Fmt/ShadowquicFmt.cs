namespace ServiceLib.Handler.Fmt;

public static class ShadowquicFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        var uri = Utils.TryUri(str);
        if (!ShareLinkValidation.IsEscapedCorrectly(str) || uri == null || !uri.Scheme.Equals("shadowquic", StringComparison.OrdinalIgnoreCase)) return null;
        var userInfo = uri.UserInfo.Split(':', 2).Select(Uri.UnescapeDataString).ToArray();
        if (userInfo.Length != 2 || userInfo.Any(string.IsNullOrEmpty) || uri.Port <= 0 || uri.IdnHost.IsNullOrEmpty()) return null;
        var item = new ProfileItem { ConfigType = EConfigType.ShadowQUIC, Address = uri.IdnHost.Trim('[', ']'), Port = uri.Port,
            Remarks = uri.GetComponents(UriComponents.Fragment, UriFormat.Unescaped) };
        var q = Utils.ParseQueryString(uri.Query);
        item.Sni = q["sni"] ?? string.Empty; item.Alpn = q["alpn"] ?? string.Empty;
        item.AllowInsecure = ShareLinkValidation.Boolean(q["insecure"]) == true ? Global.StringTrue : Global.StringFalse;
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            ShadowquicMinMtu = ParseInt(q["min-mtu"]), ShadowquicKeepAliveInterval = q["keep-alive-interval"] is string keepAlive ? int.Parse(keepAlive) : null,
            ShadowquicGso = ShareLinkValidation.Boolean(q["gso"]), ShadowquicBlackholeDetection = ShareLinkValidation.Boolean(q["blackhole-detection"]),
            ShadowquicUsername = userInfo[0], ShadowquicPassword = userInfo[1], ShadowquicQuicVersions = q["quic-versions"], ShadowquicInitialMtu = ParseInt(q["initial-mtu"]),
            ShadowquicZeroRtt = ShareLinkValidation.Boolean(q["zero-rtt"]), ShadowquicUdpOverStream = ShareLinkValidation.Boolean(q["udp-over-stream"]),
            ShadowquicCongestionControl = q["congestion-control"], ShadowquicUpMbps = ParseInt(q["up"]), ShadowquicDownMbps = ParseInt(q["down"]),
            ShadowquicCwnd = ParseInt(q["cwnd"]), ShadowquicMaxDatagramFrameSize = ParseInt(q["max-datagram-frame-size"]),
            ShadowquicMaxOpenStreams = ParseInt(q["max-open-streams"]), ShadowquicRecvWindowConn = ParseInt(q["recv-window-conn"]),
            ShadowquicRecvWindow = ParseInt(q["recv-window"]), ShadowquicDisableMtuDiscovery = ShareLinkValidation.Boolean(q["disable-mtu-discovery"])
        });
        if (!NodeValidator.Validate(item, ECoreType.shadowquic).Success) return null;
        msg = string.Empty; return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null) return null;
        var e = item.GetProtocolExtra(); var q = new List<string>();
        Add(q, "sni", item.Sni); Add(q, "alpn", item.Alpn); if (item.GetAllowInsecure()) q.Add("insecure=1");
        AddInt(q, "initial-mtu", e.ShadowquicInitialMtu);
        AddInt(q, "min-mtu", e.ShadowquicMinMtu);
        if (e.ShadowquicKeepAliveInterval.HasValue) q.Add($"keep-alive-interval={e.ShadowquicKeepAliveInterval.Value}");
        AddBool(q, "gso", e.ShadowquicGso); AddBool(q, "blackhole-detection", e.ShadowquicBlackholeDetection);
        Add(q, "quic-versions", e.ShadowquicQuicVersions); AddBool(q, "zero-rtt", e.ShadowquicZeroRtt); AddBool(q, "udp-over-stream", e.ShadowquicUdpOverStream);
        Add(q, "congestion-control", e.ShadowquicCongestionControl); AddInt(q, "up", e.ShadowquicUpMbps); AddInt(q, "down", e.ShadowquicDownMbps); AddInt(q, "cwnd", e.ShadowquicCwnd);
        AddInt(q, "max-datagram-frame-size", e.ShadowquicMaxDatagramFrameSize); AddInt(q, "max-open-streams", e.ShadowquicMaxOpenStreams); AddInt(q, "recv-window-conn", e.ShadowquicRecvWindowConn); AddInt(q, "recv-window", e.ShadowquicRecvWindow);
        AddBool(q, "disable-mtu-discovery", e.ShadowquicDisableMtuDiscovery);
        var query = q.Count == 0 ? string.Empty : "?" + string.Join('&', q); var remark = item.Remarks.IsNullOrEmpty() ? string.Empty : "#" + Utils.UrlEncode(item.Remarks);
        return $"shadowquic://{Utils.UrlEncode(e.ShadowquicUsername ?? item.Username)}:{Utils.UrlEncode(e.ShadowquicPassword ?? item.Password)}@{(Utils.IsIpv6(item.Address) ? "[" + item.Address + "]" : item.Address)}:{item.Port}{query}{remark}";
    }

    private static void AddBool(List<string> q, string name, bool? value) { if (value.HasValue) q.Add($"{name}={(value.Value ? 1 : 0)}"); }
    private static int? ParseInt(string? s) => ShareLinkValidation.PositiveInt(s);
    private static void Add(List<string> q, string name, string? value) { if (value.IsNotEmpty()) q.Add($"{name}={Utils.UrlEncode(value)}"); }
    private static void AddInt(List<string> q, string name, int? value) { if (value is > 0) q.Add($"{name}={value}"); }
}
