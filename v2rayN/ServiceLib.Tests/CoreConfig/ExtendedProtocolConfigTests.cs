using YamlDotNet.RepresentationModel;

namespace ServiceLib.Tests.CoreConfig;

public class ExtendedProtocolConfigTests
{
    [Test]
    public async Task ProtocolCapabilities_ShouldRestrictCores()
    {
        await ProtocolCapabilities.AvailableCores(EConfigType.SSR).Should().BeEquivalentTo([nameof(ECoreType.mihomo)]);
        await ProtocolCapabilities.AvailableCores(EConfigType.Mieru).Should().BeEquivalentTo([nameof(ECoreType.mihomo)]);
        await ProtocolCapabilities.AvailableCores(EConfigType.ShadowQUIC).Should().BeEquivalentTo([nameof(ECoreType.mihomo)]);
        await ProtocolCapabilities.AvailableCores(EConfigType.Juicity).Should().BeEquivalentTo([nameof(ECoreType.juicity)]);
        await ProtocolCapabilities.AvailableCores(EConfigType.Custom).Should().Contain(nameof(ECoreType.Xray));
    }

    [Test]
    public async Task DefaultInboundPort_ShouldBe20808AndJuicityShouldUseOffset()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.Inbound = [];
        CoreConfigTestFactory.BindAppManagerConfig(config);

        await Global.DefaultLocalPort.Should().BeEqualTo(20808);
        await CoreConfigJuicityService.GetListenPort(config).Should().BeEqualTo(20815);
        await AppManager.Instance.GetLocalPort(EInboundProtocol.pac).Should().BeEqualTo(20811);
    }

    [Test]
    public async Task NodeValidator_ShouldRejectUnsupportedJuicityCore()
    {
        var node = CreateJuicityNode();
        await NodeValidator.Validate(node, ECoreType.mihomo).Success.Should().BeFalse();
        await NodeValidator.Validate(node, ECoreType.juicity).Success.Should().BeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Juicity_ShouldUseOffsetListenerBehindMixedPort(bool enableTun)
    {
        var node = CreateJuicityNode();
        var config = CoreConfigTestFactory.CreateConfig();
        config.Inbound.First().LocalPort = 12080;
        config.TunModeItem.EnableTun = enableTun;
        CoreConfigTestFactory.BindAppManagerConfig(config);
        SQLiteHelper.Instance.CreateTable<RoutingItem>();
        SQLiteHelper.Instance.CreateTable<FullConfigTemplateItem>();
        SQLiteHelper.Instance.CreateTable<DNSItem>();
        await SQLiteHelper.Instance.ReplaceAsync(new RoutingItem
        {
            Id = "extended-test-routing",
            IsActive = true,
            RuleSet = "[]",
            DomainStrategy = Global.AsIs,
            DomainStrategy4Singbox = string.Empty,
        });
        var result = await CoreConfigContextBuilder.BuildAll(config, node);
        await result.Success.Should().BeTrue();
        await result.MainResult.Context.IsTunEnabled.Should().BeFalse();
        var front = result.PreSocksResult!.Context;
        await front.RunCoreType.Should().BeEqualTo(ECoreType.sing_box);
        await front.Node.Address.Should().BeEqualTo(Global.Loopback);
        await front.Node.Port.Should().BeEqualTo(12087);
        await front.IsTunEnabled.Should().BeEqualTo(enableTun);
        await front.ProtectCoreTypeList.Should().Contain(ECoreType.juicity);

        var client = new CoreConfigJuicityService(config).GenerateClientConfig(node);
        await client.Success.Should().BeTrue();
        await JsonNode.Parse(client.Data!.ToString()!)!["listen"]!.GetValue<string>().Should().BeEqualTo("127.0.0.1:12087");
        var frontResult = new CoreConfigSingboxService(front).GenerateClientConfigContent();
        await frontResult.Success.Should().BeTrue();
        var content = JsonNode.Parse(frontResult.Data!.ToString()!)!;
        await content["inbounds"]!.AsArray().Should().Contain(inbound => inbound!["type"]!.GetValue<string>() == "mixed" && inbound["listen_port"]!.GetValue<int>() == 12080);
        await content["inbounds"]!.AsArray().Any(inbound => inbound!["type"]!.GetValue<string>() == "tun").Should().BeEqualTo(enableTun);
        await content["outbounds"]!.AsArray().Should().Contain(outbound => outbound!["type"]!.GetValue<string>() == "socks" && outbound["server"]!.GetValue<string>() == Global.Loopback && outbound["server_port"]!.GetValue<int>() == 12087);
    }

    [Test]
    public async Task JuicitySpeedtest_ShouldUseAssignedPortWithoutListenerOffset()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CreateJuicityNode();
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.juicity);
        var testItem = new ServerTestItem { IndexId = node.IndexId, QueueNum = 3 };
        var path = Path.GetTempFileName();
        try
        {
            var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(config, context, testItem, path);
            await result.Success.Should().BeTrue();
            var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            await json["listen"]!.GetValue<string>().Should().BeEqualTo($"127.0.0.1:{testItem.Port}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [Arguments(65528, true)]
    [Arguments(65529, false)]
    public async Task JuicityConfig_ShouldValidateOffsetPortRange(int mixedPort, bool success)
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.Inbound.First().LocalPort = mixedPort;
        var result = new CoreConfigJuicityService(config).GenerateClientConfig(CreateJuicityNode());
        await result.Success.Should().BeEqualTo(success);
    }

    [Test]
    [Arguments(EConfigType.SSR)]
    [Arguments(EConfigType.Mieru)]
    [Arguments(EConfigType.ShadowQUIC)]
    public async Task MihomoConfig_ShouldContainProtocolOutbound(EConfigType configType)
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.mihomo);
        var node = CreateMihomoNode(configType);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.mihomo);

        var result = await new CoreConfigClashService(config, false).GenerateClientNodeConfig(context);

        await result.Success.Should().BeTrue();
        var yaml = result.Data?.ToString() ?? string.Empty;
        await yaml.Should().Contain($"type: {configType.ToString().ToLowerInvariant()}");
        await yaml.Should().Contain("proxies:");
    }

    [Test]
    [Arguments("223.5.5.5", "IP-CIDR,223.5.5.5/32,DIRECT")]
    [Arguments("2400:3200::1", "IP-CIDR6,2400:3200::1/128,DIRECT")]
    [Arguments("192.168.0.0/16", "IP-CIDR,192.168.0.0/16,DIRECT")]
    [Arguments("fc00::/7", "IP-CIDR6,fc00::/7,DIRECT")]
    [Arguments("geoip:cn", "GEOIP,cn,DIRECT")]
    public async Task MihomoRouting_ShouldPreserveAddressScope(string address, string expectedRule)
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.mihomo);
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.mihomo);
        context.RoutingItem!.RuleSet = JsonUtils.Serialize(new List<RulesItem>
        {
            new() { Ip = [address], OutboundTag = Global.DirectTag }
        });

        var result = await new CoreConfigClashService(config, false).GenerateClientNodeConfig(context, 19080);

        await result.Success.Should().BeTrue();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(result.Data!.ToString()!));
        var rules = (YamlSequenceNode)((YamlMappingNode)yaml.Documents[0].RootNode)["rules"];
        await rules.Children[0].ToString().Should().BeEqualTo(expectedRule);
        await rules.Children[1].ToString().Should().BeEqualTo("MATCH,proxy");
    }

    [Test]
    public async Task MihomoRouting_ShouldPreserveLogicalConditionsWithHostAddresses()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.mihomo);
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.mihomo);
        context.RoutingItem!.RuleSet = JsonUtils.Serialize(new List<RulesItem>
        {
            new() { Ip = ["223.5.5.5", "2400:3200::1", "192.168.0.0/16"], OutboundTag = Global.DirectTag },
            new() { Ip = ["223.6.6.6", "2400:3200:baba::1"], Network = "udp", Port = "53", OutboundTag = Global.BlockTag },
            new() { Ip = ["geoip:private"], OutboundTag = Global.DirectTag }
        });

        var result = await new CoreConfigClashService(config, false).GenerateClientNodeConfig(context, 19080);

        await result.Success.Should().BeTrue();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(result.Data!.ToString()!));
        var rules = (YamlSequenceNode)((YamlMappingNode)yaml.Documents[0].RootNode)["rules"];
        await rules.Children[0].ToString().Should().BeEqualTo("OR,((IP-CIDR,223.5.5.5/32),(IP-CIDR6,2400:3200::1/128),(IP-CIDR,192.168.0.0/16)),DIRECT");
        await rules.Children[1].ToString().Should().BeEqualTo("AND,((OR,((IP-CIDR,223.6.6.6/32),(IP-CIDR6,2400:3200:baba::1/128))),(NETWORK,UDP),(DST-PORT,53)),REJECT");
        await rules.Children[2].ToString().Should().Contain("(IP-CIDR,10.0.0.0/8)");
        await rules.Children[2].ToString().Should().Contain("(IP-CIDR6,fc00::/7)");
    }

    [Test]
    public async Task JuicityConfig_ShouldContainListenAuthenticationAndTls()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.juicity);
        var node = CreateJuicityNode();
        var result = new CoreConfigJuicityService(config).GenerateClientConfig(node, 19080);

        await result.Success.Should().BeTrue();
        var json = JsonNode.Parse(result.Data!.ToString()!)!.AsObject();
        await json["listen"]!.GetValue<string>().Should().BeEqualTo("127.0.0.1:19080");
        await json["server"]!.GetValue<string>().Should().BeEqualTo("[2001:db8::50]:443");
        await json["uuid"]!.GetValue<string>().Should().BeEqualTo(node.Username);
        await json["password"]!.GetValue<string>().Should().BeEqualTo(node.Password);
        await json["sni"]!.GetValue<string>().Should().BeEqualTo(node.Sni);
        await json["congestion_control"]!.GetValue<string>().Should().BeEqualTo("bbr");
    }

    private static ProfileItem CreateMihomoNode(EConfigType configType)
    {
        var node = new ProfileItem
        {
            IndexId = $"{configType}-node",
            ConfigType = configType,
            CoreType = ECoreType.mihomo,
            Address = "example.com",
            Port = 443,
            Username = "user",
            Password = "password",
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
        };
        node.SetProtocolExtra(configType switch
        {
            EConfigType.SSR => new ProtocolExtraItem { SsrCipher = "chacha20-ietf", SsrObfs = "plain", SsrProtocol = "origin" },
            EConfigType.Mieru => new ProtocolExtraItem { MieruTransport = "TCP" },
            _ => new ProtocolExtraItem { ShadowquicUsername = "user", ShadowquicPassword = "password", ShadowquicQuicVersions = "v1" },
        });
        return node;
    }

    private static ProfileItem CreateJuicityNode()
    {
        var node = new ProfileItem
        {
            IndexId = "juicity-node",
            ConfigType = EConfigType.Juicity,
            CoreType = ECoreType.juicity,
            Address = "2001:db8::50",
            Port = 443,
            Username = "01234567-89ab-cdef-0123-456789abcdef",
            Password = "password",
            Sni = "juicity.example",
        };
        node.SetProtocolExtra(new ProtocolExtraItem
        {
            JuicityUuid = node.Username,
            JuicityPassword = node.Password,
            JuicitySni = node.Sni,
        });
        return node;
    }
}
