using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;

namespace HasamiBoard.ViewModels;

/// <summary>クリップボード履歴1件分の状態と操作を保持するビューモデル。</summary>
public partial class ClipboardHistoryItemViewModel : ObservableObject
{
    public ClipboardItem Model { get; }

    /// <summary>表示用の本文テキスト。</summary>
    [ObservableProperty]
    private string _text;

    /// <summary>インライン編集中の入力テキスト。</summary>
    [ObservableProperty]
    private string _editingText = string.Empty;

    /// <summary>インライン編集モード中かどうか。</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>ピン留めされているかどうか。</summary>
    [ObservableProperty]
    private bool _isPinned;

    /// <summary>コピー時のフラッシュ演出中かどうか。</summary>
    [ObservableProperty]
    private bool _isFlashing;

    /// <summary>タグ選択ピッカーが開いているかどうか。</summary>
    [ObservableProperty]
    private bool _isTagPickerOpen;

    public DateTime CreatedAt => Model.CreatedAt;

    public ObservableCollection<TagBadgeViewModel> Tags { get; } = new();

    public string SearchableText => Text;

    public List<MasterTag> AvailableTagsToAdd
        => _owner.MasterTags.Where(t => !Model.Tags.Contains(t.Name)).ToList();

    public bool HasNoAvailableTags => AvailableTagsToAdd.Count == 0;

    private readonly ClipboardHistoryViewModel _owner;

    /// <summary>モデルとオーナーを受け取り初期状態を構築する。</summary>
    /// <param name="model">クリップボード履歴アイテムのモデル</param>
    /// <param name="owner">親のビューモデル</param>
    public ClipboardHistoryItemViewModel(ClipboardItem model, ClipboardHistoryViewModel owner)
    {
        Model = model;
        _owner = owner;
        _text = model.Text;
        _isPinned = model.IsPinned;
        RebuildTags();
    }

    /// <summary>タグバッジ一覧をモデルの内容から再構築する。</summary>
    /// <returns>なし</returns>
    public void RebuildTags()
    {
        Tags.Clear();
        foreach (string tagName in Model.Tags)
        {
            var master = _owner.FindMasterTag(tagName);
            Tags.Add(new TagBadgeViewModel(
                tagName,
                master?.ColorHex ?? "#607D8B",
                onClick: _owner.OnTagBadgeClicked,
                onRemove: name => _owner.RemoveTagFromItem(this, name)));
        }

        OnPropertyChanged(nameof(AvailableTagsToAdd));
        OnPropertyChanged(nameof(HasNoAvailableTags));
    }

    /// <summary>インライン編集を開始する。</summary>
    [RelayCommand]
    private void BeginEdit()
    {
        EditingText = Text;
        IsEditing = true;
    }

    /// <summary>編集内容を確定して保存する。</summary>
    [RelayCommand]
    private void CommitEdit()
    {
        Text = EditingText;
        IsEditing = false;
        _owner.CommitItemEdit(this);
    }

    /// <summary>編集をキャンセルする。</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    /// <summary>ピン留め状態を切り替える。</summary>
    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
        _owner.TogglePin(this);
    }

    /// <summary>このアイテムを削除する。</summary>
    [RelayCommand]
    private void Delete() => _owner.DeleteItem(this);

    /// <summary>本文をOSクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopyToClipboard() => _owner.CopyItemToClipboard(this);

    /// <summary>このアイテムからメモを新規作成する。</summary>
    [RelayCommand]
    private void CreateMemo() => _owner.CreateMemo(this);

    /// <summary>タグ選択ピッカーを開く。</summary>
    [RelayCommand]
    private void OpenTagPicker() => IsTagPickerOpen = true;

    /// <summary>指定したタグをこのアイテムに付与する。</summary>
    [RelayCommand]
    private void AddTag(string tagName) => _owner.AddTagToItem(this, tagName);
}
