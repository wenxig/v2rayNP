namespace ServiceLib.Tests.Manager;

public class CoreDownloadTests
{
    [Test]
    public async Task JuicityAssetsMatchSupportedPlatformsAndExcludeLoong64()
    {
        var info = CoreInfoManager.Instance.GetCoreInfo(ECoreType.juicity);
        foreach (var platform in new[] { OSPlatform.Windows, OSPlatform.Linux, OSPlatform.OSX })
            foreach (var arch in new[] { Architecture.X64, Architecture.Arm64 })
            {
                var url = CoreInfoManager.GetDownloadUrl(info, platform, arch);
                await (!string.IsNullOrEmpty(url) && url.Contains("/juicity-") && url.EndsWith(".zip")).Should().BeTrue();
            }
        await string.IsNullOrEmpty(CoreInfoManager.GetDownloadUrl(info, OSPlatform.Linux, Architecture.LoongArch64)).Should().BeTrue();
        await CoreInfoManager.GetDownloadUrl(info, OSPlatform.Linux, Architecture.RiscV64)!.EndsWith("juicity-linux-riscv64.zip").Should().BeTrue();
        if (RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64)
            await CoreInfoManager.Instance.GetCheckUpdateCoreTypes().Contains(ECoreType.juicity).Should().BeTrue();
    }

    [Test]
    public async Task ShadowquicAssetsCoverDesktopPlatformsButNeverInventX86Asset()
    {
        var info = CoreInfoManager.Instance.GetCoreInfo(ECoreType.shadowquic);
        foreach (var platform in new[] { OSPlatform.Windows, OSPlatform.Linux, OSPlatform.OSX })
            foreach (var arch in new[] { Architecture.X64, Architecture.Arm64 })
                await string.IsNullOrEmpty(CoreInfoManager.GetDownloadUrl(info, platform, arch)).Should().BeFalse();
        await string.IsNullOrEmpty(CoreInfoManager.GetDownloadUrl(info, OSPlatform.Windows, Architecture.X86)).Should().BeTrue();
        await string.IsNullOrEmpty(CoreInfoManager.GetDownloadUrl(info, OSPlatform.Linux, Architecture.LoongArch64)).Should().BeFalse();
    }
}
