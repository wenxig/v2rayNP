namespace ServiceLib.Handler.Fmt;

public static class JuicityFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        var uri = Utils.TryUri(str);
        if (!ShareLinkValidation.IsEscapedCorrectly(str) || uri == null || !uri.Scheme.Equals("juicity", StringComparison.OrdinalIgnoreCase)) return null;
        var userInfo = uri.UserInfo.Split(':', 2).Select(Uri.UnescapeDataString).ToArray();
        if (userInfo.Length != 2 || userInfo.Any(string.IsNullOrEmpty) || uri.Port <= 0 || uri.IdnHost.IsNullOrEmpty() || !Guid.TryParse(userInfo[0], out _)) return null;
        var q = Utils.ParseQueryString(uri.Query);
        var item = new ProfileItem { ConfigType = EConfigType.Juicity, Address = uri.IdnHost.Trim('[', ']'), Port = uri.Port,
            Remarks = uri.GetComponents(UriComponents.Fragment, UriFormat.Unescaped), Sni = q["sni"] ?? string.Empty,
            AllowInsecure = ShareLinkValidation.Boolean(q["allow_insecure"]) == true ? Global.StringTrue : Global.StringFalse };
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            JuicityUuid = userInfo[0], JuicityPassword = userInfo[1], JuicitySni = item.Sni, JuicityAllowInsecure = item.GetAllowInsecure(), JuicityCongestionControl = q["congestion_control"], JuicityPinnedCertchainSha256 = q["pinned_certchain_sha256"]
        });
        if (!NodeValidator.Validate(item, ECoreType.juicity).Success) return null;
        msg = string.Empty; return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null) return null;
        var e = item.GetProtocolExtra(); var uuid = e.JuicityUuid ?? item.Username; var password = e.JuicityPassword ?? item.Password; var q = new List<string>();
        Add(q, "congestion_control", e.JuicityCongestionControl); Add(q, "sni", e.JuicitySni ?? item.Sni); if (e.JuicityAllowInsecure ?? item.GetAllowInsecure()) q.Add("allow_insecure=1"); Add(q, "pinned_certchain_sha256", e.JuicityPinnedCertchainSha256);
        var query = q.Count == 0 ? string.Empty : "?" + string.Join('&', q); var remark = item.Remarks.IsNullOrEmpty() ? string.Empty : "#" + Utils.UrlEncode(item.Remarks);
        return $"juicity://{Utils.UrlEncode(uuid)}:{Utils.UrlEncode(password)}@{(Utils.IsIpv6(item.Address) ? "[" + item.Address + "]" : item.Address)}:{item.Port}{query}{remark}";
    }

    private static void Add(List<string> q, string name, string? value) { if (value.IsNotEmpty()) q.Add($"{name}={Utils.UrlEncode(value)}"); }
}
