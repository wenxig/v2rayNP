using YamlDotNet.RepresentationModel;

namespace ServiceLib.Handler.Fmt;

public static class NativeListenerContract
{
    // Keep raw configs intact; require an explicit unauthenticated loopback endpoint.
    public static bool Matches(string content, ECoreType core, int port)
    {
        if (port is <= 0 or > 65535) return false;
        try
        {
            bool Endpoint(string? endpoint) => endpoint == $"127.0.0.1:{port}";
            if (core == ECoreType.juicity)
            {
                using var document = JsonDocument.Parse(content);
                var fields = document.RootElement.EnumerateObject().ToList();
                return fields.Count(f => f.Name == "listen") == 1
                    && document.RootElement.GetProperty("listen").ValueKind == JsonValueKind.String
                    && Endpoint(document.RootElement.GetProperty("listen").GetString());
            }
            if (core != ECoreType.shadowquic) return false;
            var yaml = new YamlStream();
            yaml.Load(new StringReader(content));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root) return false;
            YamlNode? Get(YamlMappingNode map, string key) => map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
            if (Get(root, "inbounds") != null && Get(root, "inbound") != null) return false;
            var inbound = Get(root, "inbounds") ?? Get(root, "inbound");
            var entries = inbound is YamlSequenceNode list ? list.Children : inbound == null ? [] : new List<YamlNode> { inbound };
            return entries.OfType<YamlMappingNode>().Any(map =>
                Get(map, "type")?.ToString() == "socks"
                && Endpoint(Get(map, "bind-addr")?.ToString())
                && map.Children.Keys.All(k => k.ToString() is "type" or "tag" or "bind-addr"));
        }
        catch { return false; }
    }
}
