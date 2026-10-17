namespace ServiceLib.Handler.Fmt;

public static class SsrFmt
{
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=')));

    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        try
        {
            var decoded = Decode(str[6..].TrimEnd('/'));
            var parts = decoded.Split("/?", 2, StringSplitOptions.None);
            // Parse from the right to preserve IPv6 addresses.
            var fields = parts[0].Split(':');
            if (fields.Length < 6) return null;
            var n = fields.Length;
            var address = string.Join(":", fields.Take(n - 5)).Trim('[', ']');
            if (address.IsNullOrEmpty() || !int.TryParse(fields[n - 5], out var port) || port is < 1 or > 65535) return null;
            var password = Decode(fields[n - 1]);
            if (password.IsNullOrEmpty()) return null;
            var q = Utils.ParseQueryString(parts.Length == 2 ? parts[1] : string.Empty);
            var item = new ProfileItem
            {
                ConfigType = EConfigType.SSR, Address = address, Port = port, Password = password,
                Remarks = q["remarks"] is { } remarks ? Decode(remarks) : string.Empty
            };
            item.SetProtocolExtra(new ProtocolExtraItem
            {
                SsrProtocol = fields[n - 4], SsrCipher = fields[n - 3], SsrObfs = fields[n - 2],
                SsrObfsParam = q["obfsparam"] is { } obfs ? Decode(obfs) : null,
                SsrProtocolParam = q["protoparam"] is { } protocol ? Decode(protocol) : null,
                SsrUdp = q["udp"] switch { null => null, "1" => true, "0" => false, _ => throw new FormatException() }
            });
            if (!NodeValidator.Validate(item, ECoreType.mihomo).Success) return null;
            msg = string.Empty;
            return item;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return null; }
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null) return null;
        var e = item.GetProtocolExtra();
        var q = new List<string>();
        if (item.Remarks.IsNotEmpty()) q.Add($"remarks={Encode(item.Remarks)}");
        if (e.SsrObfsParam.IsNotEmpty()) q.Add($"obfsparam={Encode(e.SsrObfsParam)}");
        if (e.SsrProtocolParam.IsNotEmpty()) q.Add($"protoparam={Encode(e.SsrProtocolParam)}");
        if (e.SsrUdp.HasValue) q.Add($"udp={(e.SsrUdp.Value ? 1 : 0)}");
        var body = $"{item.Address}:{item.Port}:{e.SsrProtocol ?? "origin"}:{e.SsrCipher ?? "chacha20-ietf"}:{e.SsrObfs ?? "plain"}:{Encode(item.Password)}/?{string.Join('&', q)}";
        return $"ssr://{Encode(body)}";
    }
}
