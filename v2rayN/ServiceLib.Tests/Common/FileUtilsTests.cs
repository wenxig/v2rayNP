namespace ServiceLib.Tests.Common;

public class FileUtilsTests
{
    [Test]
    public async Task CopyNewerFiles_CopiesMissingAndNewerFilesOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), Utils.GetGuid());
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(Path.Combine(source, "mihomo"));
        Directory.CreateDirectory(Path.Combine(destination, "mihomo"));

        try
        {
            var newerSourceFile = Path.Combine(source, "mihomo", "mihomo");
            var olderDestinationFile = Path.Combine(destination, "mihomo", "mihomo");
            await File.WriteAllTextAsync(newerSourceFile, "new core");
            await File.WriteAllTextAsync(olderDestinationFile, "old core");
            File.SetLastWriteTimeUtc(newerSourceFile, DateTime.UtcNow.AddMinutes(1));
            File.SetLastWriteTimeUtc(olderDestinationFile, DateTime.UtcNow.AddMinutes(-1));

            var newerDestinationFile = Path.Combine(destination, "xray");
            var olderSourceFile = Path.Combine(source, "xray");
            await File.WriteAllTextAsync(newerDestinationFile, "new destination");
            await File.WriteAllTextAsync(olderSourceFile, "old source");
            File.SetLastWriteTimeUtc(newerDestinationFile, DateTime.UtcNow.AddMinutes(1));
            File.SetLastWriteTimeUtc(olderSourceFile, DateTime.UtcNow.AddMinutes(-1));

            FileUtils.CopyNewerFiles(source, destination);

            await (await File.ReadAllTextAsync(olderDestinationFile)).Should().BeEqualTo("new core");
            await (await File.ReadAllTextAsync(newerDestinationFile)).Should().BeEqualTo("new destination");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
