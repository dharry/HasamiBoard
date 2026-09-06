namespace HasamiBoard.Models;

/// <summary>メモ作成時に適用できる定型文テンプレートを表すクラス。</summary>
public class MemoTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MemoMode Mode { get; set; } = MemoMode.Text;
    public string Body { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
