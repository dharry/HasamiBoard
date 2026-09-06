using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>クリップボード履歴タブ全体の一覧・検索・一括操作を管理するビューモデル。</summary>
public partial class ClipboardHistoryViewModel : ObservableObject, IQuickFilterable
{
    private readonly SettingsService _settingsService;
    private readonly ClipboardHistoryRepository _repository;
    private readonly ClipboardMonitorService _monitorService;

    private List<ClipboardHistoryItemViewModel> _allItems = new();
    private string _currentFilterText = string.Empty;

    public event Action<string>? TagBadgeClicked;
    public event Action<MemoDraft>? CreateMemoRequested;

    public ObservableCollection<ClipboardHistoryItemViewModel> Items { get; } = new();

    /// <summary>Ctrlキー等で複数選択されたレコード (一括削除・一括タグ付与の対象)。</summary>
    public ObservableCollection<ClipboardHistoryItemViewModel> SelectedItems { get; } = new();

    public bool HasSelection => SelectedItems.Count > 0;

    public string SelectionCountText => string.Format(LocalizationManager.Instance["Selection_Count"], SelectedItems.Count);

    public bool AreAllSelectedPinned => SelectedItems.Count > 0 && SelectedItems.All(i => i.IsPinned);

    public string BulkPinButtonLabel => LocalizationManager.Instance[AreAllSelectedPinned ? "Tooltip_Unpin" : "Button_Pin"];

    /// <summary>現在選択中のアイテム。</summary>
    [ObservableProperty]
    private ClipboardHistoryItemViewModel? _selectedItem;

    /// <summary>フィルター結果が0件かどうか。</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>一括タグ付与ピッカーが開いているかどうか。</summary>
    [ObservableProperty]
    private bool _isBulkTagPickerOpen;

    public VimNavigationController Vim { get; }

    /// <summary>依存サービスを受け取り、履歴の読み込みとVim操作を初期化する。</summary>
    /// <param name="settingsService">アプリ設定サービス</param>
    /// <param name="repository">クリップボード履歴リポジトリ</param>
    /// <param name="monitorService">クリップボード監視サービス</param>
    public ClipboardHistoryViewModel(
        SettingsService settingsService,
        ClipboardHistoryRepository repository,
        ClipboardMonitorService monitorService)
    {
        _settingsService = settingsService;
        _repository = repository;
        _monitorService = monitorService;

        _monitorService.TextCaptured += OnTextCaptured;

        Vim = new VimNavigationController
        {
            MoveDown = SelectNext,
            MoveUp = SelectPrevious,
            MoveToTop = () => SelectedItem = Items.FirstOrDefault(),
            MoveToBottom = () => SelectedItem = Items.LastOrDefault(),
            DeleteSelected = () =>
            {
                if (SelectedItems.Count > 1) DeleteSelectedCommand.Execute(null);
                else if (SelectedItem is not null) DeleteItem(SelectedItem);
            },
            YankSelected = () =>
            {
                if (SelectedItems.Count > 1) CopySelectedToClipboardCommand.Execute(null);
                else if (SelectedItem is not null) CopyItemToClipboard(SelectedItem);
            },
            EnterEdit = () => SelectedItem?.BeginEditCommand.Execute(null),
        };

        LoadAll();
    }

    /// <summary>データベースから全履歴を読み込み、一覧を再構築する。</summary>
    private void LoadAll()
    {
        _allItems = _repository.GetAll()
            .Select(m => new ClipboardHistoryItemViewModel(m, this))
            .ToList();
        ApplyFilter(_currentFilterText);
    }

    /// <summary>クイックフィルター文字列を全履歴に適用し、表示一覧を更新する。</summary>
    /// <param name="filterText">フィルタリング対象の検索文字列</param>
    /// <returns>なし</returns>
    public void ApplyFilter(string filterText)
    {
        _currentFilterText = filterText;
        var previouslySelectedId = SelectedItem?.Model.Id;

        Items.Clear();
        foreach (var item in _allItems)
        {
            if (QuickFilterMatcher.Matches(filterText, item.SearchableText, item.Model.Tags))
            {
                Items.Add(item);
            }
        }

        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(i => i.Model.Id == previouslySelectedId) ?? Items.FirstOrDefault();

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(MaxCount));
    }

    /// <summary>選択位置を次のアイテムへ移動する。</summary>
    private void SelectNext()
    {
        if (Items.Count == 0)
        {
            return;
        }

        int index = SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        SelectedItem = Items[Math.Min(index + 1, Items.Count - 1)];
    }

    /// <summary>選択位置を前のアイテムへ移動する。</summary>
    private void SelectPrevious()
    {
        if (Items.Count == 0)
        {
            return;
        }

        int index = SelectedItem is null ? 0 : Items.IndexOf(SelectedItem);
        SelectedItem = Items[Math.Max(index - 1, 0)];
    }

    /// <summary>クリップボード監視サービスからのテキスト検出時に履歴へ追加/更新する。</summary>
    private void OnTextCaptured(string text)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = _repository.FindByText(text);
            if (existing is not null)
            {
                existing.CreatedAt = DateTime.Now;
                _repository.Update(existing);
            }
            else
            {
                var model = new ClipboardItem { Text = text, CreatedAt = DateTime.Now };
                _repository.Insert(model);
            }

            TrimIfNeeded();
            LoadAll();
        }, DispatcherPriority.Background);
    }

    /// <summary>カラーピッカーで選択したHEXコードを履歴へ追加する (存在すれば #ColorPicker タグを付与)。</summary>
    /// <param name="hex">HEX形式のカラーコード</param>
    /// <returns>なし</returns>
    public void AddColorPickerEntry(string hex)
    {
        var model = new ClipboardItem { Text = hex, CreatedAt = DateTime.Now };
        if (FindMasterTag("ColorPicker") is not null)
        {
            model.Tags.Add("ColorPicker");
        }

        _repository.Insert(model);
        TrimIfNeeded();
        LoadAll();
    }

    /// <summary>保持数上限を超えた古い非ピン留め項目を削除する。</summary>
    private void TrimIfNeeded()
    {
        int keep = _settingsService.Current.MaxHistoryCount;
        var overflow = _repository.GetTrimCandidates(keep);
        if (overflow.Count == 0)
        {
            return;
        }

        _repository.DeleteMany(overflow.Select(x => x.Id));
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>名前に一致するマスタータグを検索する。</summary>
    /// <param name="name">検索対象のタグ名</param>
    /// <returns>一致したマスタータグ、未検出時はnull</returns>
    public MasterTag? FindMasterTag(string name)
        => _settingsService.Current.MasterTags.FirstOrDefault(t => t.Name == name);

    public IReadOnlyList<MasterTag> MasterTags => _settingsService.Current.MasterTags;

    public double ItemMaxHeight => _settingsService.Current.ItemMaxHeight;
    public string HistoryFontFamily => _settingsService.Current.HistoryFontFamily;
    public double HistoryFontSize => _settingsService.Current.HistoryFontSize;
    public bool IsMemoFeatureEnabled => _settingsService.Current.IsMemoEnabled;

    public int TotalCount => _allItems.Count;
    public int MaxCount => _settingsService.Current.MaxHistoryCount;

    /// <summary>タグバッジクリックをイベントとして通知する。</summary>
    /// <param name="tagName">クリックされたタグ名</param>
    /// <returns>なし</returns>
    public void OnTagBadgeClicked(string tagName) => TagBadgeClicked?.Invoke(tagName);

    /// <summary>設定パネルでの変更 (フォント/高さ/マスタータグ等) をカード表示へ反映する。</summary>
    /// <returns>なし</returns>
    public void RefreshDisplaySettings()
    {
        OnPropertyChanged(string.Empty);
        foreach (var item in _allItems)
        {
            item.RebuildTags();
        }
    }

    /// <summary>指定したアイテムにタグを付与する。</summary>
    /// <param name="item">タグを付与するアイテム</param>
    /// <param name="tagName">付与するタグ名</param>
    /// <returns>なし</returns>
    public void AddTagToItem(ClipboardHistoryItemViewModel item, string tagName)
    {
        if (!item.Model.Tags.Contains(tagName))
        {
            item.Model.Tags.Add(tagName);
            _repository.Update(item.Model);
            item.RebuildTags();
        }

        item.IsTagPickerOpen = false;
    }

    /// <summary>指定したアイテムからタグを解除する。</summary>
    /// <param name="item">タグを解除するアイテム</param>
    /// <param name="tagName">解除するタグ名</param>
    /// <returns>なし</returns>
    public void RemoveTagFromItem(ClipboardHistoryItemViewModel item, string tagName)
    {
        item.Model.Tags.Remove(tagName);
        _repository.Update(item.Model);
        item.RebuildTags();
    }

    /// <summary>マスタータグの名前変更を、既にこのタグが付与されている全レコードへ反映する。</summary>
    /// <param name="oldName">変更前のタグ名</param>
    /// <param name="newName">変更後のタグ名</param>
    /// <returns>なし</returns>
    public void RenameTag(string oldName, string newName)
    {
        foreach (var item in _allItems)
        {
            int index = item.Model.Tags.IndexOf(oldName);
            if (index < 0)
            {
                continue;
            }

            item.Model.Tags[index] = newName;
            _repository.Update(item.Model);
            item.RebuildTags();
        }
    }

    /// <summary>マスタータグの削除を、既にこのタグが付与されている全レコードへ反映する。</summary>
    /// <param name="name">削除するタグ名</param>
    /// <returns>なし</returns>
    public void RemoveTag(string name)
    {
        foreach (var item in _allItems)
        {
            if (!item.Model.Tags.Remove(name))
            {
                continue;
            }

            _repository.Update(item.Model);
            item.RebuildTags();
        }
    }

    /// <summary>インライン編集内容を確定してデータベースへ保存する。</summary>
    /// <param name="item">編集内容を確定するアイテム</param>
    /// <returns>なし</returns>
    public void CommitItemEdit(ClipboardHistoryItemViewModel item)
    {
        item.Model.Text = item.Text;
        _repository.Update(item.Model);
    }

    /// <summary>アイテムのピン留め状態をデータベースへ反映する。</summary>
    /// <param name="item">ピン留め状態を反映するアイテム</param>
    /// <returns>なし</returns>
    public void TogglePin(ClipboardHistoryItemViewModel item)
    {
        item.Model.IsPinned = item.IsPinned;
        _repository.Update(item.Model);
    }

    /// <summary>指定したアイテムを削除する。</summary>
    /// <param name="item">削除対象のアイテム</param>
    /// <returns>なし</returns>
    public void DeleteItem(ClipboardHistoryItemViewModel item)
    {
        int index = Items.IndexOf(item);
        _repository.Delete(item.Model.Id);
        _allItems.Remove(item);
        ApplyFilter(_currentFilterText);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Clamp(index, 0, Items.Count - 1)];
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>アイテムの本文をOSクリップボードへコピーする。</summary>
    /// <param name="item">コピー対象のアイテム</param>
    /// <returns>なし</returns>
    public void CopyItemToClipboard(ClipboardHistoryItemViewModel item)
    {
        _monitorService.NotifySelfWrite(item.Text);
        Clipboard.SetText(item.Text);
        FlashItem(item);
    }

    /// <summary>コピー時のフラッシュ視覚効果を一時的に表示する。</summary>
    /// <param name="item">フラッシュ表示するアイテム</param>
    /// <returns>なし</returns>
    public async void FlashItem(ClipboardHistoryItemViewModel item)
    {
        item.IsFlashing = true;
        await System.Threading.Tasks.Task.Delay(400);
        item.IsFlashing = false;
    }

    /// <summary>クリップボード履歴のテキスト・タグを引き継いだ、新規テキストメモ作成を要求する。</summary>
    /// <param name="item">メモ作成元のクリップボード項目</param>
    /// <returns>なし</returns>
    public void CreateMemo(ClipboardHistoryItemViewModel item)
        => CreateMemoRequested?.Invoke(new MemoDraft(item.Text, IsMarkdown: false, Tags: item.Model.Tags));

    /// <summary>ListBoxのSelectionChangedから、複数選択の状態を反映する。</summary>
    /// <param name="selected">選択されたアイテム一覧</param>
    /// <returns>なし</returns>
    public void UpdateSelection(IEnumerable<ClipboardHistoryItemViewModel> selected)
    {
        SelectedItems.Clear();
        foreach (var item in selected)
        {
            SelectedItems.Add(item);
        }

        NotifySelectionChanged();
    }

    /// <summary>選択状態に依存するプロパティの変更通知をまとめて発行する。</summary>
    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCountText));
        OnPropertyChanged(nameof(AreAllSelectedPinned));
        OnPropertyChanged(nameof(BulkPinButtonLabel));
    }

    /// <summary>選択中の全アイテムのピン留めを一括で切り替える。</summary>
    [RelayCommand]
    private void TogglePinSelected()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        bool pin = !AreAllSelectedPinned;
        foreach (var item in SelectedItems)
        {
            item.IsPinned = pin;
            TogglePin(item);
        }

        NotifySelectionChanged();
    }

    /// <summary>選択中の全アイテムの本文を結合してクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopySelectedToClipboard()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        string combined = string.Join("\n\n", SelectedItems.Select(i => i.Text));
        _monitorService.NotifySelfWrite(combined);
        Clipboard.SetText(combined);
    }

    /// <summary>一括タグ付与ピッカーを開く。</summary>
    [RelayCommand]
    private void OpenBulkTagPicker() => IsBulkTagPickerOpen = true;

    /// <summary>選択中の全アイテムに指定したタグを一括付与する。</summary>
    [RelayCommand]
    private void AddTagToSelected(string tagName)
    {
        foreach (var item in SelectedItems)
        {
            if (!item.Model.Tags.Contains(tagName))
            {
                item.Model.Tags.Add(tagName);
                _repository.Update(item.Model);
                item.RebuildTags();
            }
        }

        IsBulkTagPickerOpen = false;
    }

    /// <summary>確認ダイアログの上で選択中の全アイテムを一括削除する。</summary>
    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            string.Format(LocalizationManager.Instance["Selection_DeleteConfirmMessage"], SelectedItems.Count),
            LocalizationManager.Instance["Selection_DeleteConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var item in SelectedItems.ToList())
        {
            _repository.Delete(item.Model.Id);
            _allItems.Remove(item);
        }

        SelectedItems.Clear();
        NotifySelectionChanged();
        ApplyFilter(_currentFilterText);
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>確認ダイアログの上でピン留め以外の全履歴を削除する。</summary>
    [RelayCommand]
    private void ClearAll()
    {
        var result = MessageBox.Show(
            LocalizationManager.Instance["ClearAll_ConfirmMessage"],
            LocalizationManager.Instance["ClearAll_ConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var toDelete = _allItems.Where(i => !i.IsPinned).ToList();
        foreach (var item in toDelete)
        {
            _repository.Delete(item.Model.Id);
            _allItems.Remove(item);
        }

        ApplyFilter(_currentFilterText);
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }
}
