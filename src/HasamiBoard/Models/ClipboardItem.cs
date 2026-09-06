using LiteDB;

namespace HasamiBoard.Models;

/// <summary>クリップボード履歴の1件分のデータを表すクラス。</summary>
public class ClipboardItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();

    [BsonField("Timestamp")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsPinned { get; set; }
}
