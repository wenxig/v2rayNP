namespace ServiceLib.Services;

/// <summary>Only clears a proxy setting that this lease changed and that is still unchanged.</summary>
public sealed class SystemProxyLease(Func<Task<string?>> read, Func<Task> clear)
{
    private string? _applied;

    public async Task<bool> ApplyAsync(Func<Task<bool>> apply)
    {
        var before = await read();
        if (!await apply()) return false;
        var after = await read();
        if (after != null && before != null && before != after) _applied = after;
        else if (after != _applied) _applied = null;
        return true;
    }

    public async Task ReleaseAsync()
    {
        var applied = _applied;
        _applied = null;
        if (applied != null && await read() == applied) await clear();
    }
}
