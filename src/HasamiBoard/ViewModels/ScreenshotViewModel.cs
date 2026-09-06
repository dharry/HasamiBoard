using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>スクリーンショット機能タブ全体の状態と操作を管理するViewModel。</summary>
public partial class ScreenshotViewModel : ObservableObject, IQuickFilterable
{
    private readonly SettingsService _settingsService;
    private readonly ScreenshotHistoryRepository _repository;
    private readonly ImageStorageService _imageStorageService;
    private readonly ClipboardMonitorService _monitorService;
    private readonly OcrService _ocrService;

    private List<ScreenshotItemViewModel> _allItems = new();
    private string _currentFilterText = string.Empty;

    public event Action<string>? TagBadgeClicked;
    public event Action<MemoDraft>? CreateMemoRequested;
    public event Action<BitmapSource>? AnalyzeQrCodeRequested;

    public ObservableCollection<ScreenshotItemViewModel> Items { get; } = new();

    /// <summary>Ctrlキー等で複数選択されたレコード (一括削除・一括タグ付与の対象)。</summary>
    public ObservableCollection<ScreenshotItemViewModel> SelectedItems { get; } = new();

    public bool HasSelection => SelectedItems.Count > 0;

    public string SelectionCountText => string.Format(LocalizationManager.Instance["Selection_Count"], SelectedItems.Count);

    public bool AreAllSelectedPinned => SelectedItems.Count > 0 && SelectedItems.All(i => i.IsPinned);

    public string BulkPinButtonLabel => LocalizationManager.Instance[AreAllSelectedPinned ? "Tooltip_Unpin" : "Button_Pin"];

    [ObservableProperty]
    private ScreenshotItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isBulkTagPickerOpen;

    public VimNavigationController Vim { get; }

    public double ThumbnailHeight => _settingsService.Current.ScreenshotThumbnailHeight;
    public string MemoFontFamily => _settingsService.Current.ScreenshotMemoFontFamily;
    public double MemoFontSize => _settingsService.Current.ScreenshotMemoFontSize;
    public IReadOnlyList<MasterTag> MasterTags => _settingsService.Current.MasterTags;
    public bool IsMemoFeatureEnabled => _settingsService.Current.IsMemoEnabled;
    public bool IsQrCodeFeatureEnabled => _settingsService.Current.IsConverterEnabled
        && !_settingsService.Current.ConverterDisabledModes.Contains(nameof(ConverterMode.QrCode));

    public int TotalCount => _allItems.Count;
    public int MaxCount => _settingsService.Current.MaxScreenshotHistoryCount;

    /// <summary>スクリーンショットViewModelの初期化とデータ読み込み。</summary>
    /// <param name="settingsService">アプリ設定サービス</param>
    /// <param name="repository">スクリーンショットリポジトリ</param>
    /// <param name="imageStorageService">画像保存サービス</param>
    /// <param name="monitorService">クリップボード監視サービス</param>
    /// <param name="ocrService">OCRサービス</param>
    public ScreenshotViewModel(
        SettingsService settingsService,
        ScreenshotHistoryRepository repository,
        ImageStorageService imageStorageService,
        ClipboardMonitorService monitorService,
        OcrService ocrService)
    {
        _settingsService = settingsService;
        _repository = repository;
        _imageStorageService = imageStorageService;
        _monitorService = monitorService;
        _ocrService = ocrService;

        _monitorService.ImageCaptured += OnImageCaptured;

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
                else if (SelectedItem is not null) CopyNoteToClipboard(SelectedItem);
            },
            EnterEdit = () => SelectedItem?.BeginEditNoteCommand.Execute(null),
        };

        LoadAll();
    }

    /// <summary>データベースから全スクリーンショットを読み込む。</summary>
    private void LoadAll()
    {
        _allItems = _repository.GetAll()
            .Select(m => new ScreenshotItemViewModel(m, this, m.ImagePath))
            .ToList();
        ApplyFilter(_currentFilterText);
    }

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
                item.LoadThumbnail();
            }
        }

        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(i => i.Model.Id == previouslySelectedId) ?? Items.FirstOrDefault();

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(MaxCount));
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

    /// <summary>クリップボード監視サービスからの画像キャプチャ通知を受けて新規レコードを追加する。</summary>
    private void OnImageCaptured(BitmapSource image)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now;
            string imagePath = _imageStorageService.SaveScreenshot(image, timestamp);

            var model = new ScreenshotItem
            {
                ImagePath = imagePath,
                CreatedAt = timestamp,
            };

            if (_settingsService.Current.IsDominantColorExtractionEnabled)
            {
                model.DominantColorsHex = _imageStorageService.ExtractDominantColors(imagePath);
            }

            _repository.Insert(model);

            TrimIfNeeded();
            LoadAll();
        }, DispatcherPriority.Background);
    }

    /// <summary>保持数上限を超えた古い非ピン留めレコードを削除する。</summary>
    private void TrimIfNeeded()
    {
        int keep = _settingsService.Current.MaxScreenshotHistoryCount;
        var overflow = _repository.GetTrimCandidates(keep);
        if (overflow.Count == 0) return;

        foreach (var item in overflow)
        {
            _imageStorageService.DeleteImageFile(item.ImagePath);
            _repository.Delete(item.Id);
        }

        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>名前からマスタータグ定義を検索する。</summary>
    /// <param name="name">検索対象のタグ名</param>
    /// <returns>一致したマスタータグ、未検出時はnull</returns>
    public MasterTag? FindMasterTag(string name)
        => _settingsService.Current.MasterTags.FirstOrDefault(t => t.Name == name);

    /// <summary>タグバッジクリックをイベントとして通知する。</summary>
    /// <param name="tagName">クリックされたタグ名</param>
    /// <returns>なし</returns>
    public void OnTagBadgeClicked(string tagName) => TagBadgeClicked?.Invoke(tagName);

    /// <summary>指定タグをレコードへ付与する。</summary>
    /// <param name="item">タグを付与するスクリーンショット</param>
    /// <param name="tagName">付与するタグ名</param>
    /// <returns>なし</returns>
    public void AddTagToItem(ScreenshotItemViewModel item, string tagName)
    {
        if (!item.Model.Tags.Contains(tagName))
        {
            item.Model.Tags.Add(tagName);
            _repository.Update(item.Model);
            item.RebuildTags();
        }

        item.IsTagPickerOpen = false;
    }

    /// <summary>指定タグをレコードから解除する。</summary>
    /// <param name="item">タグを解除するスクリーンショット</param>
    /// <param name="tagName">解除するタグ名</param>
    /// <returns>なし</returns>
    public void RemoveTagFromItem(ScreenshotItemViewModel item, string tagName)
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

    /// <summary>インライン編集中のメモをデータベースへ確定保存する。</summary>
    /// <param name="item">編集確定するスクリーンショット</param>
    /// <returns>なし</returns>
    public void CommitNoteEdit(ScreenshotItemViewModel item)
    {
        item.Model.Note = item.Note;
        _repository.Update(item.Model);
    }

    /// <summary>ピン留め状態をデータベースへ反映する。</summary>
    /// <param name="item">ピン留め状態を反映するスクリーンショット</param>
    /// <returns>なし</returns>
    public void TogglePin(ScreenshotItemViewModel item)
    {
        item.Model.IsPinned = item.IsPinned;
        _repository.Update(item.Model);
    }

    /// <summary>レコードと画像ファイルを削除する。</summary>
    /// <param name="item">削除対象のスクリーンショット</param>
    /// <returns>なし</returns>
    public void DeleteItem(ScreenshotItemViewModel item)
    {
        int index = Items.IndexOf(item);
        _repository.Delete(item.Model.Id);
        _imageStorageService.DeleteImageFile(item.Model.ImagePath);
        _allItems.Remove(item);
        ApplyFilter(_currentFilterText);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Clamp(index, 0, Items.Count - 1)];
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>メモをクリップボードへコピーしフラッシュ表示する。</summary>
    /// <param name="item">コピー対象のスクリーンショット</param>
    /// <returns>なし</returns>
    public void CopyNoteToClipboard(ScreenshotItemViewModel item)
    {
        _monitorService.NotifySelfWrite(item.Note);
        Clipboard.SetText(item.Note);
        Flash(item);
    }

    /// <summary>画像をクリップボードへコピーしフラッシュ表示する。</summary>
    /// <param name="item">画像コピー対象のスクリーンショット</param>
    /// <returns>なし</returns>
    public void CopyImageToClipboard(ScreenshotItemViewModel item)
    {
        try
        {
            var bitmap = LoadBitmap(item.AbsoluteImagePath);

            _monitorService.NotifySelfImageWrite();
            Clipboard.SetImage(bitmap);
            Flash(item);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>スクリーンショットのメモと画像 (メモ用に複製) から、新規メモ作成用の下書きを組み立てて要求する。</summary>
    /// <param name="item">メモ作成元のスクリーンショット</param>
    /// <returns>なし</returns>
    public void CreateMemo(ScreenshotItemViewModel item)
    {
        string body = item.Note;

        try
        {
            var bitmap = LoadBitmap(item.AbsoluteImagePath);
            string memoImagePath = _imageStorageService.SaveMemoImage(bitmap, DateTime.Now);
            string imageMarkdown = $"![screenshot]({memoImagePath.Replace('\\', '/')})";
            body = string.IsNullOrWhiteSpace(body) ? imageMarkdown : $"{body}\n\n{imageMarkdown}";
        }
        catch
        {
            // 画像の複製に失敗した場合は、メモへの本文コピーのみで続行する
        }

        CreateMemoRequested?.Invoke(new MemoDraft(body, IsMarkdown: true, Tags: item.Model.Tags));
    }

    /// <summary>スクリーンショットの画像を読み込み、開発ツールのQRコードモードでの解析を要求する。</summary>
    /// <param name="item">解析対象のスクリーンショット</param>
    public void AnalyzeQrCode(ScreenshotItemViewModel item)
    {
        if (!IsQrCodeFeatureEnabled)
        {
            return;
        }

        try
        {
            var bitmap = LoadBitmap(item.AbsoluteImagePath);
            AnalyzeQrCodeRequested?.Invoke(bitmap);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            // 画像の読み込みに失敗した場合は何もしない
        }
    }

    /// <summary>ListBoxのSelectionChangedから、複数選択の状態を反映する。</summary>
    /// <param name="selected">選択されたスクリーンショット一覧</param>
    /// <returns>なし</returns>
    public void UpdateSelection(IEnumerable<ScreenshotItemViewModel> selected)
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

    /// <summary>選択中の全項目のメモを結合してクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopySelectedToClipboard()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        string combined = string.Join("\n\n", SelectedItems.Select(i => i.Note));
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
            _imageStorageService.DeleteImageFile(item.Model.ImagePath);
            _allItems.Remove(item);
        }

        SelectedItems.Clear();
        NotifySelectionChanged();
        ApplyFilter(_currentFilterText);
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>指定パスの画像をBitmapImageとして読み込む。</summary>
    private static BitmapImage LoadBitmap(string absolutePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>画像に対してOCR文字認識を実行し、結果をメモへ反映する。</summary>
    /// <param name="item">OCR対象のスクリーンショット</param>
    /// <returns>非同期実行タスク</returns>
    public async Task RunOcrAsync(ScreenshotItemViewModel item)
    {
        if (item.IsOcrRunning)
        {
            return;
        }

        item.IsOcrRunning = true;
        try
        {
            string text = await _ocrService.RecognizeTextAsync(item.AbsoluteImagePath);
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (item.IsEditingNote)
                {
                    // 編集中に上書きすると未保存の入力が消えてしまうため、
                    // 編集が終わるまでOCR結果の反映を見送る。
                    MessageBox.Show(
                        LocalizationManager.Instance["Ocr_NoteEditInProgressMessage"],
                        LocalizationManager.Instance["Ocr_NoteEditInProgressTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    item.Note = text;
                    item.EditingNote = item.Note;
                    item.Model.Note = item.Note;
                    _repository.Update(item.Model);
                }
            }
        }
        catch (OcrEngineUnavailableException)
        {
            MessageBox.Show(
                LocalizationManager.Instance["Ocr_LanguagePackMissingMessage"],
                LocalizationManager.Instance["Ocr_LanguagePackMissingTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch
        {
            // その他の予期しない失敗は静かに無視する
        }
        finally
        {
            item.IsOcrRunning = false;
        }
    }

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

    /// <summary>画像を既定の画像ビューアーで開く。</summary>
    /// <param name="item">表示対象のスクリーンショット</param>
    /// <returns>なし</returns>
    public void OpenImageInViewer(ScreenshotItemViewModel item)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = item.AbsoluteImagePath, UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>画像を含むフォルダをエクスプローラーで開く。</summary>
    /// <param name="item">対象フォルダを開くスクリーンショット</param>
    /// <returns>なし</returns>
    public void OpenContainingFolder(ScreenshotItemViewModel item)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{item.AbsoluteImagePath}\"",
                UseShellExecute = true,
            });
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>カラーコードをクリップボードへコピーする。</summary>
    /// <param name="hex">HEX形式のカラーコード</param>
    /// <returns>なし</returns>
    public void CopyColorToClipboard(string hex)
    {
        _monitorService.NotifySelfWrite(hex);
        Clipboard.SetText(hex);
    }

    /// <summary>カードを一時的に光らせるフラッシュ効果を適用する。</summary>
    /// <param name="item">フラッシュ表示するスクリーンショット</param>
    /// <returns>なし</returns>
    public async void Flash(ScreenshotItemViewModel item)
    {
        item.IsFlashing = true;
        await System.Threading.Tasks.Task.Delay(400);
        item.IsFlashing = false;
    }

    /// <summary>確認ダイアログの上、ピン留め以外の全レコードを削除する。</summary>
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
            _imageStorageService.DeleteImageFile(item.Model.ImagePath);
            _allItems.Remove(item);
        }

        ApplyFilter(_currentFilterText);
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>履歴から参照されなくなった孤立画像ファイルを一括削除する。</summary>
    [RelayCommand]
    private void CleanOrphanedImages()
    {
        string screenshotRoot = Path.Combine(AppPaths.DataFolder, "images", "screenshot");
        if (!Directory.Exists(screenshotRoot))
        {
            return;
        }

        var confirmResult = MessageBox.Show(
            LocalizationManager.Instance["Cleanup_ConfirmMessage"],
            LocalizationManager.Instance["Cleanup_ConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
        {
            return;
        }

        var registeredAbsolutePaths = new HashSet<string>(
            _repository.GetAll().Select(x => x.ImagePath),
            StringComparer.OrdinalIgnoreCase);

        int deletedFiles = 0;
        foreach (string file in Directory.EnumerateFiles(screenshotRoot, "*", SearchOption.AllDirectories))
        {
            if (!registeredAbsolutePaths.Contains(file))
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

        ImageStorageService.RemoveEmptyDirectories(screenshotRoot);

        MessageBox.Show(
            string.Format(LocalizationManager.Instance["Cleanup_ResultMessage"], deletedFiles),
            LocalizationManager.Instance["Cleanup_ResultTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
