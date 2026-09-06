namespace HasamiBoard.ViewModels;

/// <summary>クイックフィルターの再適用を受け付けるタブ用ビューモデルの共通契約。</summary>
public interface IQuickFilterable
{
    /// <summary>指定したフィルター文字列を一覧に適用する。</summary>
    /// <param name="filterText">適用するフィルター文字列</param>
    void ApplyFilter(string filterText);
}
