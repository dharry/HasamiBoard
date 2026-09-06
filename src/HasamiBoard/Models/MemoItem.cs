using LiteDB;

namespace HasamiBoard.Models;

/// <summary>メモの編集モード (テキスト / マークダウン) を表す列挙型。</summary>
public enum MemoMode
{
    Text,
    Markdown,
}

/// <summary>メモの1件分のデータを表すクラス。</summary>
public class MemoItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public MemoMode Mode { get; set; } = MemoMode.Text;

    [BsonField("Content")]
    public string Body { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    [BsonField("Timestamp")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsPinned { get; set; }
}
