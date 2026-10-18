namespace ServiceLib.Tests.Services;

public class SpeedtestServiceBatchTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1000)]
    public async Task EmptySelection_ShouldReturnNoBatches(int pageSize)
    {
        await GetBatches([], pageSize).Count.Should().BeEqualTo(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task NonPositivePageSize_ShouldKeepEveryEligibleProfile(int pageSize)
    {
        var profiles = CreateProfiles();
        var batches = GetBatches(profiles, pageSize);

        await batches.Count.Should().BeEqualTo(profiles.Count);
        await batches.SelectMany(batch => batch).Count().Should().BeEqualTo(profiles.Count);
        foreach (var profile in profiles)
        {
            await batches.SelectMany(batch => batch).Count(item => ReferenceEquals(item, profile)).Should().BeEqualTo(1);
        }
    }

    [Test]
    public async Task NormalBatches_ShouldRespectPageSizeAndKeepCoreTypesSeparate()
    {
        var profiles = CreateProfiles();
        var batches = GetBatches(profiles, 2);

        await batches.Count.Should().BeEqualTo(3);
        await batches.SelectMany(batch => batch).Count().Should().BeEqualTo(profiles.Count);
        foreach (var batch in batches)
        {
            await (batch.Count > 0 && batch.Count <= 2).Should().BeTrue();
            await batch.Select(item => item.CoreType).Distinct().Count().Should().BeEqualTo(1);
        }
    }

    [Test]
    public async Task StandaloneCores_ShouldUseOneProcessPerProfile()
    {
        var profiles = new List<ServerTestItem>
        {
            new() { CoreType = ECoreType.shadowquic }, new() { CoreType = ECoreType.shadowquic },
            new() { CoreType = ECoreType.juicity }, new() { CoreType = ECoreType.juicity },
        };
        await GetBatches(profiles, 100).All(batch => batch.Count == 1).Should().BeTrue();
        await GetBatches(profiles, 100).SelectMany(batch => batch).Count().Should().BeEqualTo(4);
    }

    private static List<ServerTestItem> CreateProfiles() =>
    [
        new() { CoreType = ECoreType.Xray },
        new() { CoreType = ECoreType.Xray },
        new() { CoreType = ECoreType.Xray },
        new() { CoreType = ECoreType.sing_box },
        new() { CoreType = ECoreType.sing_box }
    ];

    [Test]
    public async Task RealPingRetry_ShouldKeepSuccessfulNodesOutOfFallback()
    {
        var service = new RecordingSpeedtestService();
        var profiles = new List<ServerTestItem>
        {
            new() { IndexId = "success", CoreType = ECoreType.Xray },
            new() { IndexId = "failed", CoreType = ECoreType.sing_box }
        };
        await RunBatch(service, "RunRealPingBatchAsync", profiles);

        await service.Mixed.Single().Single().IndexId.Should().BeEqualTo("failed");
        await service.RealPing.SelectMany(batch => batch).Count(item => item.IndexId == "success").Should().BeEqualTo(1);
    }

    [Test]
    public async Task UdpRetry_ShouldPreserveStandaloneGroupAndCoreIsolation()
    {
        var service = new RecordingSpeedtestService();
        var profiles = new List<ServerTestItem>
        {
            new() { IndexId = "success", CoreType = ECoreType.Xray },
            new() { IndexId = "juicity-one", CoreType = ECoreType.juicity },
            new() { IndexId = "juicity-two", CoreType = ECoreType.juicity },
            new() { IndexId = "shadowquic", CoreType = ECoreType.shadowquic },
            new() { IndexId = "mihomo", CoreType = ECoreType.mihomo },
            new() { IndexId = "group", CoreType = ECoreType.sing_box, ConfigType = EConfigType.PolicyGroup },
            new() { IndexId = "native", CoreType = ECoreType.sing_box }
        };
        await RunBatch(service, "RunUdpTestBatchAsync", profiles);

        foreach (var item in profiles)
        {
            await service.Udp.SelectMany(batch => batch).Count(test => ReferenceEquals(test, item))
                .Should().BeEqualTo(item.IndexId == "success" ? 1 : 2);
        }
        await service.Udp.All(batch => batch.Count == 1).Should().BeTrue();
    }

    private static Task RunBatch(SpeedtestService service, string method, List<ServerTestItem> profiles) =>
        (Task)typeof(SpeedtestService).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, [profiles, new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(), 4, CancellationToken.None])!;

    private sealed class RecordingSpeedtestService() : SpeedtestService(new Config
    {
        SpeedTestItem = new() { SpeedTestDelayInterval = 0, MixedConcurrencyCount = 2 }
    }, _ => Task.CompletedTask)
    {
        public List<List<ServerTestItem>> RealPing { get; } = [];
        public List<List<ServerTestItem>> Udp { get; } = [];
        public List<List<ServerTestItem>> Mixed { get; } = [];

        protected override Task<bool> RunRealPingAsync(List<ServerTestItem> selecteds,
            System.Collections.Concurrent.ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
        {
            RealPing.Add([.. selecteds]);
            return Task.FromResult(selecteds.All(item => item.IndexId == "success"));
        }

        protected override Task<bool> RunUdpTestAsync(List<ServerTestItem> selecteds,
            System.Collections.Concurrent.ConcurrentDictionary<string, byte> completedIds, CancellationToken ct = default)
        {
            Udp.Add([.. selecteds]);
            return Task.FromResult(selecteds.All(item => item.IndexId == "success"));
        }

        protected override Task RunMixedTestAsync(List<ServerTestItem> selecteds,
            System.Collections.Concurrent.ConcurrentDictionary<string, byte> completedIds, int concurrencyCount,
            bool blSpeedTest, CancellationToken ct = default)
        {
            Mixed.Add([.. selecteds]);
            return Task.CompletedTask;
        }
    }

    private static List<List<ServerTestItem>> GetBatches(List<ServerTestItem> profiles, int pageSize)
    {
        var service = new SpeedtestService(new Config { SpeedTestItem = new() }, _ => Task.CompletedTask);
        var method = typeof(SpeedtestService).GetMethod("GetTestBatchItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<List<ServerTestItem>>)method.Invoke(service, [profiles, pageSize])!;
    }
}
