using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HasamiBoard.ViewModels;

/// <summary>カード上に表示するタグピルバッジ (#タグ名)。クリックでフィルター、✕で解除。</summary>
public partial class TagBadgeViewModel : ObservableObject
{
    public string Name { get; }
    public string ColorHex { get; }

    private readonly Action<string> _onClick;
    private readonly Action<string>? _onRemove;

    public bool CanRemove => _onRemove is not null;

    /// <summary>タグバッジの初期化。</summary>
    /// <param name="name">タグ名</param>
    /// <param name="colorHex">表示色 (HEX形式)</param>
    /// <param name="onClick">クリック時のコールバック</param>
    /// <param name="onRemove">削除時のコールバック (オプション)</param>
    public TagBadgeViewModel(string name, string colorHex, Action<string> onClick, Action<string>? onRemove = null)
    {
        Name = name;
        ColorHex = colorHex;
        _onClick = onClick;
        _onRemove = onRemove;
    }

    /// <summary>タグバッジクリック時のフィルター適用処理。</summary>
    [RelayCommand]
    private void Click() => _onClick(Name);

    /// <summary>タグバッジの解除処理。</summary>
    [RelayCommand]
    private void Remove() => _onRemove?.Invoke(Name);
}
