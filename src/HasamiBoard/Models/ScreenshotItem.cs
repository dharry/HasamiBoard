using LiteDB;

namespace HasamiBoard.Models;

/// <summary>スクリーンショット履歴の1件分のデータを表すクラス。</summary>
public class ScreenshotItem
{
    public string Id { get; set; } = string.Empty;

    /// <summary>画像ファイルの絶対パス。</summary>
    public string ImagePath { get; set; } = string.Empty;

    [BsonField("Memo")]
    public string Note { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    [BsonField("Timestamp")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsPinned { get; set; }

    [BsonField("DominantColors")]
    public List<string> DominantColorsHex { get; set; } = new();

    /// <summary>拡張子から表示用のフォーマット文字列 (PNG/JPG/WEBP) を求める。DBには保存しない。</summary>
    [BsonIgnore]
    public string Format => System.IO.Path.GetExtension(ImagePath).TrimStart('.').ToUpperInvariant() switch
    {
        "JPEG" => "JPG",
        "" => "PNG",
        var ext => ext,
    };
}
