namespace ServiceLib.Handler.Builder;

/// <summary>Builds runtime-only SOCKS references; persisted profiles retain their native protocol.</summary>
public static class ManagedGroupBuilder
{
    public static CoreConfigContext PrepareSpeedtest(CoreConfigContext context, int port)
    {
        var config = JsonUtils.DeepCopy(context.AppConfig);
        var start = (long)config.CoreBasicItem.ManagedBackendPortStart + config.CoreBasicItem.ManagedBackendPortCount;
        if (port is < 1024 or > 65535 || start + config.CoreBasicItem.ManagedBackendPortCount > 65536
            || context.AppConfig.Inbound.Any(i => port >= i.LocalPort && port < (long)i.LocalPort + 10)
            || port >= config.CoreBasicItem.ManagedBackendPortStart && port < start
            || port == AppManager.Instance.StatePort || port == AppManager.Instance.StatePort2)
            throw new InvalidOperationException(ResUI.FillCorrectServerPort);
        config.CoreBasicItem.ManagedBackendPortStart = (int)start;
        config.CoreBasicItem.EnableCacheFile4Sbox = false;
        config.TunModeItem.EnableTun = false;
        config.ClashUIItem.EnableMixinContent = false;
        config.Inbound.First().LocalPort = port;
        config.Inbound.First().AllowLANConn = false;
        var map = new Dictionary<string, ProfileItem>(context.AllProxiesMap);
        foreach (var backend in context.ManagedBackends)
            foreach (var key in map.Where(p => p.Value.IndexId == backend.Node.IndexId).Select(p => p.Key).ToList())
                map[key] = backend.Node;
        return Prepare(context with
        {
            AppConfig = config, AllProxiesMap = map, ManagedBackends = [],
            IsTunEnabled = false, IsSpeedtest = true, RoutingItem = new RoutingItem { RuleSet = "[]" },
            ReservedPorts = [.. context.ReservedPorts, .. context.AppConfig.Inbound.SelectMany(i => Enumerable.Range(i.LocalPort, 10))]
        });
    }

    public static CoreConfigContext Prepare(CoreConfigContext context)
    {
        var nodes = context.AllProxiesMap.Values.Where(n => ProtocolCapabilities.IsExtendedProtocol(n.ConfigType))
            .DistinctBy(n => n.IndexId).OrderBy(n => n.IndexId, StringComparer.Ordinal).ToList();
        if (nodes.Count == 0 || context.RunCoreType != ECoreType.sing_box || context.Node.ConfigType != EConfigType.PolicyGroup) return context;
        if (context.AllProxiesMap.Values.Any(n => n.ConfigType == EConfigType.ProxyChain))
            throw new InvalidOperationException(ResUI.MsgCoreNotSupportProtocol);
        var start = context.AppConfig.CoreBasicItem.ManagedBackendPortStart;
        var count = context.AppConfig.CoreBasicItem.ManagedBackendPortCount;
        if (start < 1024 || count < nodes.Count || count > 4096 || (long)start + count > 65536)
            throw new InvalidOperationException(ResUI.FillCorrectServerPort);
        var reserved = context.AppConfig.Inbound.SelectMany(i => Enumerable.Range(i.LocalPort, 10)).ToHashSet();
        reserved.UnionWith(context.ReservedPorts);
        reserved.Add(AppManager.Instance.StatePort);
        reserved.Add(AppManager.Instance.StatePort2);
        var ports = Enumerable.Range(start, count).Where(p => !reserved.Contains(p)).Take(nodes.Count).ToList();
        if (ports.Count != nodes.Count) throw new InvalidOperationException(ResUI.FillCorrectServerPort);
        var map = new Dictionary<string, ProfileItem>(context.AllProxiesMap);
        var backends = new List<CoreConfigContext>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = JsonUtils.DeepCopy(nodes[i]);
            var core = ProtocolCapabilities.DefaultCore(node.ConfigType);
            var backendConfig = JsonUtils.DeepCopy(context.AppConfig);
            backendConfig.TunModeItem.EnableTun = false;
            backendConfig.ClashUIItem.EnableMixinContent = false;
            backendConfig.Inbound.First().AllowLANConn = false;
            backends.Add(context with
            {
                Node = node, RunCoreType = core, AppConfig = backendConfig, IsTunEnabled = false, ListenPort = ports[i],
                RoutingItem = new RoutingItem { RuleSet = "[]" },
                AllProxiesMap = new() { [node.IndexId] = node }, ManagedBackends = []
            });
            var socks = new ProfileItem
            {
                IndexId = node.IndexId, Remarks = node.Remarks, ConfigType = EConfigType.SOCKS,
                CoreType = ECoreType.sing_box, Address = Global.Loopback, Port = ports[i], Network = nameof(ETransport.raw)
            };
            foreach (var key in map.Where(p => p.Value.IndexId == node.IndexId).Select(p => p.Key).ToList()) map[key] = socks;
        }
        return context with { AllProxiesMap = map, ManagedBackends = backends };
    }
}
