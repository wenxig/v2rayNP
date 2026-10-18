namespace ServiceLib.Tests.Services;

public class SystemProxyLeaseTests
{
    [Test]
    [Arguments(false, false, 1)]
    [Arguments(true, false, 0)]
    [Arguments(false, true, 0)]
    public async Task ReleaseOnlyClearsUnchangedOwnedProxy(bool externallyChanged, bool readFails, int expectedClears)
    {
        string? state = "original";
        var clears = 0;
        var lease = new SystemProxyLease(() => Task.FromResult(state), () => { clears++; return Task.CompletedTask; });
        await lease.ApplyAsync(() => { state = "ours"; return Task.FromResult(true); });
        if (externallyChanged) state = "another app";
        if (readFails) state = null;
        await lease.ReleaseAsync();
        await lease.ReleaseAsync();
        await clears.Should().BeEqualTo(expectedClears);
    }

    [Test]
    public async Task ExistingIdenticalProxyIsNotClaimedAndReapplyRetainsOwnLease()
    {
        var state = "ours";
        var clears = 0;
        var lease = new SystemProxyLease(() => Task.FromResult<string?>(state), () => { clears++; return Task.CompletedTask; });
        await lease.ApplyAsync(() => Task.FromResult(true));
        await lease.ReleaseAsync();
        await clears.Should().BeEqualTo(0);
        state = "original";
        await lease.ApplyAsync(() => { state = "ours"; return Task.FromResult(true); });
        await lease.ApplyAsync(() => Task.FromResult(true));
        await lease.ReleaseAsync();
        await clears.Should().BeEqualTo(1);
    }
}
