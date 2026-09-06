using System.IO;
using HasamiBoard.Models;
using HasamiBoard.Services;
using HasamiBoard.ViewModels;
using Xunit;

namespace HasamiBoard.Tests;

public class MasterTagCascadeTests : IDisposable
{
    private readonly string _dataFolder;

    public MasterTagCascadeTests()
    {
        _dataFolder = Path.Combine(Path.GetTempPath(), "HasamiBoardTests_TagCascade_" + Guid.NewGuid());
        Directory.CreateDirectory(_dataFolder);
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
    public void ClipboardHistoryViewModel_RemoveTag_RemovesTagFromAllRecords()
    {
        var settings = new SettingsService(_dataFolder);
        using var repository = new ClipboardHistoryRepository(_dataFolder);
        using var monitor = new ClipboardMonitorService();

        repository.Insert(new ClipboardItem { Text = "A", Tags = { "重要", "作業中" } });
        repository.Insert(new ClipboardItem { Text = "B", Tags = { "重要" } });
        repository.Insert(new ClipboardItem { Text = "C", Tags = { "作業中" } });

        var viewModel = new ClipboardHistoryViewModel(settings, repository, monitor);

        viewModel.RemoveTag("重要");

        Assert.All(repository.GetAll(), item => Assert.DoesNotContain("重要", item.Tags));
        Assert.Contains(repository.GetAll(), item => item.Tags.Contains("作業中"));
        Assert.All(viewModel.Items, item => Assert.DoesNotContain(item.Tags, badge => badge.Name == "重要"));
    }

    [Fact]
    public void MemoViewModel_RemoveTag_RemovesTagFromRecordsAndTemplates()
    {
        var settings = new SettingsService(_dataFolder);
        using var repository = new MemoRepository(_dataFolder);
        using var templateRepository = new MemoTemplateRepository(_dataFolder);
        using var monitor = new ClipboardMonitorService();

        repository.Insert(new MemoItem { Title = "メモA", Body = "本文A", Tags = { "重要" } });
        templateRepository.Insert(new MemoTemplate { Name = "テンプレA", Body = "定型文", Tags = { "重要", "定型" } });

        var viewModel = new MemoViewModel(settings, repository, templateRepository, monitor);

        viewModel.RemoveTag("重要");

        Assert.All(repository.GetAll(), item => Assert.DoesNotContain("重要", item.Tags));
        Assert.All(templateRepository.GetAll(), template => Assert.DoesNotContain("重要", template.Tags));
        Assert.Contains(templateRepository.GetAll(), template => template.Tags.Contains("定型"));
    }

    [Fact]
    public void ScreenshotViewModel_RemoveTag_RemovesTagFromAllRecords()
    {
        var settings = new SettingsService(_dataFolder);
        using var repository = new ScreenshotHistoryRepository(_dataFolder);
        using var monitor = new ClipboardMonitorService();
        var imageStorage = new ImageStorageService(settings);
        var ocrService = new OcrService();

        repository.Insert(new ScreenshotItem { ImagePath = "dummy.png", Tags = { "重要" } });

        var viewModel = new ScreenshotViewModel(settings, repository, imageStorage, monitor, ocrService);

        viewModel.RemoveTag("重要");

        Assert.All(repository.GetAll(), item => Assert.DoesNotContain("重要", item.Tags));
    }
}
