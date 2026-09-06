using System.IO;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _dataFolder;

    public SettingsServiceTests()
    {
        _dataFolder = Path.Combine(Path.GetTempPath(), "HasamiBoardTests_" + Guid.NewGuid());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataFolder))
            {
                Directory.Delete(_dataFolder, recursive: true);
            }
        }
        catch
        {
            // ベストエフォート
        }
    }

    [Fact]
    public void FirstRun_WithNoExistingFile_UsesDefaults()
    {
        var settings = new SettingsService(_dataFolder);

        Assert.Equal("auto", settings.Current.Language);
        Assert.Equal(100, settings.Current.MaxHistoryCount);
        Assert.False(settings.Current.IsDarkMode);
        Assert.NotEmpty(settings.Current.MasterTags);
    }

    [Fact]
    public void Save_ThenReload_RoundTripsChangedValues()
    {
        var settings = new SettingsService(_dataFolder);
        settings.Current.IsDarkMode = true;
        settings.Current.Language = "ja";
        settings.Current.MaxHistoryCount = 250;
        settings.Save();

        var reloaded = new SettingsService(_dataFolder);

        Assert.True(reloaded.Current.IsDarkMode);
        Assert.Equal("ja", reloaded.Current.Language);
        Assert.Equal(250, reloaded.Current.MaxHistoryCount);
    }

    [Fact]
    public void CorruptSettingsFile_FallsBackToDefaults_WithoutThrowing()
    {
        Directory.CreateDirectory(_dataFolder);
        File.WriteAllText(Path.Combine(_dataFolder, "Settings.json"), "{ this is not valid json");

        var settings = new SettingsService(_dataFolder);

        Assert.Equal("auto", settings.Current.Language);
    }

    [Fact]
    public void Load_MigratesLegacyGuidConverterModeNameToUuid()
    {
        Directory.CreateDirectory(_dataFolder);
        File.WriteAllText(Path.Combine(_dataFolder, "Settings.json"), """
            {
              "ConverterSelectedMode": "Guid",
              "ConverterDisabledModes": ["Guid", "Password"],
              "ConverterModeOrder": ["Hash", "Guid", "Base64"]
            }
            """);

        var settings = new SettingsService(_dataFolder);

        Assert.Equal("Uuid", settings.Current.ConverterSelectedMode);
        Assert.Equal(new[] { "Uuid", "Password" }, settings.Current.ConverterDisabledModes);
        Assert.Equal(new[] { "Hash", "Uuid", "Base64" }, settings.Current.ConverterModeOrder);
    }

    [Fact]
    public void Load_MigratingLegacyGuidName_DoesNotDuplicateWhenUuidAlreadyPresent()
    {
        Directory.CreateDirectory(_dataFolder);
        File.WriteAllText(Path.Combine(_dataFolder, "Settings.json"), """
            {
              "ConverterDisabledModes": ["Guid", "Uuid", "Password"]
            }
            """);

        var settings = new SettingsService(_dataFolder);

        Assert.Equal(new[] { "Uuid", "Password" }, settings.Current.ConverterDisabledModes);
    }

    [Fact]
    public void ResetToDefault_RestoresDefaultsAndPersists()
    {
        var settings = new SettingsService(_dataFolder);
        settings.Current.IsDarkMode = true;
        settings.Save();

        settings.ResetToDefault();

        Assert.False(settings.Current.IsDarkMode);

        var reloaded = new SettingsService(_dataFolder);
        Assert.False(reloaded.Current.IsDarkMode);
    }
}
