namespace ServiceLib.Handler;

public static class ProtocolCapabilities
{
    public static bool IsStandaloneCore(ECoreType type) => type is ECoreType.juicity or ECoreType.shadowquic;

    public static bool IsExtendedProtocol(EConfigType type) => type is EConfigType.SSR or EConfigType.Mieru or EConfigType.ShadowQUIC or EConfigType.Juicity;

    public static ECoreType DefaultCore(EConfigType type) => type switch
    {
        EConfigType.SSR or EConfigType.Mieru => ECoreType.mihomo,
        EConfigType.ShadowQUIC => ECoreType.shadowquic,
        EConfigType.Juicity => ECoreType.juicity,
        _ => ECoreType.Xray
    };

    public static IEnumerable<string> AvailableCores(EConfigType type)
    {
        if (type == EConfigType.Custom) return Global.CoreTypes;
        if (IsExtendedProtocol(type)) return [DefaultCore(type).ToString()];
        return new[] { ECoreType.Xray, ECoreType.sing_box }
            .Where(core => (core == ECoreType.Xray ? Global.XraySupportConfigType : Global.SingboxSupportConfigType).Contains(type))
            .Select(core => core.ToString());
    }

    public static IEnumerable<ECoreType> GroupCores(EConfigType type) => type == EConfigType.PolicyGroup
        ? [ECoreType.Xray, ECoreType.sing_box, ECoreType.mihomo] : [ECoreType.Xray, ECoreType.sing_box];

    public static IEnumerable<EMultipleLoad> GroupStrategies(ECoreType core) => core switch
    {
        ECoreType.mihomo => [EMultipleLoad.LeastPing, EMultipleLoad.Fallback, EMultipleLoad.RoundRobin],
        ECoreType.sing_box => [EMultipleLoad.LeastPing],
        ECoreType.Xray => Enum.GetValues<EMultipleLoad>(),
        _ => []
    };

    public static bool CanGroupMember(ECoreType core, EConfigType type) => core switch
    {
        ECoreType.mihomo => type is EConfigType.SSR or EConfigType.Mieru or EConfigType.PolicyGroup,
        ECoreType.sing_box => Global.SingboxSupportConfigType.Contains(type) || type.IsGroupType(),
        ECoreType.Xray => Global.XraySupportConfigType.Contains(type) || type.IsGroupType(),
        _ => false
    };

    public static string StrategyLabel(EMultipleLoad strategy) => strategy switch
    {
        EMultipleLoad.LeastPing => ResUI.TbLeastPing, EMultipleLoad.Fallback => ResUI.TbFallback,
        EMultipleLoad.RoundRobin => ResUI.TbRoundRobin, EMultipleLoad.Random => ResUI.TbRandom,
        EMultipleLoad.LeastLoad => ResUI.TbLeastLoad, _ => strategy.ToString()
    };

    public static CoreConfigContext AdaptBuiltinRouting(CoreConfigContext context, List<string> warnings)
    {
        if (context.RunCoreType != ECoreType.mihomo || context.Node.ConfigType == EConfigType.Custom) return context;
        var rules = JsonUtils.Deserialize<List<RulesItem>>(context.RoutingItem?.RuleSet);
        var original = JsonUtils.Deserialize<List<RulesItem>>(EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + "black"));
        if (rules == null || original == null || rules.Count != original.Count) return context;
        string Signature(RulesItem rule)
        {
            var copy = JsonUtils.DeepCopy(rule);
            copy.Id = string.Empty;
            return JsonUtils.Serialize(copy);
        }
        if (!rules.Select(Signature).SequenceEqual(original.Select(Signature))) return context;
        var routing = JsonUtils.DeepCopy(context.RoutingItem!);
        routing.RuleSet = JsonUtils.Serialize(rules.Where(r => r.Protocol?.Contains("bittorrent") != true).ToList());
        warnings.Add(Text("MsgMihomoBuiltinBlacklist"));
        return context with { RoutingItem = routing };
    }

    public static NodeValidatorResult ValidateRouting(CoreConfigContext context)
    {
        var result = NodeValidatorResult.Empty();
        if (context.RunCoreType != ECoreType.mihomo || context.Node.ConfigType == EConfigType.Custom) return result;
        foreach (var rule in JsonUtils.Deserialize<List<RulesItem>>(context.RoutingItem?.RuleSet) ?? [])
        {
            if (!rule.Enabled || rule.RuleType == ERuleType.DNS) continue;
            void Reject(string field) => result.Errors.Add(string.Format(Text("MsgMihomoRuleUnsupported"), rule.Remarks ?? rule.Id, field));
            if (rule.InboundTag is { Count: > 0 }) Reject(nameof(rule.InboundTag));
            if (rule.Protocol is { Count: > 0 }) Reject(nameof(rule.Protocol));
            if (rule.Type.IsNotEmpty() && rule.Type != "field") Reject(nameof(rule.Type));
            if (rule.Network.IsNotEmpty() && rule.Network.Split(',').Any(n => n.Trim() is not ("tcp" or "udp"))) Reject(nameof(rule.Network));
            if (rule.Port.IsNotEmpty() && rule.Port.Split(',').Any(p => !ValidPortRange(p.Trim()))) Reject(nameof(rule.Port));
            if (rule.Ip?.Any(ip => !ip.StartsWith("geoip:") && !IPNetwork2.TryParse(ip, out _)) == true) Reject(nameof(rule.Ip));
            if (rule.Domain?.Any(d => d.Contains(':') && !new[] { "domain:", "full:", "keyword:", "regexp:", "geosite:" }.Any(d.StartsWith)) == true) Reject(nameof(rule.Domain));
            if (rule.OutboundTag.IsNotEmpty() && !Global.OutboundTags.Contains(rule.OutboundTag)
                && !context.AllProxiesMap.ContainsKey($"remark:{rule.OutboundTag}")) Reject(nameof(rule.OutboundTag));
        }
        return result;
    }

    private static bool ValidPortRange(string value)
    {
        var parts = value.Replace(':', '-').Split('-');
        return parts.Length is 1 or 2 && parts.All(p => int.TryParse(p, out var port) && port is >= 0 and <= 65535)
            && (parts.Length == 1 || int.Parse(parts[0]) <= int.Parse(parts[1]));
    }

    public static string Text(string key) => ResUI.ResourceManager.GetString(key, ResUI.Culture) ?? key;
}
