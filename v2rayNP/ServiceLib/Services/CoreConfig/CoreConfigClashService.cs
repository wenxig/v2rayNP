using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ServiceLib.Services.CoreConfig;

/// <summary>
/// Core configuration file processing class.
/// The TUN state is taken as a snapshot so the generated config always agrees
/// with the launch elevation decision (see CoreManager.ShouldRunAsSudo).
/// </summary>
public class CoreConfigClashService(Config config, bool isTunEnabled)
{
    private static readonly string _tag = "CoreConfigClashService";

    public async Task<RetResult> GenerateClientNodeConfig(CoreConfigContext context, int? listenPort = null)
    {
        var ret = new RetResult { Msg = ResUI.InitialConfiguration };
        try
        {
            var node = context.Node;
            var validation = NodeValidator.Validate(node, ECoreType.mihomo);
            if (!validation.Success) return new RetResult { Msg = string.Join(Environment.NewLine, validation.Errors) };
            var routingValidation = ProtocolCapabilities.ValidateRouting(context);
            if (!routingValidation.Success) return new RetResult { Msg = string.Join(Environment.NewLine, routingValidation.Errors) };
            var proxyName = $"node-{node.IndexId}";
            var content = new Dictionary<string, object>
            {
                ["mixed-port"] = listenPort ?? config.Inbound.First().LocalPort,
                ["allow-lan"] = config.Inbound.FirstOrDefault()?.AllowLANConn == true,
                ["mode"] = "rule",
                ["log-level"] = GetLogLevel(config.CoreBasicItem.Loglevel),
                ["proxies"] = new List<object>(),
                ["proxy-groups"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = Global.ProxyTag,
                        ["type"] = "select",
                        ["proxies"] = new List<string> { proxyName }
                    }
                }
            };
            var proxies = (List<object>)content["proxies"];
            var groups = (List<object>)content["proxy-groups"];
            foreach (var member in context.AllProxiesMap.Values.Append(node).DistinctBy(n => n.IndexId))
            {
                var check = NodeValidator.Validate(member, ECoreType.mihomo);
                if (!check.Success) return new RetResult { Msg = string.Join(Environment.NewLine, check.Errors) };
                if (member.ConfigType == EConfigType.PolicyGroup)
                {
                    var strategy = member.GetProtocolExtra().MultipleLoad ?? EMultipleLoad.LeastPing;
                    var automatic = $"node-{member.IndexId}-auto";
                    var members = Utils.String2List(member.GetProtocolExtra().ChildItems).Select(id => $"node-{id}").ToList();
                    var group = new Dictionary<string, object>
                    {
                        ["name"] = automatic,
                        ["type"] = strategy switch { EMultipleLoad.LeastPing => "url-test", EMultipleLoad.Fallback => "fallback", EMultipleLoad.RoundRobin => "load-balance", _ => throw new InvalidOperationException() },
                        ["proxies"] = members, ["url"] = config.SpeedTestItem.SpeedPingTestUrl, ["interval"] = 180, ["lazy"] = false
                    };
                    if (strategy == EMultipleLoad.RoundRobin) group["strategy"] = "round-robin";
                    groups.Add(group);
                    groups.Add(new Dictionary<string, object> { ["name"] = $"node-{member.IndexId}", ["type"] = "select", ["proxies"] = new[] { automatic }.Concat(members).ToList() });
                }
                else
                {
                    var outbound = BuildProxy(member);
                    outbound["name"] = $"node-{member.IndexId}";
                    proxies.Add(outbound);
                }
            }
            content["rules"] = BuildNodeRules(context);
            content["bind-address"] = listenPort == null && config.Inbound.First().AllowLANConn ? "*" : Global.Loopback;
            content["allow-lan"] = listenPort == null && config.Inbound.First().AllowLANConn;
            content["ipv6"] = config.ClashUIItem.EnableIPv6;
            if (listenPort == null)
            {
                content["external-controller"] = $"{Global.Loopback}:{AppManager.Instance.StatePort2}";
                if (isTunEnabled)
                {
                    content["tun"] = new Dictionary<string, object> { ["enable"] = true, ["stack"] = config.TunModeItem.Stack.IsNotEmpty() ? config.TunModeItem.Stack : "gvisor", ["auto-route"] = config.TunModeItem.AutoRoute, ["strict-route"] = config.TunModeItem.StrictRoute, ["auto-detect-interface"] = true, ["dns-hijack"] = new[] { "any:53" } };
                    content["dns"] = new Dictionary<string, object> { ["enable"] = true, ["enhanced-mode"] = "fake-ip", ["nameserver"] = new[] { "1.1.1.1" } };
                }
                await MixinContent(content, node);
            }
            ret.Data = YamlUtils.ToYaml(content);
            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, node.GetSummary());
            ret.Success = ret.Data is string text && text.IsNotEmpty();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
        }
        return ret;
    }

    private static List<string> BuildNodeRules(CoreConfigContext context)
    {
        var result = new List<string>();
        foreach (var rule in JsonUtils.Deserialize<List<RulesItem>>(context.RoutingItem?.RuleSet) ?? [])
        {
            if (!rule.Enabled || rule.RuleType == ERuleType.DNS) continue;
            var target = rule.OutboundTag switch
            {
                null or "" => Global.ProxyTag,
                var t when t == Global.ProxyTag => Global.ProxyTag,
                var t when t == Global.DirectTag => "DIRECT",
                var t when t == Global.BlockTag => "REJECT",
                var t => context.AllProxiesMap.TryGetValue($"remark:{t}", out var node) ? $"node-{node.IndexId}" : throw new InvalidOperationException()
            };
            var dimensions = new List<List<string>>();
            if (rule.Domain is { Count: > 0 }) dimensions.Add(rule.Domain.Select(domain => domain switch
            {
                var d when d.StartsWith("domain:") => $"DOMAIN-SUFFIX,{d[7..]}",
                var d when d.StartsWith("full:") => $"DOMAIN,{d[5..]}",
                var d when d.StartsWith("keyword:") => $"DOMAIN-KEYWORD,{d[8..]}",
                var d when d.StartsWith("regexp:") => $"DOMAIN-REGEX,{d[7..]}",
                var d when d.StartsWith("geosite:") => $"GEOSITE,{d[8..]}",
                var d => $"DOMAIN-KEYWORD,{d}"
            }).ToList());
            if (rule.Ip is { Count: > 0 }) dimensions.Add(rule.Ip.SelectMany(ip => ip == "geoip:private"
                ? new[] { "IP-CIDR,10.0.0.0/8", "IP-CIDR,172.16.0.0/12", "IP-CIDR,192.168.0.0/16", "IP-CIDR,127.0.0.0/8", "IP-CIDR6,fc00::/7", "IP-CIDR6,::1/128" }
                : new[] { BuildIpRule(ip) }).ToList());
            if (rule.Network.IsNotEmpty()) dimensions.Add(rule.Network.Split(',').Select(x => $"NETWORK,{x.Trim().ToUpperInvariant()}").ToList());
            if (rule.Port.IsNotEmpty()) dimensions.Add(rule.Port.Split(',').Select(x => $"DST-PORT,{x.Trim().Replace(':', '-')}").ToList());
            if (rule.Process is { Count: > 0 }) dimensions.Add(rule.Process.Select(x => $"PROCESS-NAME,{x}").ToList());

            if (rule.InboundTag is { Count: > 0 }) throw new InvalidOperationException(ProtocolCapabilities.Text("MsgMihomoInboundRuleUnsupported"));
            var conditions = dimensions.Select(values => values.Count == 1 ? values[0] : $"OR,({string.Join(',', values.Select(x => $"({x})"))})").ToList();
            if (conditions.Count == 0) result.Add($"MATCH,{target}");
            else if (conditions.Count == 1) result.Add($"{conditions[0]},{target}");
            else result.Add($"AND,({string.Join(',', conditions.Select(x => $"({x})"))}),{target}");
        }
        result.Add($"MATCH,{Global.ProxyTag}");
        return result;
    }

    private static string BuildIpRule(string address)
    {
        if (address.StartsWith("geoip:"))
        {
            return $"GEOIP,{address[6..]}";
        }

        var isIpv6 = address.Contains(':');
        if (!address.Contains('/') && IPAddress.TryParse(address, out var ip))
        {
            isIpv6 = ip.AddressFamily == AddressFamily.InterNetworkV6;
            address = $"{address}/{(isIpv6 ? 128 : 32)}";
        }

        return $"{(isIpv6 ? "IP-CIDR6" : "IP-CIDR")},{address}";
    }

    private static Dictionary<string, object> BuildProxy(ProfileItem node)
    {
        var e = node.GetProtocolExtra();
        var proxy = new Dictionary<string, object>
        {
            ["name"] = node.Remarks,
            ["type"] = Global.ProtocolTypes[node.ConfigType],
            ["server"] = node.Address,
            ["port"] = node.Port,
        };
        switch (node.ConfigType)
        {
            case EConfigType.SSR:
                proxy["cipher"] = e.SsrCipher ?? "chacha20-ietf";
                proxy["password"] = node.Password;
                proxy["obfs"] = e.SsrObfs ?? "plain";
                proxy["protocol"] = e.SsrProtocol ?? "origin";
                Add(proxy, "obfs-param", e.SsrObfsParam); Add(proxy, "protocol-param", e.SsrProtocolParam);
                if (e.SsrUdp == true) proxy["udp"] = true;
                break;
            case EConfigType.Mieru:
                proxy["username"] = node.Username; proxy["password"] = node.Password;
                proxy["transport"] = e.MieruTransport.IsNotEmpty() ? e.MieruTransport : "TCP";
                if (e.MieruPortRange.IsNotEmpty()) { proxy.Remove("port"); proxy["port-range"] = e.MieruPortRange; }
                if (e.MieruUdp == true) proxy["udp"] = true;
                Add(proxy, "multiplexing", e.MieruMultiplexing); Add(proxy, "handshake-mode", e.MieruHandshakeMode); Add(proxy, "traffic-pattern", e.MieruTrafficPattern);
                break;

        }
        return proxy;
    }

    private static void Add(Dictionary<string, object> target, string key, string? value) { if (value.IsNotEmpty()) target[key] = value; }

    public async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
    {
        var ret = new RetResult();
        if (node == null || fileName is null)
        {
            ret.Msg = ResUI.CheckServerSettings;
            return ret;
        }

        ret.Msg = ResUI.InitialConfiguration;

        try
        {
            if (node == null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            var addressFileName = node.Address;
            if (addressFileName.IsNullOrEmpty())
            {
                ret.Msg = ResUI.FailedGetDefaultConfiguration;
                return ret;
            }
            if (!File.Exists(addressFileName))
            {
                addressFileName = Path.Combine(Utils.GetConfigPath(), addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "1";
                return ret;
            }

            var tagYamlStr1 = "!<str>";
            var tagYamlStr2 = "__strn__";
            var tagYamlStr3 = "!!str";
            var txtFile = await File.ReadAllTextAsync(addressFileName);
            txtFile = txtFile.Replace(tagYamlStr1, tagYamlStr2);

            //YAML anchors
            if (txtFile.Contains("<<:") && txtFile.Contains('*') && txtFile.Contains('&'))
            {
                txtFile = YamlUtils.PreprocessYaml(txtFile);
            }

            var fileContent = YamlUtils.FromYaml<Dictionary<string, object>>(txtFile);
            if (fileContent == null)
            {
                ret.Msg = ResUI.FailedConversionConfiguration;
                return ret;
            }

            //mixed-port
            fileContent["mixed-port"] = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            //log-level
            fileContent["log-level"] = GetLogLevel(config.CoreBasicItem.Loglevel);

            //external-controller
            fileContent["external-controller"] = $"{Global.Loopback}:{AppManager.Instance.StatePort2}";
            fileContent.Remove("secret");
            //allow-lan
            if (config.Inbound.First().AllowLANConn)
            {
                fileContent["allow-lan"] = "true";
                fileContent["bind-address"] = "*";
            }
            else
            {
                fileContent["allow-lan"] = "false";
            }

            //ipv6
            fileContent["ipv6"] = config.ClashUIItem.EnableIPv6;

            //mode
            fileContent.TryAdd("mode", nameof(ERuleMode.Rule));

            //enable tun mode
            if (isTunEnabled)
            {
                var tun = EmbedUtils.GetEmbedText(Global.ClashTunYaml);
                if (tun.IsNotEmpty())
                {
                    var tunContent = YamlUtils.FromYaml<Dictionary<string, object>>(tun);
                    if (tunContent != null)
                    {
                        fileContent["tun"] = tunContent["tun"];
                    }
                }
            }

            //Mixin
            try
            {
                await MixinContent(fileContent, node);
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"{_tag}-Mixin", ex);
            }

            // Mihomo parses plain values such as 815458e4 as floats, so quote REALITY short IDs.
            var originalRealityShortIds = new List<(Dictionary<object, object> RealityOptions, string ShortId)>();
            if (fileContent.GetValueOrDefault("proxies") is List<object> proxies)
            {
                foreach (var proxy in proxies.OfType<Dictionary<object, object>>())
                {
                    if (proxy.GetValueOrDefault("reality-opts") is Dictionary<object, object> realityOptions
                        && realityOptions.GetValueOrDefault("short-id") is string shortId
                        && !shortId.StartsWith(tagYamlStr2, StringComparison.Ordinal))
                    {
                        originalRealityShortIds.Add((realityOptions, shortId));
                        realityOptions["short-id"] = new YamlScalarNode(shortId) { Style = ScalarStyle.DoubleQuoted };
                    }
                }
            }

            var txtFileNew = YamlUtils.ToYaml(fileContent).Replace(tagYamlStr2, tagYamlStr3);
            foreach (var (realityOptions, shortId) in originalRealityShortIds)
            {
                realityOptions["short-id"] = shortId;
            }
            await File.WriteAllTextAsync(fileName, txtFileNew);
            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedReadConfiguration + "2";
                return ret;
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, $"{node.GetSummary()}");
            ret.Success = true;
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    private async Task MixinContent(Dictionary<string, object> fileContent, ProfileItem node)
    {
        if (!config.ClashUIItem.EnableMixinContent)
        {
            return;
        }

        var path = Utils.GetConfigPath(Global.ClashMixinConfigFileName);
        if (!File.Exists(path))
        {
            var mixin = EmbedUtils.GetEmbedText(Global.ClashMixinYaml);
            await File.AppendAllTextAsync(path, mixin);
        }

        var txtFile = await File.ReadAllTextAsync(Utils.GetConfigPath(Global.ClashMixinConfigFileName));

        var mixinContent = YamlUtils.FromYaml<Dictionary<string, object>>(txtFile);
        if (mixinContent == null)
        {
            return;
        }
        foreach (var item in mixinContent)
        {
            if (!isTunEnabled && item.Key == "tun")
            {
                continue;
            }

            if (item.Key.StartsWith("prepend-")
                || item.Key.StartsWith("append-")
                || item.Key.StartsWith("removed-"))
            {
                ModifyContentMerge(fileContent, item.Key, item.Value);
            }
            else
            {
                fileContent[item.Key] = item.Value;
            }
        }
        return;
    }

    private void ModifyContentMerge(Dictionary<string, object> fileContent, string key, object value)
    {
        var blPrepend = false;
        var blRemoved = false;
        if (key.StartsWith("prepend-"))
        {
            blPrepend = true;
            key = key.Replace("prepend-", "");
        }
        else if (key.StartsWith("append-"))
        {
            blPrepend = false;
            key = key.Replace("append-", "");
        }
        else if (key.StartsWith("removed-"))
        {
            blRemoved = true;
            key = key.Replace("removed-", "");
        }
        else
        {
            return;
        }

        if (!blRemoved && fileContent.TryAdd(key, value))
        {
            return;
        }
        var lstOri = (List<object>)fileContent[key];
        var lstValue = (List<object>)value;

        if (blRemoved)
        {
            foreach (var item in lstValue)
            {
                lstOri.RemoveAll(t => t.ToString().StartsWith(item.ToString()));
            }
            return;
        }

        if (blPrepend)
        {
            lstValue.Reverse();
            lstValue.ForEach(item => lstOri.Insert(0, item));
        }
        else
        {
            lstValue.ForEach(lstOri.Add);
        }
    }

    private string GetLogLevel(string level)
    {
        if (level == "none")
        {
            return "silent";
        }
        else
        {
            return level;
        }
    }
}
