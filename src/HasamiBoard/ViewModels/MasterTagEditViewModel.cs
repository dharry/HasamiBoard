using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HasamiBoard.ViewModels;

/// <summary>設定パネルのマスタータグ管理行 (名前変更・カラー変更・削除)。</summary>
public partial class MasterTagEditViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _colorHex;

    [ObservableProperty]
    private bool _isColorPickerOpen;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editingName = string.Empty;

    private readonly Action<string, string> _onColorChanged;
    private readonly Action<string> _onDelete;
    private readonly Action<string, string> _onRename;

    public static readonly IReadOnlyList<string> Palette = new[]
    {
        "#E53935", "#FB8C00", "#FDD835", "#43A047", "#00ACC1",
        "#3949AB", "#8E24AA", "#D81B60", "#6D4C41", "#607D8B",
        "#546E7A", "#212121",
    };

    /// <summary>マスタータグ編集行の初期化。</summary>
    /// <param name="name">タグ名</param>
    /// <param name="colorHex">タグの表示色 (HEX形式)</param>
    /// <param name="onColorChanged">色変更時のコールバック</param>
    /// <param name="onDelete">削除時のコールバック</param>
    /// <param name="onRename">名前変更時のコールバック</param>
    public MasterTagEditViewModel(
        string name,
        string colorHex,
        Action<string, string> onColorChanged,
        Action<string> onDelete,
        Action<string, string> onRename)
    {
        _name = name;
        _colorHex = colorHex;
        _onColorChanged = onColorChanged;
        _onDelete = onDelete;
        _onRename = onRename;
    }

    /// <summary>カラーピッカーを開く。</summary>
    [RelayCommand]
    private void OpenColorPicker() => IsColorPickerOpen = true;

    /// <summary>選択した色をタグに適用する。</summary>
    [RelayCommand]
    private void SelectColor(string hex)
    {
        ColorHex = hex;
        IsColorPickerOpen = false;
        _onColorChanged(Name, hex);
    }

    /// <summary>タグを削除する。</summary>
    [RelayCommand]
    private void Delete() => _onDelete(Name);

    /// <summary>タグ名のリネーム編集を開始する。</summary>
    [RelayCommand]
    private void BeginRename()
    {
        EditingName = Name;
        IsRenaming = true;
    }

    /// <summary>
    /// リネーム編集を終了中かどうか。IsRenaming を false にすると編集用 TextBox が
    /// Collapsed になり、WPF がフォーカスを移動して LostFocus を同期的に発火させるため、
    /// CommitRename/CancelRename が自分自身を再入してしまうのを防ぐガード。
    /// </summary>
    private bool _isClosingRenameEdit;

    /// <summary>編集中のタグ名を確定する。</summary>
    [RelayCommand]
    private void CommitRename()
    {
        if (_isClosingRenameEdit)
        {
            return;
        }

        _isClosingRenameEdit = true;
        try
        {
            IsRenaming = false;
            string trimmed = EditingName.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed == Name)
            {
                return;
            }

            string oldName = Name;
            _onRename(oldName, trimmed);
        }
        finally
        {
            _isClosingRenameEdit = false;
        }
    }

    /// <summary>タグ名のリネーム編集をキャンセルする。</summary>
    [RelayCommand]
    private void CancelRename()
    {
        if (_isClosingRenameEdit)
        {
            return;
        }

        _isClosingRenameEdit = true;
        try
        {
            IsRenaming = false;
        }
        finally
        {
            _isClosingRenameEdit = false;
        }
    }
}
