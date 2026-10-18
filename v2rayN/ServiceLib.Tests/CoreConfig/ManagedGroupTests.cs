namespace ServiceLib.Tests.CoreConfig;

public class ManagedGroupTests
{
    private static ProfileItem Juicity(string id)
    {
        var node = new ProfileItem { IndexId = id, ConfigType = EConfigType.Juicity, CoreType = ECoreType.juicity, Address = "example.invalid", Port = 443, Remarks = id, Network = nameof(ETransport.raw) };
        node.SetProtocolExtra(new ProtocolExtraItem { JuicityUuid = "11111111-1111-4111-8111-111111111111", JuicityPassword = "fake-password" });
        return node;
    }

    private static CoreConfigContext Context()
    {
        var config = CoreConfigTestFactory.CreateConfig();
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var group = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.sing_box, "fake-group", "fake-group", ["b", "a"]);
        var context = CoreConfigTestFactory.CreateContext(config, group, ECoreType.sing_box);
        context.AllProxiesMap["b"] = Juicity("b");
        context.AllProxiesMap["a"] = Juicity("a");
        return context;
    }

    [Test]
    public async Task SpeedtestUsesSeparateBackendsWithoutControllerCacheOrProductionMutation()
    {
        var production = ManagedGroupBuilder.Prepare(Context());
        var test = ManagedGroupBuilder.PrepareSpeedtest(production, 28080);
        await test.ManagedBackends.Select(b => b.ListenPort).SequenceEqual(new int?[] { 26200, 26201 }).Should().BeTrue();
        await production.ManagedBackends[0].ListenPort.Should().BeEqualTo(26000);
        await production.AppConfig.Inbound[0].LocalPort.Should().BeEqualTo(20808);
        await test.ManagedBackends.All(b => b.Node.ConfigType == EConfigType.Juicity).Should().BeTrue();
        var result = await CoreConfigHandler.GenerateClientConfig(test, null);
        await result.Success.Should().BeTrue();
        var json = JsonNode.Parse(result.Data!.ToString()!)!;
        await (json["experimental"]?["clash_api"] == null).Should().BeTrue();
        await (json["experimental"]?["cache_file"] == null).Should().BeTrue();
        await json["inbounds"]!.AsArray().Count.Should().BeEqualTo(1);
        await json["inbounds"]![0]!["listen_port"]!.GetValue<int>().Should().BeEqualTo(28080);
        await json["outbounds"]!.AsArray().Any(o => o!["server_port"]?.GetValue<int>() == 26200).Should().BeTrue();
    }

    [Test]
    [Arguments(20808)]
    [Arguments(26000)]
    public async Task SpeedtestRefusesProductionListenerOrBackendPort(int port)
    {
        var rejected = false;
        try { ManagedGroupBuilder.PrepareSpeedtest(ManagedGroupBuilder.Prepare(Context()), port); }
        catch (InvalidOperationException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    public async Task DedicatedPortsAndRuntimeSocksDoNotMutateNativeProfiles()
    {
        var original = Context();
        var prepared = ManagedGroupBuilder.Prepare(original);
        await prepared.ManagedBackends.Count.Should().BeEqualTo(2);
        await prepared.ManagedBackends[0].Node.IndexId.Should().BeEqualTo("a");
        await prepared.ManagedBackends[0].ListenPort.Should().BeEqualTo(26000);
        await prepared.ManagedBackends[1].ListenPort.Should().BeEqualTo(26001);
        await prepared.AllProxiesMap["a"].ConfigType.Should().BeEqualTo(EConfigType.SOCKS);
        await original.AllProxiesMap["a"].ConfigType.Should().BeEqualTo(EConfigType.Juicity);
        await CoreConfigJuicityService.GetListenPort(original.AppConfig).Should().BeEqualTo(20815);
        await prepared.ManagedBackends.All(b => !b.IsTunEnabled && !b.AppConfig.Inbound[0].AllowLANConn).Should().BeTrue();
        var result = new CoreConfigSingboxService(prepared).GenerateClientConfigContent();
        await result.Success.Should().BeTrue();
        var json = JsonNode.Parse(result.Data!.ToString()!)!;
        var outbounds = json["outbounds"]!.AsArray();
        await outbounds.Any(o => o!["tag"]!.ToString() == "managed-a" && o["server_port"]!.GetValue<int>() == 26000).Should().BeTrue();
        await outbounds.Any(o => o!["type"]!.ToString() == "urltest").Should().BeTrue();
    }

    [Test]
    public async Task ReservedPortsAreExcludedAndInsufficientRangeRejects()
    {
        var context = Context();
        context.AppConfig.CoreBasicItem.ManagedBackendPortStart = 20808;
        var prepared = ManagedGroupBuilder.Prepare(context);
        await prepared.ManagedBackends.All(b => b.ListenPort >= 20818).Should().BeTrue();
        context.AppConfig.CoreBasicItem.ManagedBackendPortCount = 1;
        var rejected = false;
        try { ManagedGroupBuilder.Prepare(context); } catch (InvalidOperationException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    public async Task BackendInsideAnyDialThroughChainIsRejected()
    {
        var context = Context();
        context.AllProxiesMap["chain"] = CoreConfigTestFactory.CreateProxyChainNode(ECoreType.sing_box, "chain", "chain", ["a"]);
        var rejected = false;
        try { ManagedGroupBuilder.Prepare(context); } catch (InvalidOperationException) { rejected = true; }
        await rejected.Should().BeTrue();
    }

    [Test]
    public async Task NestedSelectorsReferenceGroupsRatherThanFlatteningLeaves()
    {
        var context = Context();
        var nested = CoreConfigTestFactory.CreatePolicyGroupNode(ECoreType.sing_box, "nested", "nested", ["a"]);
        context.AllProxiesMap["nested"] = nested;
        context.Node.SetProtocolExtra(context.Node.GetProtocolExtra() with { ChildItems = "nested,b" });
        var result = new CoreConfigSingboxService(ManagedGroupBuilder.Prepare(context)).GenerateClientConfigContent();
        await result.Success.Should().BeTrue();
        var outbounds = JsonNode.Parse(result.Data!.ToString()!)!["outbounds"]!.AsArray();
        var selector = outbounds.Single(o => o!["tag"]!.ToString() == Global.ProxyTag)!;
        await selector["outbounds"]!.AsArray().Any(o => o!.ToString() == "group-nested").Should().BeTrue();
        await outbounds.Select(o => o!["tag"]!.ToString()).Distinct().Count().Should().BeEqualTo(outbounds.Count);
    }
}
