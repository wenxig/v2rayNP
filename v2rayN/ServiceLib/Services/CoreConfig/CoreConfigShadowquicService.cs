using YamlDotNet.Serialization;

namespace ServiceLib.Services.CoreConfig;

public class CoreConfigShadowquicService(Config config)
{
    public static int GetListenPort(Config config)
    {
        var mixedPort = config.Inbound.FirstOrDefault(inbound => inbound.Protocol == nameof(EInboundProtocol.socks))?.LocalPort ?? Global.DefaultLocalPort;
        return mixedPort + (int)EInboundProtocol.shadowquic;
    }

    public RetResult GenerateClientConfig(ProfileItem node, int? listenPort = null)
    {
        var result = new RetResult { Msg = ResUI.InitialConfiguration };
        try
        {
            var validation = NodeValidator.Validate(node, ECoreType.shadowquic);
            if (!validation.Success) return new RetResult { Msg = string.Join(Environment.NewLine, validation.Errors) };
            var port = listenPort ?? GetListenPort(config);
            if (port is <= 0 or > 65535) return new RetResult { Msg = ResUI.FillCorrectServerPort };
            var extra = node.GetProtocolExtra();
            var outbound = new Dictionary<string, object>
            {
                ["tag"] = Global.ProxyTag,
                ["type"] = "shadowquic",
                ["addr"] = $"{(Utils.IsIpv6(node.Address) ? $"[{node.Address}]" : node.Address)}:{node.Port}",
                ["username"] = extra.ShadowquicUsername ?? node.Username,
                ["password"] = extra.ShadowquicPassword ?? node.Password,
                ["server-name"] = node.Sni.IsNotEmpty() ? node.Sni : node.Address,
                ["alpn"] = node.Alpn.IsNotEmpty() ? node.GetAlpn()! : new List<string> { "h3" },
                ["initial-mtu"] = extra.ShadowquicInitialMtu ?? 1300,
                ["congestion-control"] = extra.ShadowquicCongestionControl.IsNotEmpty() ? extra.ShadowquicCongestionControl.Replace('_', '-') : "bbr",
            };
            if (extra.ShadowquicUdpOverStream.HasValue) outbound["over-stream"] = extra.ShadowquicUdpOverStream.Value;
            if (extra.ShadowquicDisableMtuDiscovery.HasValue) outbound["mtu-discovery"] = !extra.ShadowquicDisableMtuDiscovery.Value;
            if (extra.ShadowquicMinMtu.HasValue) outbound["min-mtu"] = extra.ShadowquicMinMtu.Value;
            if (extra.ShadowquicKeepAliveInterval.HasValue) outbound["keep-alive-interval"] = extra.ShadowquicKeepAliveInterval.Value;
            if (extra.ShadowquicGso.HasValue) outbound["gso"] = extra.ShadowquicGso.Value;
            if (extra.ShadowquicBlackholeDetection.HasValue) outbound["blackhole-detection"] = extra.ShadowquicBlackholeDetection.Value;
            // Omitted options use the official client's defaults; false is an explicit override.
            if (extra.ShadowquicZeroRtt.HasValue)
            {
                outbound["zero-rtt"] = extra.ShadowquicZeroRtt.Value;
            }
            var content = new Dictionary<string, object>
            {
                ["inbounds"] = new[] { new Dictionary<string, object> { ["tag"] = "socks-in", ["type"] = "socks", ["bind-addr"] = $"{Global.Loopback}:{port}" } },
                ["outbounds"] = new[] { outbound },
                ["log-level"] = config.CoreBasicItem.Loglevel switch { "warning" => "warn", "none" => "error", var level => level },
            };
            result.Data = new SerializerBuilder().WithQuotingNecessaryStrings().Build().Serialize(content);
            result.Msg = string.Format(ResUI.SuccessfulConfiguration, node.GetSummary());
            result.Success = true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(CoreConfigShadowquicService), ex);
            result.Msg = ResUI.FailedGenDefaultConfiguration;
        }
        return result;
    }
}
