using System.IO;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _sourceFolder;
    private readonly string _restoreFolder;
    private readonly string _zipPath;

    public BackupServiceTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "HasamiBoardTests_Backup_" + Guid.NewGuid());
        _sourceFolder = Path.Combine(root, "source");
        _restoreFolder = Path.Combine(root, "restore");
        _zipPath = Path.Combine(root, "backup.zip");
        Directory.CreateDirectory(_sourceFolder);
        Directory.CreateDirectory(_restoreFolder);
    }

    public void Dispose()
    {
        try
        {
            string root = Path.GetDirectoryName(_sourceFolder)!;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // ベストエフォート
        }
    }

    [Fact]
    public void CreateBackup_IncludesDatabaseFilesAndImages()
    {
        File.WriteAllText(Path.Combine(_sourceFolder, "Settings.json"), "{\"Language\":\"ja\"}");
        File.WriteAllText(Path.Combine(_sourceFolder, "ClipboardHistory.db"), "dummy-db-content");
        string imagesSubDir = Path.Combine(_sourceFolder, "images", "screenshot", "2026", "01", "01");
        Directory.CreateDirectory(imagesSubDir);
        File.WriteAllText(Path.Combine(imagesSubDir, "120000_abc.png"), "fake-image-bytes");

        var service = new BackupService(_sourceFolder);
        service.CreateBackup(_zipPath);

        Assert.True(File.Exists(_zipPath));

        using var zip = System.IO.Compression.ZipFile.OpenRead(_zipPath);
        Assert.Contains(zip.Entries, e => e.FullName == "Settings.json");
        Assert.Contains(zip.Entries, e => e.FullName == "ClipboardHistory.db");
        Assert.Contains(zip.Entries, e => e.FullName == "images/screenshot/2026/01/01/120000_abc.png");
    }

    [Theory]
    [InlineData("Settings.json")]
    [InlineData("ClipboardHistory.db")]
    [InlineData("ScreenshotHistory.db")]
    [InlineData("Memos.db")]
    [InlineData("MemoTemplates.db")]
    public void CreateBackup_IncludesEveryKnownDatabaseFile(string fileName)
    {
        File.WriteAllText(Path.Combine(_sourceFolder, fileName), "dummy-content");

        var service = new BackupService(_sourceFolder);
        service.CreateBackup(_zipPath);

        using var zip = System.IO.Compression.ZipFile.OpenRead(_zipPath);
        Assert.Contains(zip.Entries, e => e.FullName == fileName);
    }

    [Fact]
    public void RestoreBackup_ExtractsFilesAndImagesToDestinationFolder()
    {
        File.WriteAllText(Path.Combine(_sourceFolder, "Settings.json"), "{\"Language\":\"ja\"}");
        string imagesSubDir = Path.Combine(_sourceFolder, "images", "screenshot", "2026", "01", "01");
        Directory.CreateDirectory(imagesSubDir);
        File.WriteAllText(Path.Combine(imagesSubDir, "120000_abc.png"), "fake-image-bytes");

        new BackupService(_sourceFolder).CreateBackup(_zipPath);

        new BackupService(_restoreFolder).RestoreBackup(_zipPath);

        Assert.Equal("{\"Language\":\"ja\"}", File.ReadAllText(Path.Combine(_restoreFolder, "Settings.json")));
        Assert.Equal(
            "fake-image-bytes",
            File.ReadAllText(Path.Combine(_restoreFolder, "images", "screenshot", "2026", "01", "01", "120000_abc.png")));
    }

    [Fact]
    public void RestoreBackup_OverwritesExistingFiles()
    {
        File.WriteAllText(Path.Combine(_sourceFolder, "Settings.json"), "{\"Language\":\"ja\"}");
        new BackupService(_sourceFolder).CreateBackup(_zipPath);

        File.WriteAllText(Path.Combine(_restoreFolder, "Settings.json"), "{\"Language\":\"en\"}");

        new BackupService(_restoreFolder).RestoreBackup(_zipPath);

        Assert.Equal("{\"Language\":\"ja\"}", File.ReadAllText(Path.Combine(_restoreFolder, "Settings.json")));
    }

    [Fact]
    public void CreateBackup_WithNoDataFiles_ProducesEmptyZipWithoutThrowing()
    {
        var service = new BackupService(_sourceFolder);
        service.CreateBackup(_zipPath);

        Assert.True(File.Exists(_zipPath));
        using var zip = System.IO.Compression.ZipFile.OpenRead(_zipPath);
        Assert.Empty(zip.Entries);
    }

    [Fact]
    public void RestoreBackup_RejectsPathTraversalEntry_WithoutWritingOutsideDataFolder()
    {
        string root = Path.GetDirectoryName(_restoreFolder)!;
        string escapeTargetPath = Path.Combine(root, "evil.txt");

        using (var zip = System.IO.Compression.ZipFile.Open(_zipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("../evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("malicious-content");
        }

        Assert.Throws<InvalidDataException>(() => new BackupService(_restoreFolder).RestoreBackup(_zipPath));
        Assert.False(File.Exists(escapeTargetPath));
    }

    [Fact]
    public void RestoreBackup_RejectsRootedEntry_WithoutWritingOutsideDataFolder()
    {
        string root = Path.GetDirectoryName(_restoreFolder)!;
        string escapeTargetPath = Path.Combine(root, "rooted-evil.txt");

        using (var zip = System.IO.Compression.ZipFile.Open(_zipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry(escapeTargetPath.Replace('\\', '/'));
            using var writer = new StreamWriter(entry.Open());
            writer.Write("malicious-content");
        }

        Assert.Throws<InvalidDataException>(() => new BackupService(_restoreFolder).RestoreBackup(_zipPath));
        Assert.False(File.Exists(escapeTargetPath));
    }
}
