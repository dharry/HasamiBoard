using System.Collections.ObjectModel;

namespace HasamiBoard.ViewModels;

/// <summary>設定パネル「開発ツールのモード」一覧における、1カテゴリ分の見出しと配下モードの一覧。
/// 並び替え (▲▼) はこのグループの<see cref="Items"/>内に閉じて行われる。</summary>
public class ConverterModeCategoryGroup
{
    /// <summary>カテゴリのローカライズ済み見出し。</summary>
    public string HeaderText { get; }

    /// <summary>このカテゴリに属するモードのチェックボックス一覧。</summary>
    public ObservableCollection<ConverterModeToggleItem> Items { get; } = new();

    /// <summary>カテゴリ見出しを指定して初期化する。</summary>
    /// <param name="headerText">ローカライズ済みのカテゴリ見出し</param>
    public ConverterModeCategoryGroup(string headerText)
    {
        HeaderText = headerText;
    }
}
