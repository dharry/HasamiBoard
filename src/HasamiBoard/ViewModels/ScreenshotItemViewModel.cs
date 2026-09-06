using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;

namespace HasamiBoard.ViewModels;

/// <summary>スクリーンショット一覧の各カードを表すViewModel。</summary>
public partial class ScreenshotItemViewModel : ObservableObject
{
    public ScreenshotItem Model { get; }

    private readonly ScreenshotViewModel _owner;

    [ObservableProperty]
    private string _note;

    [ObservableProperty]
    private string _editingNote = string.Empty;

    [ObservableProperty]
    private bool _isEditingNote;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isFlashing;

    [ObservableProperty]
    private bool _isTagPickerOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunOcr))]
    private bool _isOcrRunning;

    public bool CanRunOcr => !IsOcrRunning;

    [ObservableProperty]
    private BitmapImage? _thumbnail;

    public string AbsoluteImagePath { get; }

    public DateTime CreatedAt => Model.CreatedAt;

    public string Format => Model.Format;

    public ObservableCollection<TagBadgeViewModel> Tags { get; } = new();

    public ObservableCollection<string> DominantColors { get; }

    public string SearchableText => (Note + " " + System.IO.Path.GetFileName(Model.ImagePath));

    public List<MasterTag> AvailableTagsToAdd
        => _owner.MasterTags.Where(t => !Model.Tags.Contains(t.Name)).ToList();

    public bool HasNoAvailableTags => AvailableTagsToAdd.Count == 0;

    /// <summary>スクリーンショットカードViewModelの初期化。</summary>
    /// <param name="model">スクリーンショットのモデル</param>
    /// <param name="owner">親のビューモデル</param>
    /// <param name="absoluteImagePath">画像ファイルの絶対パス</param>
    public ScreenshotItemViewModel(ScreenshotItem model, ScreenshotViewModel owner, string absoluteImagePath)
    {
        Model = model;
        _owner = owner;
        AbsoluteImagePath = absoluteImagePath;
        _note = model.Note;
        _isPinned = model.IsPinned;
        DominantColors = new ObservableCollection<string>(model.DominantColorsHex);
        RebuildTags();
    }

    /// <summary>サムネイル画像を遅延読み込みする。</summary>
    /// <returns>なし</returns>
    public void LoadThumbnail()
    {
        if (Thumbnail is not null)
        {
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 400;
            bitmap.UriSource = new Uri(AbsoluteImagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            Thumbnail = bitmap;
        }
        catch
        {
            Thumbnail = null;
        }
    }

    /// <summary>タグバッジ一覧をモデルのタグ情報から再構築する。</summary>
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

    /// <summary>メモのインライン編集を開始する。</summary>
    [RelayCommand]
    private void BeginEditNote()
    {
        EditingNote = Note;
        IsEditingNote = true;
    }

    /// <summary>編集中のメモを確定する。</summary>
    [RelayCommand]
    private void CommitEditNote()
    {
        Note = EditingNote;
        IsEditingNote = false;
        _owner.CommitNoteEdit(this);
    }

    /// <summary>メモのインライン編集をキャンセルする。</summary>
    [RelayCommand]
    private void CancelEditNote() => IsEditingNote = false;

    /// <summary>ピン留め状態を切り替える。</summary>
    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
        _owner.TogglePin(this);
    }

    /// <summary>このレコードを削除する。</summary>
    [RelayCommand]
    private void Delete() => _owner.DeleteItem(this);

    /// <summary>メモをクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopyNote() => _owner.CopyNoteToClipboard(this);

    /// <summary>画像をクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopyImage() => _owner.CopyImageToClipboard(this);

    /// <summary>このレコードからメモを新規作成する。</summary>
    [RelayCommand]
    private void CreateMemo() => _owner.CreateMemo(this);

    /// <summary>画像を既定の画像ビューアーで開く。</summary>
    [RelayCommand]
    private void OpenImage() => _owner.OpenImageInViewer(this);

    /// <summary>画像を含むフォルダをエクスプローラーで開く。</summary>
    [RelayCommand]
    private void OpenFolder() => _owner.OpenContainingFolder(this);

    /// <summary>OCR文字認識を実行する。</summary>
    [RelayCommand]
    private async Task RunOcr() => await _owner.RunOcrAsync(this);

    /// <summary>開発ツールのQRコードモードを起動し、この画像のQRコードを解析する。</summary>
    [RelayCommand]
    private void AnalyzeQrCode() => _owner.AnalyzeQrCode(this);

    /// <summary>タグ選択ピッカーを開く。</summary>
    [RelayCommand]
    private void OpenTagPicker() => IsTagPickerOpen = true;

    /// <summary>指定タグをこのレコードへ付与する。</summary>
    [RelayCommand]
    private void AddTag(string tagName) => _owner.AddTagToItem(this, tagName);

    /// <summary>指定カラーコードをクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopyColor(string hex) => _owner.CopyColorToClipboard(hex);
}
