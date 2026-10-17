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
    public async Task NodeValidator_ShouldRejectUnsupportedCoreAndJuicityTun()
    {
        var node = CreateJuicityNode();
        await NodeValidator.Validate(node, ECoreType.mihomo).Success.Should().BeFalse();
        await NodeValidator.Validate(node, ECoreType.juicity).Success.Should().BeTrue();

        var config = CoreConfigTestFactory.CreateConfig();
        config.TunModeItem.EnableTun = true;
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
        var result = await CoreConfigContextBuilder.Build(config, node);
        await result.Success.Should().BeFalse();
        await result.ValidatorResult.Errors.Should().Contain(error => error.Contains("TUN", StringComparison.OrdinalIgnoreCase));
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
