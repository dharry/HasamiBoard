using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class ImageStorageServiceTests : IDisposable
{
    private readonly string _dataFolder;

    public ImageStorageServiceTests()
    {
        _dataFolder = Path.Combine(Path.GetTempPath(), "HasamiBoardTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_dataFolder);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dataFolder, recursive: true);
        }
        catch
        {
            // ベストエフォート
        }
    }

    private static BitmapSource CreateSolidColorBitmap(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private SettingsService CreateSettings(string format)
    {
        var settings = new SettingsService(_dataFolder);
        settings.Current.ScreenshotImageFormat = format;
        settings.Current.ScreenshotJpegQuality = 90;
        settings.Current.ScreenshotWebpQuality = 85;
        return settings;
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("webp")]
    public void SaveScreenshot_WritesValidFileWithExpectedExtension(string format)
    {
        var service = new ImageStorageService(CreateSettings(format));
        var bitmap = CreateSolidColorBitmap(10, 10, 255, 0, 0);

        string path = service.SaveScreenshot(bitmap, new DateTime(2026, 1, 2, 3, 4, 5));

        Assert.True(File.Exists(path));
        Assert.EndsWith("." + format, path);
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void SaveScreenshot_OrganizesFilesByYearMonthDay()
    {
        var service = new ImageStorageService(CreateSettings("png"));
        var bitmap = CreateSolidColorBitmap(4, 4, 0, 255, 0);
        var timestamp = new DateTime(2026, 3, 15, 9, 0, 0);

        string path = service.SaveScreenshot(bitmap, timestamp);

        Assert.Contains(Path.Combine("2026", "03", "15"), path);
    }

    [Fact]
    public void ExtractDominantColors_FindsSolidColorAsTopResult()
    {
        var service = new ImageStorageService(CreateSettings("png"));
        var bitmap = CreateSolidColorBitmap(40, 40, 255, 0, 0);
        string path = service.SaveScreenshot(bitmap, DateTime.Now);

        var colors = service.ExtractDominantColors(path);

        Assert.NotEmpty(colors);
        Assert.StartsWith("#F", colors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractDominantColors_NonExistentFile_ReturnsEmptyListInsteadOfThrowing()
    {
        var service = new ImageStorageService(CreateSettings("png"));
        var colors = service.ExtractDominantColors(Path.Combine(_dataFolder, "does-not-exist.png"));
        Assert.Empty(colors);
    }

    [Fact]
    public void DeleteImageFile_RemovesFile()
    {
        var service = new ImageStorageService(CreateSettings("png"));
        var bitmap = CreateSolidColorBitmap(4, 4, 1, 2, 3);
        string path = service.SaveScreenshot(bitmap, DateTime.Now);
        Assert.True(File.Exists(path));

        service.DeleteImageFile(path);

        Assert.False(File.Exists(path));
    }
}
