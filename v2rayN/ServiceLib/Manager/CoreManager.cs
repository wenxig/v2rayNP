namespace ServiceLib.Manager;

/// <summary>
/// Core process processing class
/// </summary>
public class CoreManager
{
    private static readonly Lazy<CoreManager> _instance = new(() => new());
    public static CoreManager Instance => _instance.Value;
    private Config _config;

    [SupportedOSPlatform("windows")]
    private WindowsJobService? _processJob;

    private readonly CoreLifecycleService _lifecycle;
    private bool _proxyApplied;
    private readonly SemaphoreSlim _managedSpeedtestGate = new(1, 1);

    public CoreManager()
    {
        _lifecycle = new CoreLifecycleService(async (core, failure) =>
        {
            AppManager.Instance.RunningCoreType = core;
            if (core == null && _proxyApplied)
            {
                await SysProxyHandler.UpdateSysProxy(_config, true);
                _proxyApplied = false;
            }
            if (failure != null) await UpdateFunc(true, failure.Message);
        });
    }

    public bool IsRunning => _lifecycle.IsRunning;
    public void CancelPendingLoad() => _lifecycle.CancelPending();

    public Task<bool> ApplySystemProxyAsync() => _lifecycle.WhenRunningAsync(async () =>
    {
        var applied = await SysProxyHandler.UpdateSysProxy(_config, false);
        _proxyApplied = applied && _config.SystemProxyItem.SysProxyType is ESysProxyType.ForcedChange or ESysProxyType.Pac;
    });
    private Func<bool, string, Task>? _updateFunc;
    private const string _tag = "CoreHandler";

    public async Task Init(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        // Keep core binaries in the active storage location when startup mode changes.
        var baseBinPath = Utils.GetBaseDirectory("bin");
        var activeBinPath = Utils.GetBinPath("");
        var localAppDataBinPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "v2rayN", "bin");
        var inactiveBinPath = string.Equals(activeBinPath, baseBinPath, StringComparison.Ordinal)
            ? localAppDataBinPath
            : baseBinPath;
        if (!string.Equals(activeBinPath, inactiveBinPath, StringComparison.Ordinal))
        {
            FileUtils.CopyNewerFiles(inactiveBinPath, activeBinPath);
        }

        if (Utils.IsNonWindows())
        {
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo();
            foreach (var it in coreInfo)
            {
                if (it.CoreType == ECoreType.v2rayN)
                {
                    if (Utils.UpgradeAppExists(out var upgradeFileName))
                    {
                        await Utils.SetLinuxChmod(upgradeFileName);
                    }
                    continue;
                }

                foreach (var name in it.CoreExes)
                {
                    var exe = Utils.GetBinPath(Utils.GetExeName(name), it.CoreType.ToString());
                    if (File.Exists(exe))
                    {
                        await Utils.SetLinuxChmod(exe);
                    }
                }
            }
        }
    }

    public async Task<CoreStartResult> LoadCore(CoreConfigContext? mainContext, CoreConfigContext? preContext, CancellationToken cancellationToken = default)
    {
        var result = await _lifecycle.LoadAsync(token => PrepareLaunchAsync(mainContext, preContext, token), cancellationToken);
        if (!result.Success) await UpdateFunc(true, result.Message);
        else await UpdateFunc(true, mainContext!.Node.GetSummary());
        return result;
    }

    private async Task<CoreLaunchPlan> PrepareLaunchAsync(CoreConfigContext? mainContext, CoreConfigContext? preContext, CancellationToken token)
    {
        if (mainContext == null) throw new CoreLaunchException(new(CoreStartFailure.Configuration, "Configuration"));
        var files = new List<string>();
        try
        {
            var steps = new List<CoreLaunchStep>();
            foreach (var context in mainContext.ManagedBackends.Concat(new[] { mainContext, preContext }.OfType<CoreConfigContext>()))
            {
                token.ThrowIfCancellationRequested();
                var stage = context.ListenPort != null || ReferenceEquals(context, mainContext) && preContext != null ? "Backend" : "Frontend";
                var info = CoreInfoManager.Instance.GetCoreInfo(context.RunCoreType);
                if (CoreInfoManager.Instance.GetCoreExecFile(info, out var missingCoreMessage).IsNullOrEmpty())
                    throw new CoreLaunchException(new(CoreStartFailure.MissingCore, stage, context.RunCoreType, Guidance: missingCoreMessage));
                var file = $"launch-{Utils.GetGuid(false)}.json";
                files.Add(Utils.GetBinConfigPath(file));
                var generated = await CoreConfigHandler.GenerateClientConfig(context, files[^1]);
                if (!generated.Success) throw new CoreLaunchException(new(CoreStartFailure.Configuration, "Configuration", context.RunCoreType));
                int? port = context.ListenPort ?? (ReferenceEquals(context, mainContext) && preContext != null ? preContext.Node.Port
                    : context.Node.ConfigType != EConfigType.Custom && !(context.IsTunEnabled && context.Node.Address == Global.Loopback && context.Node.Port == context.AppConfig.Inbound.First().LocalPort)
                        ? context.AppConfig.Inbound.First().LocalPort : null);
                steps.Add(new(context.RunCoreType, stage, port, async ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    if (Utils.IsWindows() && context.IsTunEnabled) await WindowsUtils.RemoveTunDevice();
                    var process = await RunProcess(info, file, context.Node.ConfigType != EConfigType.Custom || context.Node.DisplayLog, true, context.IsTunEnabled);
                    if (process == null) throw new CoreLaunchException(new(CoreStartFailure.Launch, stage, context.RunCoreType));
                    return ShouldRunAsSudo(context.IsTunEnabled, context.RunCoreType, Utils.IsNonWindows())
                        ? new ElevatedCoreProcess(process) : process;
                }, OptionalAfterReady: mainContext.ManagedBackends.Contains(context)));
            }
            return new CoreLaunchPlan(steps, () =>
            {
                foreach (var path in files) File.Delete(path);
                return Task.CompletedTask;
            });
        }
        catch
        {
            foreach (var path in files) File.Delete(path);
            throw;
        }
    }

    public async Task<ICoreProcess?> LoadCoreConfigSpeedtest(List<ServerTestItem> selecteds, CancellationToken cancellationToken = default)
    {
        if (selecteds.Count == 1 && (selecteds[0].ConfigType.IsGroupType() || selecteds[0].CoreType is ECoreType.mihomo or ECoreType.juicity or ECoreType.shadowquic)) return await LoadCoreConfigSpeedtest(selecteds[0], cancellationToken);
        if (selecteds.Any(x => x.CoreType is ECoreType.mihomo or ECoreType.juicity or ECoreType.shadowquic)) return null;
        var coreType = selecteds.FirstOrDefault()?.CoreType == ECoreType.sing_box ? ECoreType.sing_box : ECoreType.Xray;
        var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
        var configPath = Utils.GetBinConfigPath(fileName);
        var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, configPath, selecteds, coreType);
        await UpdateFunc(false, result.Msg);
        if (result.Success != true)
        {
            return null;
        }

        await UpdateFunc(false, string.Format(ResUI.StartService, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")));
        await UpdateFunc(false, configPath);

        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
        return await RunProcess(coreInfo, fileName, true, false);
    }

    public async Task<ICoreProcess?> LoadCoreConfigSpeedtest(ServerTestItem testItem, CancellationToken cancellationToken = default)
    {
        var node = await AppManager.Instance.GetProfileItem(testItem.IndexId);
        if (node is null)
        {
            return null;
        }

        var fileName = string.Format(Global.CoreSpeedtestConfigFileName, Utils.GetGuid(false));
        var configPath = Utils.GetBinConfigPath(fileName);
        var testConfig = JsonUtils.DeepCopy(_config);
        testConfig.TunModeItem.EnableTun = false;
        var build = await CoreConfigContextBuilder.Build(testConfig, node);
        if (!build.Success) { await UpdateFunc(false, string.Join(Environment.NewLine, build.ValidatorResult.Errors)); return null; }
        var context = build.Context;
        if (context.ManagedBackends.Count > 0)
            return await LoadManagedSpeedtestAsync(context, testItem, cancellationToken);
        var result = await CoreConfigHandler.GenerateClientSpeedtestConfig(_config, context, testItem, configPath);
        if (result.Success != true)
        {
            return null;
        }

        var coreType = context.RunCoreType;
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(coreType);
        return await RunProcess(coreInfo, fileName, true, false);
    }

    private async Task<ICoreProcess?> LoadManagedSpeedtestAsync(CoreConfigContext context, ServerTestItem testItem, CancellationToken cancellationToken)
    {
        testItem.AllowTest = false;
        // Serialize managed tests so their backend range is never shared by concurrent tests.
        await _managedSpeedtestGate.WaitAsync(cancellationToken);
        var leaseTransferred = false;
        var lifecycle = new CoreLifecycleService((_, failure) => failure == null ? Task.CompletedTask : UpdateFunc(false, failure.Message));
        try
        {
            var port = Utils.GetFreePort(AppManager.Instance.GetLocalPort(EInboundProtocol.speedtest) + testItem.QueueNum);
            var testContext = ManagedGroupBuilder.PrepareSpeedtest(context, port);
            var result = await lifecycle.LoadAsync(token => PrepareLaunchAsync(testContext, null, token), cancellationToken);
            if (!result.Success)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return null;
            }
            testItem.Port = port;
            testItem.AllowTest = true;
            leaseTransferred = true;
            return new SpeedtestCoreGroup(lifecycle, _managedSpeedtestGate);
        }
        finally
        {
            if (!leaseTransferred)
            {
                await lifecycle.StopAsync();
                _managedSpeedtestGate.Release();
            }
        }
    }

    private sealed class SpeedtestCoreGroup(CoreLifecycleService lifecycle, SemaphoreSlim lease) : ICoreProcess
    {
        private int _stopped;
        public bool HasExited => !lifecycle.IsRunning;
        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
            try { await lifecycle.StopAsync(); }
            finally { lease.Release(); }
        }
        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            while (lifecycle.IsRunning) await Task.Delay(50, cancellationToken);
        }
        public void Dispose() { }
    }

    public Task CoreStop() => _lifecycle.StopAsync();

    private Task UpdateFunc(bool notify, string msg) => _updateFunc?.Invoke(notify, msg) ?? Task.CompletedTask;

    private sealed class ElevatedCoreProcess(ProcessService process) : ICoreProcess
    {
        public bool HasExited => process.HasExited;
        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
        public async Task StopAsync()
        {
            await CoreAdminManager.Instance.KillProcessAsLinuxSudo();
            await process.StopAsync();
        }
        public void Dispose() => process.Dispose();
    }

    #region Process

    /// <summary>
    ///     Decides whether a core launch must be elevated on non-Windows platforms.
    ///     The TUN state comes from the immutable <see cref="CoreConfigContext" /> snapshot that
    ///     generated the config, never from the live mutable config: the generated config and the
    ///     launch mode must always agree, even if TUN is toggled while a reload is in flight.
    /// </summary>
    public static bool ShouldRunAsSudo(bool isTunLaunch, ECoreType? coreType, bool isNonWindows)
    {
        return isTunLaunch
            && coreType is ECoreType.sing_box or ECoreType.mihomo or ECoreType.Xray
            && isNonWindows;
    }

    private async Task<ProcessService?> RunProcess(CoreInfo? coreInfo, string configPath, bool displayLog, bool mayNeedSudo, bool isTunLaunch = false)
    {
        var fileName = CoreInfoManager.Instance.GetCoreExecFile(coreInfo, out var msg);
        if (fileName.IsNullOrEmpty())
        {
            await UpdateFunc(false, msg);
            return null;
        }

        try
        {
            if (mayNeedSudo
                && ShouldRunAsSudo(isTunLaunch, coreInfo.CoreType, Utils.IsNonWindows()))
            {
                await CoreAdminManager.Instance.Init(_config, _updateFunc);
                return await CoreAdminManager.Instance.RunProcessAsLinuxSudo(fileName, coreInfo, configPath);
            }

            return await RunProcessNormal(fileName, coreInfo, configPath, displayLog);
        }
        catch (CoreLaunchException) { throw; }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await UpdateFunc(mayNeedSudo, ResUI.FailedToRunCore);
            return null;
        }
    }

    private async Task<ProcessService?> RunProcessNormal(string fileName, CoreInfo? coreInfo, string configPath, bool displayLog)
    {
        var environmentVars = new Dictionary<string, string>();
        foreach (var kv in coreInfo.Environment)
        {
            environmentVars[kv.Key] = string.Format(kv.Value, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath);
        }

        var procService = new ProcessService(
            fileName: fileName,
            arguments: string.Format(coreInfo.Arguments, coreInfo.AbsolutePath ? Utils.GetBinConfigPath(configPath).AppendQuotes() : configPath),
            workingDirectory: Utils.GetBinConfigPath(),
            displayLog: displayLog,
            redirectInput: false,
            environmentVars: environmentVars,
            updateFunc: _updateFunc
        );

        try
        {
            await procService.StartAsync();

            await Task.Delay(100);
            if (procService.HasExited) throw new CoreLaunchException(new(CoreStartFailure.ProcessExited, "Backend", coreInfo.CoreType));
            AddProcessJob(procService.Handle);

            return procService;
        }
        catch
        {
            procService.Dispose();
            throw;
        }
    }

    private void AddProcessJob(nint processHandle)
    {
        if (Utils.IsWindows())
        {
            _processJob ??= new();
            try
            {
                _processJob?.AddProcess(processHandle);
            }
            catch { }
        }
    }

    #endregion Process
}
