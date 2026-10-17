namespace ServiceLib.Handler.Fmt;

// Field numbers follow upstream appctl/proto/clientcfg.proto and base.proto.
// ProfileItem represents one profile, server and transport binding.
public static class MieruFmt
{
    private static readonly string[] Multiplexing = ["MULTIPLEXING_DEFAULT", "MULTIPLEXING_OFF", "MULTIPLEXING_LOW", "MULTIPLEXING_MIDDLE", "MULTIPLEXING_HIGH"];
    private static readonly string[] Handshake = ["HANDSHAKE_DEFAULT", "HANDSHAKE_STANDARD", "HANDSHAKE_NO_WAIT"];

    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;
        try
        {
            var item = str.StartsWith("mierus://", StringComparison.OrdinalIgnoreCase) ? ResolveSimple(str) : ResolveBinary(Convert.FromBase64String(str[8..]));
            if (!NodeValidator.Validate(item, ECoreType.mihomo).Success) return null;
            msg = string.Empty;
            return item;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or OverflowException) { return null; }
    }

    private static ProfileItem ResolveSimple(string str)
    {
        var u = new Uri(str);
        var auth = u.UserInfo.Split(':', 2);
        if (auth.Length != 2 || !ShareLinkValidation.IsEscapedCorrectly(str)) throw new FormatException();
        var q = Utils.ParseQueryString(u.Query);
        var ports = q.GetValues("port");
        var protocols = q.GetValues("protocol");
        if (ports?.Length != 1 || protocols?.Length != 1 || q["profile"].IsNullOrEmpty() || q["mtu"] != null) throw new FormatException();
        var range = ports[0].Contains('-') ? ports[0] : null;
        var port = range == null ? int.Parse(ports[0]) : int.Parse(range.Split('-')[0]);
        var item = new ProfileItem { ConfigType = EConfigType.Mieru, Address = u.IdnHost.Trim('[', ']'), Port = port, Username = Uri.UnescapeDataString(auth[0]), Password = Uri.UnescapeDataString(auth[1]), Remarks = q["profile"]! };
        item.SetProtocolExtra(new ProtocolExtraItem { MieruTransport = protocols[0], MieruPortRange = range, MieruMultiplexing = q["multiplexing"], MieruHandshakeMode = q["handshake-mode"], MieruTrafficPattern = q["traffic-pattern"] });
        return item;
    }

    private static ProfileItem ResolveBinary(byte[] data)
    {
        var config = Read(data);
        var p = Read(Only(config, 1));
        if (p.Any(f => f.Number is 4 or 8)) throw new FormatException();
        var user = Read(Only(p, 2));
        var server = Read(Only(p, 3));
        var binding = Read(Only(server, 3));
        var range = Text(binding, 3);
        var port = range.IsNotEmpty() ? int.Parse(range.Split('-')[0]) : Number(binding, 1);
        var transport = Number(binding, 2) switch { 1 => "UDP", 2 => "TCP", _ => throw new FormatException() };
        var mux = p.FirstOrDefault(f => f.Number == 5);
        var muxLevel = mux == null ? 0 : Number(Read(mux.Data), 1);
        var mode = Number(p, 6);
        if (muxLevel >= Multiplexing.Length || mode >= Handshake.Length) throw new FormatException();
        var traffic = p.FirstOrDefault(f => f.Number == 7);
        var item = new ProfileItem { ConfigType = EConfigType.Mieru, Address = Text(server, 2).IsNotEmpty() ? Text(server, 2) : Text(server, 1), Port = port, Username = Text(user, 1), Password = Text(user, 2), Remarks = Text(p, 1) };
        item.SetProtocolExtra(new ProtocolExtraItem { MieruTransport = transport, MieruPortRange = range.NullIfEmpty(), MieruMultiplexing = muxLevel == 0 ? null : Multiplexing[muxLevel], MieruHandshakeMode = mode == 0 ? null : Handshake[mode], MieruTrafficPattern = traffic == null ? null : Convert.ToBase64String(traffic.Data) });
        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null) return null;
        var e = item.GetProtocolExtra();
        var binding = e.MieruPortRange.IsNotEmpty() ? Bytes(3, Encoding.UTF8.GetBytes(e.MieruPortRange)) : Var(1, item.Port);
        binding = [.. binding, .. Var(2, e.MieruTransport == "UDP" ? 1 : 2)];
        var server = Join(Bytes(Utils.IsDomain(item.Address) ? 2 : 1, Encoding.UTF8.GetBytes(item.Address)), Bytes(3, binding));
        var user = Join(Bytes(1, Encoding.UTF8.GetBytes(item.Username)), Bytes(2, Encoding.UTF8.GetBytes(item.Password)));
        var name = item.Remarks.IsNotEmpty() ? item.Remarks : "Mieru";
        var profile = Join(Bytes(1, Encoding.UTF8.GetBytes(name)), Bytes(2, user), Bytes(3, server));
        if (e.MieruMultiplexing.IsNotEmpty()) profile = Join(profile, Bytes(5, Var(1, Array.IndexOf(Multiplexing, e.MieruMultiplexing))));
        if (e.MieruHandshakeMode.IsNotEmpty()) profile = Join(profile, Var(6, Array.IndexOf(Handshake, e.MieruHandshakeMode)));
        if (e.MieruTrafficPattern.IsNotEmpty()) profile = Join(profile, Bytes(7, Convert.FromBase64String(e.MieruTrafficPattern)));
        return "mieru://" + Convert.ToBase64String(Join(Bytes(1, profile), Bytes(2, Encoding.UTF8.GetBytes(name))));
    }

    private sealed record Field(int Number, ulong Value, byte[] Data);
    private static List<Field> Read(byte[] data)
    {
        if (data.Length > 1048576) throw new FormatException();
        var fields = new List<Field>();
        var pos = 0;
        while (pos < data.Length)
        {
            var key = ReadVarint(data, ref pos);
            var number = checked((int)(key >> 3));
            if (number == 0) throw new FormatException();
            switch (key & 7)
            {
                case 0: fields.Add(new(number, ReadVarint(data, ref pos), [])); break;
                case 2:
                    var size = checked((int)ReadVarint(data, ref pos));
                    if (size > data.Length - pos) throw new FormatException();
                    fields.Add(new(number, 0, data[pos..(pos + size)])); pos += size; break;
                case 1: if (data.Length - pos < 8) throw new FormatException(); pos += 8; break;
                case 5: if (data.Length - pos < 4) throw new FormatException(); pos += 4; break;
                default: throw new FormatException();
            }
        }
        return fields;
    }

    private static ulong ReadVarint(byte[] data, ref int pos)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (pos >= data.Length) throw new FormatException();
            var b = data[pos++];
            if (shift == 63 && b > 1) throw new FormatException();
            value |= (ulong)(b & 127) << shift;
            if (b < 128) return value;
        }
        throw new FormatException();
    }

    private static byte[] Only(List<Field> fields, int number) => fields.Single(f => f.Number == number).Data;
    private static string Text(List<Field> fields, int number) => Encoding.UTF8.GetString(fields.LastOrDefault(f => f.Number == number)?.Data ?? []);
    private static int Number(List<Field> fields, int number) => checked((int)(fields.LastOrDefault(f => f.Number == number)?.Value ?? 0));
    private static byte[] Join(params byte[][] values) => values.SelectMany(v => v).ToArray();
    private static byte[] Varint(ulong value)
    {
        var data = new List<byte>();
        while (value >= 128) { data.Add((byte)((value & 127) | 128)); value >>= 7; }
        data.Add((byte)value);
        return data.ToArray();
    }
    private static byte[] Var(int number, int value) => value < 0 ? throw new FormatException() : Join(Varint((ulong)(number << 3)), Varint((ulong)value));
    private static byte[] Bytes(int number, byte[] value) => Join(Varint((ulong)((number << 3) | 2)), Varint((ulong)value.Length), value);
}
