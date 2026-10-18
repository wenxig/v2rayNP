using YamlDotNet.RepresentationModel;

namespace ServiceLib.Tests.CoreConfig;

public class ExtendedProtocolConfigTests
{
    [Test]
    public async Task ProtocolCapabilities_ShouldRestrictCores()
    {
        await ProtocolCapabilities.AvailableCores(EConfigType.SSR).Should().BeEquivalentTo([nameof(ECoreType.mihomo)]);
        await ProtocolCapabilities.AvailableCores(EConfigType.Mieru).Should().BeEquivalentTo([nameof(ECoreType.mihomo)]);
        await ProtocolCapabilities.AvailableCores(EConfigType.ShadowQUIC).Should().BeEquivalentTo([nameof(ECoreType.shadowquic)]);
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
    [Arguments(false, EConfigType.Juicity)]
    [Arguments(true, EConfigType.Juicity)]
    [Arguments(false, EConfigType.ShadowQUIC)]
    [Arguments(true, EConfigType.ShadowQUIC)]
    public async Task StandaloneCore_ShouldUseOffsetListenerBehindMixedPort(bool enableTun, EConfigType configType)
    {
        var node = configType == EConfigType.Juicity ? CreateJuicityNode() : CreateMihomoNode(configType);
        var core = ProtocolCapabilities.DefaultCore(configType);
        var port = configType == EConfigType.Juicity ? 12087 : 12088;
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
        await front.Node.Port.Should().BeEqualTo(port);
        await front.IsTunEnabled.Should().BeEqualTo(enableTun);
        await front.ProtectCoreTypeList.Should().Contain(core);

        var client = await CoreConfigHandler.GenerateClientConfig(result.MainResult.Context, null);
        await client.Success.Should().BeTrue();
        await client.Data!.ToString()!.Should().Contain($"127.0.0.1:{port}");
        var frontResult = new CoreConfigSingboxService(front).GenerateClientConfigContent();
        await frontResult.Success.Should().BeTrue();
        var content = JsonNode.Parse(frontResult.Data!.ToString()!)!;
        await content["inbounds"]!.AsArray().Should().Contain(inbound => inbound!["type"]!.GetValue<string>() == "mixed" && inbound["listen_port"]!.GetValue<int>() == 12080);
        await content["inbounds"]!.AsArray().Any(inbound => inbound!["type"]!.GetValue<string>() == "tun").Should().BeEqualTo(enableTun);
        await content["outbounds"]!.AsArray().Should().Contain(outbound => outbound!["type"]!.GetValue<string>() == "socks" && outbound["server"]!.GetValue<string>() == Global.Loopback && outbound["server_port"]!.GetValue<int>() == port);
    }

    [Test]
    [Arguments(EConfigType.SSR)]
    [Arguments(EConfigType.Mieru)]
    [Arguments(EConfigType.ShadowQUIC)]
    [Arguments(EConfigType.Juicity)]
    public async Task SingleNodeSpeedtest_ShouldEnableTestingAfterConfigIsWritten(EConfigType configType)
    {
        var coreType = ProtocolCapabilities.DefaultCore(configType);
        var config = CoreConfigTestFactory.CreateConfig(coreType);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = configType == EConfigType.Juicity ? CreateJuicityNode() : CreateMihomoNode(configType);
        var context = CoreConfigTestFactory.CreateContext(config, node, coreType);
        var testItem = new ServerTestItem { IndexId = node.IndexId, QueueNum = 3 };
        var path = Path.GetTempFileName();
        try
        {
            var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(config, context, testItem, path);
            await result.Success.Should().BeTrue();
            await testItem.AllowTest.Should().BeTrue();
            if (coreType == ECoreType.juicity)
            {
                var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
                await json["listen"]!.GetValue<string>().Should().BeEqualTo($"127.0.0.1:{testItem.Port}");
            }
            else if (coreType == ECoreType.shadowquic)
            {
                var yaml = new YamlStream();
                yaml.Load(new StringReader(await File.ReadAllTextAsync(path)));
                var root = (YamlMappingNode)yaml.Documents[0].RootNode;
                var inbound = (YamlMappingNode)((YamlSequenceNode)root["inbounds"])[0];
                await inbound["bind-addr"].ToString().Should().BeEqualTo($"127.0.0.1:{testItem.Port}");
            }
            else
            {
                var yaml = new YamlStream();
                yaml.Load(new StringReader(await File.ReadAllTextAsync(path)));
                var root = (YamlMappingNode)yaml.Documents[0].RootNode;
                await root["mixed-port"].ToString().Should().BeEqualTo(testItem.Port.ToString());
                await root["bind-address"].ToString().Should().BeEqualTo(Global.Loopback);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [Arguments(EConfigType.ShadowQUIC)]
    [Arguments(EConfigType.Juicity)]
    public async Task SingleNodeSpeedtest_InvalidConfigShouldClearEligibility(EConfigType configType)
    {
        var coreType = ProtocolCapabilities.DefaultCore(configType);
        var config = CoreConfigTestFactory.CreateConfig(coreType);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = configType == EConfigType.Juicity ? CreateJuicityNode() : CreateMihomoNode(configType);
        node.Port = 0;
        var context = CoreConfigTestFactory.CreateContext(config, node, coreType);
        var testItem = new ServerTestItem { IndexId = node.IndexId, AllowTest = true };
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(config, context, testItem, path);
            await result.Success.Should().BeFalse();
            await testItem.AllowTest.Should().BeFalse();
            await File.Exists(path).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
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
        var node = CreateMihomoNode(EConfigType.Mieru);
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
        var node = CreateMihomoNode(EConfigType.Mieru);
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

    [Test]
    [Arguments(ECoreType.Xray, false)]
    [Arguments(ECoreType.sing_box, false)]
    [Arguments(ECoreType.mihomo, false)]
    [Arguments(ECoreType.juicity, false)]
    [Arguments(ECoreType.shadowquic, true)]
    public async Task Shadowquic_ShouldRequireOfficialCore(ECoreType core, bool success)
    {
        await NodeValidator.Validate(CreateMihomoNode(EConfigType.ShadowQUIC), core).Success.Should().BeEqualTo(success);
        await NodeValidator.Validate(new ProfileItem { ConfigType = EConfigType.PolicyGroup }, ECoreType.shadowquic).Success.Should().BeFalse();
    }

    [Test]
    public async Task Shadowquic_ShouldMigrateOnlyRetiredCoreMappings()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.CoreTypeItem.Add(new() { ConfigType = EConfigType.ShadowQUIC, CoreType = ECoreType.mihomo });
        ConfigHandler.MigrateShadowquicCore(config);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        await AppManager.Instance.GetCoreType(null, EConfigType.ShadowQUIC).Should().BeEqualTo(ECoreType.shadowquic);
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        node.CoreType = ECoreType.mihomo;
        await (await ConfigHandler.AddServerCommon(config, node, false)).Should().BeEqualTo(0);
        await node.CoreType.Should().BeEqualTo(ECoreType.shadowquic);
        await config.CoreTypeItem.First().CoreType.Should().BeEqualTo(ECoreType.Xray);
    }

    [Test]
    public async Task ShadowquicMigration_ShouldPersistOnlyTheRetiredProtocolCorePair()
    {
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        node.IndexId = Guid.NewGuid().ToString();
        node.CoreType = ECoreType.mihomo;
        var mieru = CreateMihomoNode(EConfigType.Mieru);
        mieru.IndexId = Guid.NewGuid().ToString();
        try
        {
            await SQLiteHelper.Instance.ReplaceAsync(node);
            await SQLiteHelper.Instance.ReplaceAsync(mieru);
            await AppManager.Instance.MigrateShadowquicProfiles();
            var migrated = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(x => x.IndexId == node.IndexId).FirstAsync();
            var retained = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(x => x.IndexId == mieru.IndexId).FirstAsync();
            await migrated.CoreType.Should().BeEqualTo(ECoreType.shadowquic);
            await migrated.GetProtocolExtra().Should().BeEqualTo(node.GetProtocolExtra());
            await retained.CoreType.Should().BeEqualTo(ECoreType.mihomo);
        }
        finally
        {
            await SQLiteHelper.Instance.DeleteAsync(node);
            await SQLiteHelper.Instance.DeleteAsync(mieru);
        }
    }

    [Test]
    [Arguments("012345", "true")]
    [Arguments("user: #test", "line1\nline2: #secret")]
    public async Task ShadowquicYaml_ShouldPreserveCredentials(string username, string password)
    {
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        node.SetProtocolExtra(node.GetProtocolExtra() with { ShadowquicUsername = username, ShadowquicPassword = password });
        var result = new CoreConfigShadowquicService(CoreConfigTestFactory.CreateConfig()).GenerateClientConfig(node);
        await result.Success.Should().BeTrue();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(result.Data!.ToString()!));
        var outbound = (YamlMappingNode)((YamlSequenceNode)((YamlMappingNode)yaml.Documents[0].RootNode)["outbounds"])[0];
        await outbound["username"].ToString().Should().BeEqualTo(username);
        await outbound["password"].ToString().Should().BeEqualTo(password);
    }

    [Test]
    public async Task ShadowquicConfig_ShouldMapOfficialFieldsAndRejectUnsupportedOptions()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        config.Inbound.First().LocalPort = 12080;
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        node.Address = "2001:db8::1";
        node.Sni = "sni.example";
        node.Alpn = "h3,hq-29";
        node.SetProtocolExtra(node.GetProtocolExtra() with
        {
            ShadowquicInitialMtu = 1400, ShadowquicZeroRtt = true,
            ShadowquicUdpOverStream = true, ShadowquicCongestionControl = "new_reno",
            ShadowquicDisableMtuDiscovery = true,
        });
        var result = new CoreConfigShadowquicService(config).GenerateClientConfig(node);
        await result.Success.Should().BeTrue();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(result.Data!.ToString()!));
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var inbound = (YamlMappingNode)((YamlSequenceNode)root["inbounds"])[0];
        var outbound = (YamlMappingNode)((YamlSequenceNode)root["outbounds"])[0];
        await inbound["bind-addr"].ToString().Should().BeEqualTo("127.0.0.1:12088");
        await outbound["addr"].ToString().Should().BeEqualTo("[2001:db8::1]:443");
        await outbound["username"].ToString().Should().BeEqualTo("user");
        await outbound["password"].ToString().Should().BeEqualTo("password");
        await outbound["server-name"].ToString().Should().BeEqualTo("sni.example");
        await outbound["initial-mtu"].ToString().Should().BeEqualTo("1400");
        await outbound["congestion-control"].ToString().Should().BeEqualTo("new-reno");
        await outbound["zero-rtt"].ToString().Should().BeEqualTo("true");
        await outbound["over-stream"].ToString().Should().BeEqualTo("true");
        await outbound["mtu-discovery"].ToString().Should().BeEqualTo("false");
        await ((YamlSequenceNode)outbound["alpn"]).Children.Select(x => x.ToString()).Should().BeEquivalentTo(["h3", "hq-29"]);
        node.SetProtocolExtra(node.GetProtocolExtra() with { ShadowquicCwnd = 10 });
        await new CoreConfigShadowquicService(config).GenerateClientConfig(node).Success.Should().BeFalse();
        node.SetProtocolExtra(node.GetProtocolExtra() with { ShadowquicCwnd = null, ShadowquicQuicVersions = "v2" });
        await new CoreConfigShadowquicService(config).GenerateClientConfig(node).Success.Should().BeFalse();
        node.SetProtocolExtra(node.GetProtocolExtra() with { ShadowquicQuicVersions = null });
        config.Inbound.First().LocalPort = 65528;
        await new CoreConfigShadowquicService(config).GenerateClientConfig(node).Success.Should().BeFalse();
        config.Inbound.First().LocalPort = 65527;
        await new CoreConfigShadowquicService(config).GenerateClientConfig(node).Success.Should().BeTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShadowquicConfig_ShouldDeferAbsentZeroRttToOfficialDefaultAndPreserveOverrides(bool? zeroRtt)
    {
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        node.SetProtocolExtra(node.GetProtocolExtra() with { ShadowquicZeroRtt = zeroRtt });
        var result = new CoreConfigShadowquicService(CoreConfigTestFactory.CreateConfig()).GenerateClientConfig(node);
        await result.Success.Should().BeTrue();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(result.Data!.ToString()!));
        var outbound = (YamlMappingNode)((YamlSequenceNode)((YamlMappingNode)yaml.Documents[0].RootNode)["outbounds"])[0];
        await outbound.Children.ContainsKey(new YamlScalarNode("zero-rtt")).Should().BeEqualTo(zeroRtt.HasValue);
        if (zeroRtt.HasValue)
        {
            await bool.Parse(outbound["zero-rtt"].ToString()).Should().BeEqualTo(zeroRtt.Value);
        }
    }

    [Test]
    [Arguments(null, true)]
    [Arguments(false, false)]
    [Arguments(true, true)]
    public async Task ShadowquicEditor_ShouldShowOfficialDefaultAndSaveExplicitOverride(bool? configured, bool expected)
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices());
        var previous = AppManager.Instance.Config;
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var node = CreateMihomoNode(EConfigType.ShadowQUIC);
        node.IndexId = string.Empty;
        node.Remarks = "test-shadowquic-editor";
        node.SetProtocolExtra(node.GetProtocolExtra() with { ShadowquicZeroRtt = configured });
        try
        {
            var editor = new ServiceLib.ViewModels.AddServerViewModel(node);
            await editor.ShadowquicZeroRtt.Should().BeEqualTo(expected);
            editor.ShadowquicZeroRtt = false;
            var save = typeof(ServiceLib.ViewModels.AddServerViewModel).GetMethod("SaveServerAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)save.Invoke(editor, null)!;
            await editor.SelectedSource.GetProtocolExtra().ShadowquicZeroRtt.Should().BeEqualTo(false);
            await node.IndexId.IsNotEmpty().Should().BeTrue();
            var saved = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(x => x.IndexId == node.IndexId).FirstAsync();
            await saved.GetProtocolExtra().ShadowquicZeroRtt.Should().BeEqualTo(false);
        }
        finally
        {
            if (node.IndexId.IsNotEmpty()) await SQLiteHelper.Instance.DeleteAsync(node);
            CoreConfigTestFactory.BindAppManagerConfig(previous);
        }
    }

    [Test]
    public async Task ShadowquicCore_ShouldExposeOfficialDownloadAndLaunchContract()
    {
        var core = CoreInfoManager.Instance.GetCoreInfo(ECoreType.shadowquic)!;
        await core.Arguments.Should().BeEqualTo("-c {0}");
        await core.VersionArg.Should().BeEqualTo("--version");
        await core.DownloadUrlOSX64.Should().EndWith("/shadowquic-x86_64-darwin");
        await core.DownloadUrlWinArm64.Should().EndWith("/shadowquic-aarch64-windows.exe");
        await CoreInfoManager.Instance.IsCheckUpdateSupported(ECoreType.shadowquic).Should().BeTrue();
    }

    private static ProfileItem CreateMihomoNode(EConfigType configType)
    {
        var node = new ProfileItem
        {
            IndexId = $"{configType}-node",
            ConfigType = configType,
            CoreType = ProtocolCapabilities.DefaultCore(configType),
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
