using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>メモ機能タブ全体の状態と操作を管理するViewModel。</summary>
public partial class MemoViewModel : ObservableObject, IQuickFilterable
{
    // 生成する Markdown 画像記法 ![alt](path) からパス部分を抽出する
    private static readonly Regex MarkdownImageRegex = new(@"!\[[^\]]*\]\(([^)\s]+)\)", RegexOptions.Compiled);

    private readonly SettingsService _settingsService;
    private readonly MemoRepository _repository;
    private readonly MemoTemplateRepository _templateRepository;
    private readonly ClipboardMonitorService _monitorService;
    private readonly MarkdownRenderer _markdownRenderer = new();

    private List<MemoItemViewModel> _allItems = new();
    private string _currentFilterText = string.Empty;

    public event Action<string>? TagBadgeClicked;

    /// <summary>編集ウィンドウを開いてほしいという要求。第1引数が null は新規作成を意味する。
    /// 第2引数は新規作成時に初期本文を差し込みたい場合の下書き情報 (通常は null)。</summary>
    public event Action<MemoItemViewModel?, MemoDraft?>? EditRequested;

    /// <summary>テンプレート編集ウィンドウを開いてほしいという要求。null は新規テンプレート作成、
    /// Id が空の下書きは既存メモから複製した新規テンプレート、Id が入っていれば既存テンプレートの編集。</summary>
    public event Action<MemoTemplate?>? TemplateEditRequested;

    /// <summary>テンプレート管理ウィンドウを開いてほしいという要求。</summary>
    public event Action? TemplateManagerRequested;

    public ObservableCollection<MemoItemViewModel> Items { get; } = new();

    public ObservableCollection<MemoTemplate> Templates { get; } = new();

    [ObservableProperty]
    private bool _isNewMemoPickerOpen;

    /// <summary>Ctrlキー等で複数選択されたレコード (一括削除・一括タグ付与の対象)。</summary>
    public ObservableCollection<MemoItemViewModel> SelectedItems { get; } = new();

    public bool HasSelection => SelectedItems.Count > 0;

    public string SelectionCountText => string.Format(LocalizationManager.Instance["Selection_Count"], SelectedItems.Count);

    public bool AreAllSelectedPinned => SelectedItems.Count > 0 && SelectedItems.All(i => i.IsPinned);

    public string BulkPinButtonLabel => LocalizationManager.Instance[AreAllSelectedPinned ? "Tooltip_Unpin" : "Button_Pin"];

    [ObservableProperty]
    private MemoItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isBulkTagPickerOpen;

    public VimNavigationController Vim { get; }

    public IReadOnlyList<MasterTag> MasterTags => _settingsService.Current.MasterTags;

    public double ItemMaxHeight => _settingsService.Current.ItemMaxHeight;

    public int TotalCount => _allItems.Count;

    /// <summary>メモViewModelの初期化とデータ読み込み。</summary>
    /// <param name="settingsService">アプリ設定サービス</param>
    /// <param name="repository">メモリポジトリ</param>
    /// <param name="templateRepository">メモテンプレートリポジトリ</param>
    /// <param name="monitorService">クリップボード監視サービス</param>
    public MemoViewModel(
        SettingsService settingsService,
        MemoRepository repository,
        MemoTemplateRepository templateRepository,
        ClipboardMonitorService monitorService)
    {
        _settingsService = settingsService;
        _repository = repository;
        _templateRepository = templateRepository;
        _monitorService = monitorService;

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
            EnterEdit = () => { if (SelectedItem is not null) RequestEdit(SelectedItem); },
        };

        LoadAll();
        LoadTemplates();
    }

    /// <summary>データベースから全メモを読み込む。</summary>
    private void LoadAll()
    {
        _allItems = _repository.GetAll().Select(m => new MemoItemViewModel(m, this)).ToList();
        ApplyFilter(_currentFilterText);
    }

    /// <summary>データベースから全テンプレートを読み込む。</summary>
    private void LoadTemplates()
    {
        Templates.Clear();
        foreach (var template in _templateRepository.GetAll())
        {
            Templates.Add(template);
        }
    }

    /// <summary>テンプレート管理ウィンドウでの変更をカード表示側へ反映する。</summary>
    /// <returns>なし</returns>
    public void ReloadTemplates() => LoadTemplates();

    /// <summary>クイックフィルター文字列で一覧を絞り込む。</summary>
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
    }

    /// <summary>次の項目を選択する。</summary>
    private void SelectNext()
    {
        if (Items.Count == 0) return;
        int index = SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        SelectedItem = Items[Math.Min(index + 1, Items.Count - 1)];
    }

    /// <summary>前の項目を選択する。</summary>
    private void SelectPrevious()
    {
        if (Items.Count == 0) return;
        int index = SelectedItem is null ? 0 : Items.IndexOf(SelectedItem);
        SelectedItem = Items[Math.Max(index - 1, 0)];
    }

    /// <summary>名前からマスタータグ定義を検索する。</summary>
    /// <param name="name">検索対象のタグ名</param>
    /// <returns>一致したマスタータグ、未検出時はnull</returns>
    public MasterTag? FindMasterTag(string name)
        => _settingsService.Current.MasterTags.FirstOrDefault(t => t.Name == name);

    /// <summary>
    /// 設定パネル等、WebView2より手前に表示したいオーバーレイを開いている間、
    /// 一覧カードのMarkdownプレビュー(WebView2)を一時的に隠す。
    /// </summary>
    /// <param name="suspended">プレビュー抑制フラグ</param>
    /// <returns>なし</returns>
    public void SetPreviewsSuspended(bool suspended)
    {
        foreach (var item in _allItems)
        {
            item.IsPreviewSuspended = suspended;
        }
    }

    /// <summary>一覧カードでのMarkdownプレビュー表示用にHTMLへ変換する。</summary>
    /// <param name="body">Markdown本文</param>
    /// <returns>HTML形式の変換結果</returns>
    public string RenderMarkdownPreview(string body)
    {
        var s = _settingsService.Current;
        return _markdownRenderer.RenderHtmlDocument(
            body,
            s.IsDarkMode,
            s.MemoMarkdownViewFontFamily,
            s.MemoMarkdownViewFontSize,
            s.MemoMarkdownCodeFontFamily,
            s.MemoMarkdownCodeFontSize,
            isPreview: true);
    }

    /// <summary>タグバッジクリックをイベントとして通知する。</summary>
    /// <param name="tagName">クリックされたタグ名</param>
    /// <returns>なし</returns>
    public void OnTagBadgeClicked(string tagName) => TagBadgeClicked?.Invoke(tagName);

    /// <summary>指定タグをメモへ付与する。</summary>
    /// <param name="item">タグを付与するメモ</param>
    /// <param name="tagName">付与するタグ名</param>
    /// <returns>なし</returns>
    public void AddTagToItem(MemoItemViewModel item, string tagName)
    {
        if (!item.Model.Tags.Contains(tagName))
        {
            item.Model.Tags.Add(tagName);
            _repository.Update(item.Model);
            item.RebuildTags();
        }

        item.IsTagPickerOpen = false;
    }

    /// <summary>指定タグをメモから解除する。</summary>
    /// <param name="item">タグを解除するメモ</param>
    /// <param name="tagName">解除するタグ名</param>
    /// <returns>なし</returns>
    public void RemoveTagFromItem(MemoItemViewModel item, string tagName)
    {
        item.Model.Tags.Remove(tagName);
        _repository.Update(item.Model);
        item.RebuildTags();
    }

    /// <summary>マスタータグの名前変更を、既にこのタグが付与されている全レコード/テンプレートへ反映する。</summary>
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

        foreach (var template in Templates)
        {
            int index = template.Tags.IndexOf(oldName);
            if (index < 0)
            {
                continue;
            }

            template.Tags[index] = newName;
            _templateRepository.Update(template);
        }
    }

    /// <summary>マスタータグの削除を、既にこのタグが付与されている全レコード/テンプレートへ反映する。</summary>
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

        foreach (var template in Templates)
        {
            if (template.Tags.Remove(name))
            {
                _templateRepository.Update(template);
            }
        }
    }

    /// <summary>設定パネルでの変更 (マスタータグ・テーマ・フォント等) をカード表示へ反映する。</summary>
    /// <returns>なし</returns>
    public void RefreshDisplaySettings()
    {
        OnPropertyChanged(string.Empty);
        foreach (var item in _allItems)
        {
            item.RebuildTags();
            item.RefreshRenderedHtml();
        }
    }

    /// <summary>メモ編集ウィンドウを開くよう要求する。</summary>
    /// <param name="item">編集対象のメモ (nullは新規作成)</param>
    /// <returns>なし</returns>
    public void RequestEdit(MemoItemViewModel? item) => EditRequested?.Invoke(item, null);

    /// <summary>他機能から初期本文付きで新規メモ作成ウィンドウを開く。</summary>
    /// <param name="draft">初期本文・モード・タグの下書き情報</param>
    /// <returns>なし</returns>
    public void RequestNewMemo(MemoDraft draft) => EditRequested?.Invoke(null, draft);

    /// <summary>ListBoxのSelectionChangedから、複数選択の状態を反映する。</summary>
    /// <param name="selected">選択されたメモ一覧</param>
    /// <returns>なし</returns>
    public void UpdateSelection(IEnumerable<MemoItemViewModel> selected)
    {
        SelectedItems.Clear();
        foreach (var item in selected)
        {
            SelectedItems.Add(item);
        }

        NotifySelectionChanged();
    }

    /// <summary>選択状態に依存するプロパティの変更を通知する。</summary>
    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCountText));
        OnPropertyChanged(nameof(AreAllSelectedPinned));
        OnPropertyChanged(nameof(BulkPinButtonLabel));
    }

    /// <summary>選択中の全項目のピン留め状態を一括切替する。</summary>
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

    /// <summary>選択中の全項目の本文を結合してクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopySelectedToClipboard()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        string combined = string.Join("\n\n", SelectedItems.Select(i => i.Body));
        _monitorService.NotifySelfWrite(combined);
        Clipboard.SetText(combined);
    }

    /// <summary>一括タグ付与用ピッカーを開く。</summary>
    [RelayCommand]
    private void OpenBulkTagPicker() => IsBulkTagPickerOpen = true;

    /// <summary>選択中の全項目へ指定タグを一括付与する。</summary>
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

    /// <summary>確認ダイアログの上、選択中の全項目を一括削除する。</summary>
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

    /// <summary>➕ ボタン押下時。テンプレートが1件も無ければ従来通り即座に白紙のメモを開き、
    /// 1件以上あれば「白紙から作成」とテンプレート一覧を選べるピッカーを開く。</summary>
    [RelayCommand]
    private void CreateNew()
    {
        if (Templates.Count == 0)
        {
            RequestEdit(null);
            return;
        }

        IsNewMemoPickerOpen = true;
    }

    /// <summary>白紙のメモを新規作成する。</summary>
    [RelayCommand]
    private void CreateBlankMemo()
    {
        IsNewMemoPickerOpen = false;
        RequestEdit(null);
    }

    /// <summary>テンプレート本文の予約文字 ({date} 等) を展開し、そのテンプレートを初期値とした新規メモを開く。</summary>
    [RelayCommand]
    private void CreateFromTemplate(MemoTemplate template)
    {
        IsNewMemoPickerOpen = false;
        string expandedBody = MemoTemplateExpander.Expand(template.Body, DateTime.Now);
        RequestNewMemo(new MemoDraft(expandedBody, template.Mode == MemoMode.Markdown, template.Tags));
    }

    /// <summary>テンプレート管理ウィンドウを開くよう要求する。</summary>
    [RelayCommand]
    private void OpenTemplateManager() => TemplateManagerRequested?.Invoke();

    /// <summary>既存メモの内容を初期値とした新規テンプレート作成ウィンドウを開く。</summary>
    /// <param name="item">テンプレート化するメモ</param>
    /// <returns>なし</returns>
    public void RequestSaveAsTemplate(MemoItemViewModel item)
    {
        var seed = new MemoTemplate
        {
            Name = item.Title,
            Mode = item.Mode,
            Body = item.Body,
            Tags = new List<string>(item.Model.Tags),
        };

        TemplateEditRequested?.Invoke(seed);
    }

    /// <summary>MemoEditWindow の保存結果を反映する。新規作成時は existingItem が null。</summary>
    /// <param name="existingItem">編集対象メモ (nullは新規作成)</param>
    /// <param name="title">メモのタイトル</param>
    /// <param name="mode">テキストまたはMarkdown</param>
    /// <param name="body">メモの本文</param>
    /// <param name="tags">付与するタグ一覧</param>
    /// <returns>なし</returns>
    public void SaveFromEditor(MemoItemViewModel? existingItem, string title, MemoMode mode, string body, List<string> tags)
    {
        if (existingItem is null)
        {
            var model = new MemoItem
            {
                Title = title,
                Mode = mode,
                Body = body,
                Tags = tags,
                CreatedAt = DateTime.Now,
            };
            _repository.Insert(model);
        }
        else
        {
            existingItem.Model.Title = title;
            existingItem.Model.Mode = mode;
            existingItem.Model.Body = body;
            existingItem.Model.Tags = tags;
            _repository.Update(existingItem.Model);
        }

        LoadAll();
    }

    /// <summary>ピン留め状態をデータベースへ反映する。</summary>
    /// <param name="item">ピン留め状態を反映するメモ</param>
    /// <returns>なし</returns>
    public void TogglePin(MemoItemViewModel item)
    {
        item.Model.IsPinned = item.IsPinned;
        _repository.Update(item.Model);
    }

    /// <summary>メモを削除する。</summary>
    /// <param name="item">削除対象のメモ</param>
    /// <returns>なし</returns>
    public void DeleteItem(MemoItemViewModel item)
    {
        int index = Items.IndexOf(item);
        _repository.Delete(item.Model.Id);
        _allItems.Remove(item);
        ApplyFilter(_currentFilterText);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Clamp(index, 0, Items.Count - 1)];
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>本文をクリップボードへコピーしフラッシュ表示する。</summary>
    /// <param name="item">コピー対象のメモ</param>
    /// <returns>なし</returns>
    public void CopyItemToClipboard(MemoItemViewModel item)
    {
        _monitorService.NotifySelfWrite(item.Body);
        Clipboard.SetText(item.Body);
        Flash(item);
    }

    /// <summary>カードを一時的に光らせるフラッシュ効果を適用する。</summary>
    /// <param name="item">フラッシュ表示するメモ</param>
    /// <returns>なし</returns>
    public async void Flash(MemoItemViewModel item)
    {
        item.IsFlashing = true;
        await System.Threading.Tasks.Task.Delay(400);
        item.IsFlashing = false;
    }

    /// <summary>確認ダイアログの上、ピン留め以外の全メモを削除する。</summary>
    [RelayCommand]
    private void ClearAll()
    {
        var result = MessageBox.Show(
            LocalizationManager.Instance["ClearAll_ConfirmMessage"],
            LocalizationManager.Instance["ClearAll_ConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var toDelete = _allItems.Where(i => !i.IsPinned).ToList();
        foreach (var item in toDelete)
        {
            _repository.Delete(item.Model.Id);
            _allItems.Remove(item);
        }

        ApplyFilter(_currentFilterText);
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>メモから参照されなくなった孤立画像ファイルを一括削除する。</summary>
    [RelayCommand]
    private void CleanOrphanedImages()
    {
        string memoImagesRoot = Path.Combine(AppPaths.DataFolder, "images", "memo");
        if (!Directory.Exists(memoImagesRoot))
        {
            return;
        }

        var confirmResult = MessageBox.Show(
            LocalizationManager.Instance["Cleanup_ConfirmMessage_Memo"],
            LocalizationManager.Instance["Cleanup_ConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
        {
            return;
        }

        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _allItems.Where(i => i.Model.Mode == MemoMode.Markdown))
        {
            foreach (Match match in MarkdownImageRegex.Matches(item.Model.Body))
            {
                string rawPath = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                try
                {
                    referencedPaths.Add(Path.GetFullPath(rawPath));
                }
                catch (ArgumentException)
                {
                    // 画像記法とは関係ない不正なパス文字列は無視する
                }
            }
        }

        int deletedFiles = 0;
        foreach (string file in Directory.EnumerateFiles(memoImagesRoot, "*", SearchOption.AllDirectories))
        {
            if (!referencedPaths.Contains(Path.GetFullPath(file)))
            {
                try
                {
                    File.Delete(file);
                    deletedFiles++;
                }
                catch
                {
                    // ignore
                }
            }
        }

        ImageStorageService.RemoveEmptyDirectories(memoImagesRoot);

        MessageBox.Show(
            string.Format(LocalizationManager.Instance["Cleanup_ResultMessage"], deletedFiles),
            LocalizationManager.Instance["Cleanup_ResultTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
