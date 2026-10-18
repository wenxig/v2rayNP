using YamlDotNet.RepresentationModel;

namespace ServiceLib.Handler.Fmt;

public record NativeClientImport(bool Recognized, ProfileItem? Node, string Message);

/// <summary>Strict editable-node subset of ShadowQUIC v0.4.4 and Juicity v0.5.0 client schemas.</summary>
public static class NativeClientFmt
{
    private static readonly HashSet<string> ShadowFields = ["tag", "type", "addr", "username", "password", "server-name", "alpn", "initial-mtu", "min-mtu", "keep-alive-interval", "gso", "blackhole-detection", "congestion-control", "zero-rtt", "over-stream", "mtu-discovery"];
    private static readonly HashSet<string> JuicityFields = ["listen", "server", "uuid", "password", "sni", "allow_insecure", "congestion_control", "pinned_certchain_sha256", "log_level"];

    public static NativeClientImport Parse(string input)
    {
        var recognized = false;
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(input));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root) return new(false, null, "");
            var fields = Map(root);
            if (fields.ContainsKey("server") && fields.ContainsKey("uuid") && fields.ContainsKey("password") && fields.ContainsKey("listen"))
            {
                recognized = true;
                // Juicity accepts JSON; do not reinterpret an unrelated YAML document.
                using var json = JsonDocument.Parse(input);
                if (json.RootElement.EnumerateObject().GroupBy(p => p.Name).Any(g => g.Count() > 1)) throw new FormatException();
                RequireKnown(fields, JuicityFields);
                var node = Endpoint(Text(fields, "server"), EConfigType.Juicity);
                node.Sni = OptionalText(fields, "sni") ?? "";
                node.SetProtocolExtra(node.GetProtocolExtra() with
                {
                    JuicityUuid = Text(fields, "uuid"), JuicityPassword = Text(fields, "password"),
                    JuicitySni = OptionalText(fields, "sni"), JuicityAllowInsecure = Boolean(fields, "allow_insecure"),
                    JuicityCongestionControl = OptionalText(fields, "congestion_control"),
                    JuicityPinnedCertchainSha256 = OptionalText(fields, "pinned_certchain_sha256")
                });
                return Validate(node);
            }
            YamlMappingNode? outbound = null;
            if (OptionalText(fields, "type") == "shadowquic" && fields.ContainsKey("addr")) outbound = root;
            else if (fields.TryGetValue("outbounds", out var list) || fields.TryGetValue("outbound", out list))
            {
                var candidates = list is YamlSequenceNode sequence ? sequence.Children.ToList() : new List<YamlNode> { list };
                if (!candidates.OfType<YamlMappingNode>().Any(n => OptionalText(Map(n), "type") == "shadowquic")) return new(false, null, "");
                recognized = true;
                RequireKnown(fields, ["inbounds", "inbound", "outbounds", "outbound", "log-level"]);
                if (fields.ContainsKey("inbounds") && fields.ContainsKey("inbound") || fields.ContainsKey("outbounds") && fields.ContainsKey("outbound")) throw new FormatException();
                if (candidates.Count != 1 || candidates[0] is not YamlMappingNode single) return Rejected("outbounds");
                outbound = single;
                if (fields.TryGetValue("inbounds", out var inbound) || fields.TryGetValue("inbound", out inbound))
                {
                    var inbounds = inbound is YamlSequenceNode inSequence ? inSequence.Children.ToList() : new List<YamlNode> { inbound };
                    if (inbounds.Count != 1 || inbounds[0] is not YamlMappingNode inMap) return Rejected("inbounds");
                    var local = Map(inMap);
                    RequireKnown(local, ["tag", "type", "bind-addr"]);
                    if (Text(local, "type") is not ("socks" or "mixed")) return Rejected("inbounds.type");
                    _ = Text(local, "bind-addr");
                }
            }
            if (outbound == null) return new(false, null, "");
            recognized = true;
            fields = Map(outbound);
            RequireKnown(fields, ShadowFields);
            var item = Endpoint(Text(fields, "addr"), EConfigType.ShadowQUIC);
            item.Sni = Text(fields, "server-name");
            if (fields.TryGetValue("alpn", out var alpn))
            {
                if (alpn is not YamlSequenceNode protocols || protocols.Children.Any(p => p is not YamlScalarNode)) throw new FormatException();
                item.Alpn = string.Join(",", protocols.Children.Cast<YamlScalarNode>().Select(p => p.Value));
            }
            var discovery = Boolean(fields, "mtu-discovery");
            item.SetProtocolExtra(item.GetProtocolExtra() with
            {
                ShadowquicUsername = Text(fields, "username"), ShadowquicPassword = Text(fields, "password"),
                ShadowquicInitialMtu = Integer(fields, "initial-mtu"), ShadowquicMinMtu = Integer(fields, "min-mtu"),
                ShadowquicKeepAliveInterval = Integer(fields, "keep-alive-interval"), ShadowquicGso = Boolean(fields, "gso"),
                ShadowquicBlackholeDetection = Boolean(fields, "blackhole-detection"), ShadowquicZeroRtt = Boolean(fields, "zero-rtt"),
                ShadowquicUdpOverStream = Boolean(fields, "over-stream"), ShadowquicDisableMtuDiscovery = discovery.HasValue ? !discovery.Value : null,
                ShadowquicCongestionControl = OptionalText(fields, "congestion-control")
            });
            return Validate(item);
        }
        catch (UnsupportedFieldException ex) { return Rejected(ex.Field); }
        catch { return new(recognized, null, recognized ? ProtocolCapabilities.Text("MsgNativeConfigInvalid") : ""); }
    }

    private static NativeClientImport Validate(ProfileItem node)
    {
        node.CoreType = ProtocolCapabilities.DefaultCore(node.ConfigType);
        var result = NodeValidator.Validate(node, node.CoreType.Value);
        return result.Success ? new(true, node, "") : new(true, null, string.Join(Environment.NewLine, result.Errors));
    }

    private static ProfileItem Endpoint(string address, EConfigType type)
    {
        var split = address.LastIndexOf(':');
        if (split <= 0 || !int.TryParse(address[(split + 1)..], out var port)) throw new FormatException();
        var host = address[..split];
        if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
        else if (host.Contains(':')) throw new FormatException();
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown) throw new FormatException();
        return new ProfileItem { ConfigType = type, Address = host, Port = port, Remarks = $"{type} {host}" };
    }

    private sealed class UnsupportedFieldException(string field) : Exception { public string Field { get; } = field; }
    private static NativeClientImport Rejected(string field) => new(true, null, string.Format(ProtocolCapabilities.Text("MsgNativeConfigRejected"), field));
    private static Dictionary<string, YamlNode> Map(YamlMappingNode node) => node.Children.ToDictionary(p => ((YamlScalarNode)p.Key).Value!, p => p.Value, StringComparer.Ordinal);
    private static void RequireKnown(Dictionary<string, YamlNode> fields, HashSet<string> allowed)
    {
        // Never echo attacker-controlled key text; only identify a known structural category.
        if (fields.Keys.Any(k => !allowed.Contains(k))) throw new UnsupportedFieldException("schema");
    }
    private static string Text(Dictionary<string, YamlNode> fields, string key) => OptionalText(fields, key) ?? throw new FormatException();
    private static string? OptionalText(Dictionary<string, YamlNode> fields, string key) => fields.TryGetValue(key, out var value)
        ? value is YamlScalarNode scalar ? scalar.Value : throw new FormatException() : null;
    private static int? Integer(Dictionary<string, YamlNode> fields, string key) => OptionalText(fields, key) is string value ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : null;
    private static bool? Boolean(Dictionary<string, YamlNode> fields, string key) => OptionalText(fields, key) is string value ? value switch { "true" => true, "false" => false, _ => throw new FormatException() } : null;
}
