using YamlDotNet.RepresentationModel;

namespace ServiceLib.Tests.CoreConfig;

public class MihomoCapabilitiesTests
{
    private static ProfileItem Node(string id = "fake-ssr")
    {
        var node = new ProfileItem { IndexId = id, ConfigType = EConfigType.SSR, CoreType = ECoreType.mihomo, Address = "example.invalid", Port = 443, Password = "fake-password" };
        node.SetProtocolExtra(new ProtocolExtraItem { SsrCipher = "aes-128-ctr", SsrObfs = "plain", SsrProtocol = "origin" });
        return node;
    }

    [Test]
    [Arguments(EMultipleLoad.LeastPing, "url-test")]
    [Arguments(EMultipleLoad.Fallback, "fallback")]
    [Arguments(EMultipleLoad.RoundRobin, "load-balance")]
    public async Task GroupsGenerateRequestedStrategyAndManualMembers(EMultipleLoad strategy, string type)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.mihomo, "fake-group", "fake-group", ["one", "two"]);
        group.SetProtocolExtra(group.GetProtocolExtra() with { MultipleLoad = strategy });
        var context = CoreConfigTestFactory.CreateContext(config, group, ECoreType.mihomo);
        context.AllProxiesMap["one"] = Node("one");
        context.AllProxiesMap["two"] = Node("two");
        var result = await new CoreConfigClashService(config, false).GenerateClientNodeConfig(context, 25080);
        await result.Success.Should().BeTrue();
        var yaml = new YamlStream(); yaml.Load(new StringReader(result.Data!.ToString()!));
        var groups = (YamlSequenceNode)((YamlMappingNode)yaml.Documents[0].RootNode)["proxy-groups"];
        var automatic = groups.Children.Cast<YamlMappingNode>().Single(g => g["name"].ToString() == "node-fake-group-auto");
        await automatic["type"].ToString().Should().BeEqualTo(type);
        await automatic["interval"].ToString().Should().BeEqualTo("180");
        await ((YamlSequenceNode)automatic["proxies"]).Children.Count.Should().BeEqualTo(2);
        if (strategy == EMultipleLoad.RoundRobin) await automatic["strategy"].ToString().Should().BeEqualTo("round-robin");
    }

    [Test]
    [Arguments(EMultipleLoad.Random)]
    [Arguments(EMultipleLoad.LeastLoad)]
    public async Task UnsupportedStrategiesRejectBeforeGeneration(EMultipleLoad strategy)
    {
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.mihomo, "group", "group", ["one"]);
        group.SetProtocolExtra(group.GetProtocolExtra() with { MultipleLoad = strategy });
        await NodeValidator.Validate(group, ECoreType.mihomo).Success.Should().BeFalse();
        await ProtocolCapabilities.GroupStrategies(ECoreType.mihomo).Contains(strategy).Should().BeFalse();
    }

    [Test]
    public async Task MultiDimensionRulesPreserveAndOrIpv6AndCustomTarget()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        var context = CoreConfigTestFactory.CreateContext(config, Node(), ECoreType.mihomo);
        context.AllProxiesMap["remark:other"] = Node("other");
        context = context with { RoutingItem = new RoutingItem { RuleSet = JsonUtils.Serialize(new[] { new RulesItem
        { Remarks = "fake-combination", Domain = ["full:one.invalid", "domain:two.invalid"], Ip = ["2001:db8::1", "192.0.2.1"], Network = "tcp", Port = "443,8000:9000", Process = ["fake-process"], OutboundTag = "other" } }) } };
        var result = await new CoreConfigClashService(config, false).GenerateClientNodeConfig(context, 25080);
        await result.Success.Should().BeTrue();
        var text = result.Data!.ToString()!;
        await text.Contains("AND,(").Should().BeTrue();
        await text.Contains("OR,(").Should().BeTrue();
        await text.Contains("IP-CIDR6,2001:db8::1/128").Should().BeTrue();
        await text.Contains("DST-PORT,8000-9000").Should().BeTrue();
        await text.Contains("node-other").Should().BeTrue();
    }

    [Test]
    public async Task CustomUnsupportedRuleIsNamedAndNeverBecomesCatchAll()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        var context = CoreConfigTestFactory.CreateContext(config, Node(), ECoreType.mihomo) with
        { RoutingItem = new RoutingItem { RuleSet = JsonUtils.Serialize(new[] { new RulesItem { Remarks = "fake-bittorrent", Protocol = ["bittorrent"], OutboundTag = "direct" } }) } };
        var validation = ProtocolCapabilities.ValidateRouting(context);
        await validation.Success.Should().BeFalse();
        await validation.Errors.Single().Contains("fake-bittorrent").Should().BeTrue();
        await validation.Errors.Single().Contains("Protocol").Should().BeTrue();
        await (await new CoreConfigClashService(config, false).GenerateClientNodeConfig(context, 25080)).Success.Should().BeFalse();
    }

    [Test]
    [Arguments("white")]
    [Arguments("black")]
    [Arguments("global")]
    public async Task BuiltinTemplatesHaveExplicitMihomoAdaptation(string template)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        var context = CoreConfigTestFactory.CreateContext(config, Node(), ECoreType.mihomo) with
        { RoutingItem = new RoutingItem { RuleSet = EmbedUtils.GetEmbedText(Global.CustomRoutingFileName + template) } };
        var warnings = new List<string>();
        var adapted = ProtocolCapabilities.AdaptBuiltinRouting(context, warnings);
        await ProtocolCapabilities.ValidateRouting(adapted).Success.Should().BeTrue();
        await warnings.Count.Should().BeEqualTo(template == "black" ? 1 : 0);
        if (template == "black") await context.RoutingItem!.RuleSet.Contains("bittorrent").Should().BeTrue();
    }
}
