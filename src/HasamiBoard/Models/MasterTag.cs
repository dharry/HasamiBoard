namespace HasamiBoard.Models;

/// <summary>タグ管理で使用するマスタータグ (名前と色) を表すクラス。</summary>
public class MasterTag
{
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#607D8B";
}
