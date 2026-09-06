using System.IO;
using HasamiBoard.Models;
using HasamiBoard.Services;
using LiteDB;
using Xunit;

namespace HasamiBoard.Tests;

/// <summary>
/// 実際に配布されていた旧アプリの LiteDB ファイルには、GUID文字列の _id や
/// "Timestamp" というフィールド名など、このリポジトリの当初モデルとは異なるスキーマが
/// 使われていた。この差異により起動時に InvalidCastException が発生し、アプリが
/// 一切起動できない不具合が発生した (Id: int を想定していたが実データは string GUID だった)。
/// 二度と同じ理由で起動不能にならないよう、実データと同じ形の BSON を直接書き込んで
/// リポジトリ層がそれを問題なく読み込めることを検証する。
/// </summary>
public class LegacySchemaCompatibilityTests : IDisposable
{
    private readonly string _dataFolder;

    public LegacySchemaCompatibilityTests()
    {
        _dataFolder = Path.Combine(Path.GetTempPath(), "HasamiBoardTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_dataFolder);
        LiteDbBootstrap.Configure();
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

    [Fact]
    public void ClipboardHistoryRepository_ReadsLegacyGuidIdSchema()
    {
        string dbPath = Path.Combine(_dataFolder, "ClipboardHistory.db");
        using (var db = new LiteDatabase(dbPath))
        {
            var col = db.GetCollection("clipboard_items");
            col.Insert(new BsonDocument
            {
                ["_id"] = Guid.NewGuid().ToString(),
                ["Text"] = "レガシーデータのテスト",
                ["Tags"] = new BsonArray(new[] { new BsonValue("重要") }),
                ["HasTags"] = true,
                ["Timestamp"] = new BsonValue(new DateTime(2026, 1, 2, 3, 4, 5)),
                ["IsPinned"] = false,
            });
        }

        using var repository = new ClipboardHistoryRepository(_dataFolder);
        var items = repository.GetAll();

        var item = Assert.Single(items);
        Assert.False(string.IsNullOrEmpty(item.Id));
        Assert.Equal("レガシーデータのテスト", item.Text);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), item.CreatedAt);
        Assert.Contains("重要", item.Tags);
    }

    [Fact]
    public void ScreenshotHistoryRepository_ReadsLegacyAbsolutePathSchema()
    {
        string dbPath = Path.Combine(_dataFolder, "ScreenshotHistory.db");
        string imagePath = @"C:\Users\example\AppData\Local\HasamiBoard\images\screenshot\2026\01\02\120000_abc.webp";

        using (var db = new LiteDatabase(dbPath))
        {
            var col = db.GetCollection("screenshot_items");
            col.Insert(new BsonDocument
            {
                ["_id"] = Guid.NewGuid().ToString(),
                ["DominantColors"] = new BsonArray(new[] { new BsonValue("#FF0000") }),
                ["HasDominantColors"] = true,
                ["Tags"] = new BsonArray(),
                ["HasTags"] = false,
                ["Memo"] = "OCR結果テキスト",
                ["ImagePath"] = imagePath,
                ["Timestamp"] = new BsonValue(new DateTime(2026, 1, 2, 12, 0, 0)),
                ["IsPinned"] = true,
            });
        }

        using var repository = new ScreenshotHistoryRepository(_dataFolder);
        var items = repository.GetAll();

        var item = Assert.Single(items);
        Assert.Equal(imagePath, item.ImagePath);
        Assert.Equal("OCR結果テキスト", item.Note);
        Assert.Equal("WEBP", item.Format);
        Assert.True(item.IsPinned);
        Assert.Contains("#FF0000", item.DominantColorsHex);
    }

    [Theory]
    [InlineData("テキスト", MemoMode.Text)]
    [InlineData("マークダウン", MemoMode.Markdown)]
    [InlineData("Text", MemoMode.Text)]
    [InlineData("Markdown", MemoMode.Markdown)]
    public void MemoRepository_ReadsLegacyLocalizedModeStrings(string legacyModeValue, MemoMode expected)
    {
        string dbPath = Path.Combine(_dataFolder, "Memos.db");
        using (var db = new LiteDatabase(dbPath))
        {
            var col = db.GetCollection("memo_items");
            col.Insert(new BsonDocument
            {
                ["_id"] = Guid.NewGuid().ToString(),
                ["Tags"] = new BsonArray(),
                ["Title"] = "テストメモ",
                ["Mode"] = legacyModeValue,
                ["Content"] = "本文テスト",
                ["Timestamp"] = new BsonValue(new DateTime(2026, 1, 1, 0, 0, 0)),
                ["IsPinned"] = false,
            });
        }

        using var repository = new MemoRepository(_dataFolder);
        var items = repository.GetAll();

        var item = Assert.Single(items);
        Assert.Equal(expected, item.Mode);
        Assert.Equal("本文テスト", item.Body);
    }

    [Fact]
    public void ClipboardHistoryRepository_RoundTripsNewlyInsertedItem()
    {
        using var repository = new ClipboardHistoryRepository(_dataFolder);
        var item = new ClipboardItem { Text = "新規追加テスト" };

        repository.Insert(item);

        Assert.False(string.IsNullOrEmpty(item.Id));
        var reloaded = Assert.Single(repository.GetAll());
        Assert.Equal(item.Id, reloaded.Id);
        Assert.Equal("新規追加テスト", reloaded.Text);
    }
}
