using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;

namespace HasamiBoard.ViewModels;

/// <summary>メモ一覧の各カードを表すViewModel。</summary>
public partial class MemoItemViewModel : ObservableObject
{
    private const int PreviewLineLimit = 8;

    public MemoItem Model { get; }

    private readonly MemoViewModel _owner;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _body;

    [ObservableProperty]
    private MemoMode _mode;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isFlashing;

    [ObservableProperty]
    private bool _isTagPickerOpen;

    [ObservableProperty]
    private bool _isPreviewSuspended;

    public DateTime CreatedAt => Model.CreatedAt;

    public string ModeLabel => Mode == MemoMode.Markdown ? "Markdown" : "Text";

    public bool IsMarkdown => Mode == MemoMode.Markdown;

    /// <summary>
    /// カードにMarkdownプレビュー(WebView2)を表示してよいか。設定パネル等、
    /// WebView2より手前に表示したいオーバーレイが開いている間は false になる
    /// (WebView2はネイティブ描画領域を持つため、通常のWPFオーバーレイの背面に回せない)。
    /// </summary>
    public bool ShowMarkdownPreview => IsMarkdown && !IsPreviewSuspended;

    public string BodyPreview => ClampToLines(Body, PreviewLineLimit);

    public string RenderedHtml => IsMarkdown ? _owner.RenderMarkdownPreview(Body) : string.Empty;

    public ObservableCollection<TagBadgeViewModel> Tags { get; } = new();

    public string SearchableText => Title + " " + Body;

    public List<MasterTag> AvailableTagsToAdd
        => _owner.MasterTags.Where(t => !Model.Tags.Contains(t.Name)).ToList();

    public bool HasNoAvailableTags => AvailableTagsToAdd.Count == 0;

    /// <summary>メモカードViewModelの初期化。</summary>
    /// <param name="model">メモのモデル</param>
    /// <param name="owner">親のビューモデル</param>
    public MemoItemViewModel(MemoItem model, MemoViewModel owner)
    {
        Model = model;
        _owner = owner;
        _title = model.Title;
        _body = model.Body;
        _mode = model.Mode;
        _isPinned = model.IsPinned;
        RebuildTags();
    }

    /// <summary>本文変更時にMarkdownプレビューHTMLを再通知する。</summary>
    partial void OnBodyChanged(string value)
    {
        OnPropertyChanged(nameof(RenderedHtml));
    }

    /// <summary>モード変更時に関連プロパティの変更を通知する。</summary>
    partial void OnModeChanged(MemoMode value)
    {
        OnPropertyChanged(nameof(IsMarkdown));
        OnPropertyChanged(nameof(ShowMarkdownPreview));
        OnPropertyChanged(nameof(RenderedHtml));
    }

    /// <summary>プレビュー抑制状態変更時に表示可否を再通知する。</summary>
    partial void OnIsPreviewSuspendedChanged(bool value) => OnPropertyChanged(nameof(ShowMarkdownPreview));

    /// <summary>設定パネルでのテーマ/フォント変更をこのカードのMarkdownプレビューへ反映する。</summary>
    /// <returns>なし</returns>
    public void RefreshRenderedHtml() => OnPropertyChanged(nameof(RenderedHtml));

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

    /// <summary>テキストを指定行数までに切り詰め、超過時は末尾に「...」を付与する。</summary>
    private static string ClampToLines(string text, int maxLines)
    {
        var lines = text.Split('\n');
        if (lines.Length <= maxLines)
        {
            return text;
        }

        return string.Join('\n', lines.Take(maxLines)).TrimEnd() + "...";
    }

    /// <summary>ピン留め状態を切り替える。</summary>
    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
        _owner.TogglePin(this);
    }

    /// <summary>このメモを削除する。</summary>
    [RelayCommand]
    private void Delete() => _owner.DeleteItem(this);

    /// <summary>本文をクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopyToClipboard() => _owner.CopyItemToClipboard(this);

    /// <summary>編集ウィンドウを開く。</summary>
    [RelayCommand]
    private void OpenEdit() => _owner.RequestEdit(this);

    /// <summary>このメモをテンプレートとして保存する。</summary>
    [RelayCommand]
    private void SaveAsTemplate() => _owner.RequestSaveAsTemplate(this);

    /// <summary>タグ選択ピッカーを開く。</summary>
    [RelayCommand]
    private void OpenTagPicker() => IsTagPickerOpen = true;

    /// <summary>指定タグをこのメモへ付与する。</summary>
    [RelayCommand]
    private void AddTag(string tagName) => _owner.AddTagToItem(this, tagName);
}
