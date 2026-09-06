using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HasamiBoard.ViewModels;

/// <summary>設定パネル「開発ツールのモード」チェックボックス1件分の表示・状態。</summary>
public partial class ConverterModeToggleItem : ObservableObject
{
    /// <summary>対応する <see cref="ConverterMode"/> の列挙値名 (設定への保存キー)。</summary>
    public string ModeName { get; }

    /// <summary>チェックボックスに表示するローカライズ済みラベル。</summary>
    public string DisplayName { get; }

    /// <summary>このモードを開発ツールのサイドバーに表示するかどうか。</summary>
    [ObservableProperty]
    private bool _isEnabled;

    private readonly Action<string, bool> _onToggled;
    private readonly Action<ConverterModeToggleItem> _onMoveUp;
    private readonly Action<ConverterModeToggleItem> _onMoveDown;

    /// <summary>チェックボックス1件分の初期化。</summary>
    /// <param name="modeName">対応するConverterMode列挙値名</param>
    /// <param name="displayName">表示ラベル</param>
    /// <param name="isEnabled">初期状態</param>
    /// <param name="onToggled">切替時のコールバック</param>
    /// <param name="onMoveUp">並び順を1つ上へ移動するコールバック</param>
    /// <param name="onMoveDown">並び順を1つ下へ移動するコールバック</param>
    public ConverterModeToggleItem(
        string modeName,
        string displayName,
        bool isEnabled,
        Action<string, bool> onToggled,
        Action<ConverterModeToggleItem> onMoveUp,
        Action<ConverterModeToggleItem> onMoveDown)
    {
        ModeName = modeName;
        DisplayName = displayName;
        _isEnabled = isEnabled;
        _onToggled = onToggled;
        _onMoveUp = onMoveUp;
        _onMoveDown = onMoveDown;
    }

    /// <summary>有効/無効切替時にコールバックへ通知する。</summary>
    partial void OnIsEnabledChanged(bool value) => _onToggled(ModeName, value);

    /// <summary>並び順を1つ上へ移動する。</summary>
    [RelayCommand]
    private void MoveUp() => _onMoveUp(this);

    /// <summary>並び順を1つ下へ移動する。</summary>
    [RelayCommand]
    private void MoveDown() => _onMoveDown(this);
}
