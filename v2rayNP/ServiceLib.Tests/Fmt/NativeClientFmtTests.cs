using ServiceLib.Tests.CoreConfig;

namespace ServiceLib.Tests.Fmt;

public class NativeClientFmtTests
{
    private const string Outbound = """
        type: shadowquic
        addr: "[2001:db8::1]:443"
        username: "fake:user"
        password: "fake%secret"
        server-name: example.invalid
        alpn: [h3]
        min-mtu: 1200
        initial-mtu: 1250
        keep-alive-interval: 0
        gso: false
        blackhole-detection: true
        zero-rtt: false
        over-stream: false
        mtu-discovery: false
        """;

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShadowquicImportPreservesOptionsAcrossUriAndGeneration(bool full)
    {
        var input = full ? "inbounds:\n- {tag: local, type: socks, bind-addr: \"127.0.0.1:9999\"}\noutbounds:\n- " + Outbound.Replace("\n", "\n  ") : Outbound;
        var result = NativeClientFmt.Parse(input);
        await result.Recognized.Should().BeTrue();
        await (result.Node != null).Should().BeTrue();
        var node = result.Node!;
        await node.Address.Should().BeEqualTo("2001:db8::1");
        await node.Port.Should().BeEqualTo(443);
        var exported = FmtHandler.GetShareUri(node)!;
        var roundTrip = FmtHandler.ResolveConfig(exported, out _)!;
        await roundTrip.GetProtocolExtra().Should().BeEqualTo(node.GetProtocolExtra());
        var config = CoreConfigTestFactory.CreateConfig();
        var generated = new CoreConfigShadowquicService(config).GenerateClientConfig(roundTrip);
        await generated.Success.Should().BeTrue();
        var importedAgain = NativeClientFmt.Parse(generated.Data!.ToString()!);
        await importedAgain.Node!.GetProtocolExtra().Should().BeEqualTo(node.GetProtocolExtra() with { ShadowquicCongestionControl = "bbr" });
    }

    [Test]
    [Arguments("null")]
    [Arguments("\"false\"")]
    [Arguments("0")]
    public async Task JuicityRejectsWrongJsonBooleanTypes(string value)
    {
        var input = "{\"listen\":\"127.0.0.1:12345\",\"server\":\"example.invalid:443\",\"uuid\":\"00000000-0000-0000-0000-000000000001\",\"password\":\"fake\",\"allow_insecure\":" + value + "}";
        var result = NativeClientFmt.Parse(input);
        await result.Recognized.Should().BeTrue();
        await (result.Node == null).Should().BeTrue();
    }

    [Test]
    [Arguments("127.0.0.1:25080", 25080, true)]
    [Arguments("0.0.0.0:25080", 25080, false)]
    [Arguments("127.0.0.1:25081", 25080, false)]
    public async Task RawNativeConfigRequiresExplicitMatchingLoopbackSocks(string listen, int port, bool accepted)
    {
        await NativeListenerContract.Matches("{\"listen\":\"" + listen + "\"}", ECoreType.juicity, port).Should().BeEqualTo(accepted);
        await NativeListenerContract.Matches($"inbounds:\n- {{type: socks, bind-addr: '{listen}'}}\nrouter: {{}}", ECoreType.shadowquic, port).Should().BeEqualTo(accepted);
        await NativeListenerContract.Matches($"inbounds:\n- {{type: socks, bind-addr: '{listen}', username: fake}}", ECoreType.shadowquic, port).Should().BeFalse();
    }

    [Test]
    [Arguments(EConfigType.ShadowQUIC)]
    [Arguments(EConfigType.Juicity)]
    public async Task EditingAndSavingPreservesUnsetNativeBooleans(EConfigType type)
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices());
        var previous = AppManager.Instance.Config;
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        var input = type == EConfigType.ShadowQUIC
            ? "type: shadowquic\naddr: example.invalid:443\nusername: fake\npassword: fake\nserver-name: example.invalid"
            : "{\"listen\":\"127.0.0.1:12345\",\"server\":\"example.invalid:443\",\"uuid\":\"00000000-0000-0000-0000-000000000001\",\"password\":\"fake\"}";
        var node = NativeClientFmt.Parse(input).Node!;
        try
        {
            var editor = new ServiceLib.ViewModels.AddServerViewModel(node);
            var save = typeof(ServiceLib.ViewModels.AddServerViewModel).GetMethod("SaveServerAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)save.Invoke(editor, null)!;
            var saved = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(n => n.IndexId == node.IndexId).FirstAsync();
            var extra = saved.GetProtocolExtra();
            await (extra.ShadowquicZeroRtt == null && extra.ShadowquicUdpOverStream == null
                && extra.ShadowquicDisableMtuDiscovery == null && extra.JuicityAllowInsecure == null).Should().BeTrue();
        }
        finally
        {
            if (node.IndexId.IsNotEmpty()) await SQLiteHelper.Instance.DeleteAsync(node);
            CoreConfigTestFactory.BindAppManagerConfig(previous);
        }
    }

    [Test]
    public async Task JuicityJsonPreservesFalseAndIpv6WithoutUsingLocalListenerAsServer()
    {
        var result = NativeClientFmt.Parse("""
            {"listen":"127.0.0.1:12345","server":"[2001:db8::2]:443","uuid":"00000000-0000-0000-0000-000000000001","password":"fake:password%","sni":"example.invalid","allow_insecure":false,"congestion_control":"bbr"}
            """);
        await result.Node!.Address.Should().BeEqualTo("2001:db8::2");
        await result.Node.Port.Should().BeEqualTo(443);
        var uri = FmtHandler.GetShareUri(result.Node)!;
        await uri.Contains("allow_insecure=0").Should().BeTrue();
        var imported = FmtHandler.ResolveConfig(uri, out _)!;
        await imported.GetProtocolExtra().JuicityAllowInsecure.Should().BeEqualTo(false);
        await imported.GetProtocolExtra().JuicityPassword.Should().BeEqualTo("fake:password%");
    }

    [Test]
    [Arguments("router: {}\noutbounds:\n- {type: shadowquic}")]
    [Arguments("outbounds:\n- {type: shadowquic}\n- {type: direct}")]
    [Arguments("type: shadowquic\naddr: example.invalid:443\nunknown-secret-token: fake")]
    public async Task ComplexOrUnknownConfigsAreRecognizedAndRejectedWithoutEchoingSecrets(string text)
    {
        var result = NativeClientFmt.Parse(text);
        await result.Recognized.Should().BeTrue();
        await (result.Node == null).Should().BeTrue();
        await result.Message.Contains("secret-token").Should().BeFalse();
        await result.Message.IsNotEmpty().Should().BeTrue();
    }

    [Test]
    [Arguments("{\"inbounds\":[],\"outbounds\":[{\"type\":\"socks\",\"server\":\"fake\"}]}")]
    [Arguments("proxies: [{type: ss, server: fake}]\nproxy-groups: []")]
    [Arguments("{\"outbounds\":[{\"protocol\":\"vless\"}]}")]
    public async Task OtherCoreSchemasAreNotIntercepted(string text)
    {
        await NativeClientFmt.Parse(text).Recognized.Should().BeFalse();
    }

    [Test]
    [Arguments(1200, 1250, true)]
    [Arguments(1290, 1280, false)]
    [Arguments(1199, 1300, false)]
    [Arguments(1200, 65528, false)]
    public async Task MtuValidationUsesIndependentMinimumAndInitialLimits(int minimum, int initial, bool valid)
    {
        var input = Outbound.Replace("min-mtu: 1200", $"min-mtu: {minimum}").Replace("initial-mtu: 1250", $"initial-mtu: {initial}");
        await (NativeClientFmt.Parse(input).Node != null).Should().BeEqualTo(valid);
    }

    [Test]
    public async Task SubscriptionPersistenceAndEditorKeepAdvancedFields()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices());
        var previous = AppManager.Instance.Config;
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        var subid = $"native-test-{Guid.NewGuid():N}";
        await SQLiteHelper.Instance.ReplaceAsync(new SubItem { Id = subid, Remarks = "fake native subscription" });
        try
        {
            await (await ConfigHandler.AddBatchServers(config, Outbound, subid, true)).Should().BeEqualTo(1);
            var node = (await AppManager.Instance.ProfileItems(subid)).Single();
            var editor = new ServiceLib.ViewModels.AddServerViewModel(node);
            await editor.ShadowquicGso.Should().BeEqualTo(false);
            await editor.ShadowquicMinMtu.Should().BeEqualTo(1200);
            var save = typeof(ServiceLib.ViewModels.AddServerViewModel).GetMethod("SaveServerAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)save.Invoke(editor, null)!;
            var saved = await SQLiteHelper.Instance.TableAsync<ProfileItem>().Where(n => n.IndexId == node.IndexId).FirstAsync();
            await saved.GetProtocolExtra().ShadowquicGso.Should().BeEqualTo(false);
            await saved.GetProtocolExtra().ShadowquicKeepAliveInterval.Should().BeEqualTo(0);
            await (await ConfigHandler.AddBatchServers(config, "router: {}\noutbounds:\n- {type: shadowquic}", subid, true)).Should().BeEqualTo(-1);
            await (await AppManager.Instance.ProfileItems(subid)).Count.Should().BeEqualTo(1);
        }
        finally
        {
            foreach (var node in await AppManager.Instance.ProfileItems(subid)) await SQLiteHelper.Instance.DeleteAsync(node);
            await SQLiteHelper.Instance.DeleteAsync(new SubItem { Id = subid });
            CoreConfigTestFactory.BindAppManagerConfig(previous);
        }
    }
}
