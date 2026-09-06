using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>メモの新規作成・編集用ウィンドウのViewModel。</summary>
public partial class MemoEditViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly List<string> _tagNames;

    [ObservableProperty]
    private string _windowTitle;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextMode))]
    private bool _isMarkdownMode;

    /// <summary>テキストモードが有効（マークダウンモードではない）かどうか。</summary>
    /// <returns>マークダウンモードが無効の場合true。</returns>
    public bool IsTextMode => !IsMarkdownMode;

    /// <summary>メモに付与されたタグのバッジ一覧。</summary>
    /// <returns>タグバッジの表示・管理を行う観測可能なコレクション。</returns>
    public ObservableCollection<TagBadgeViewModel> Tags { get; } = new();

    /// <summary>メモに追加可能な未使用タグ一覧。</summary>
    /// <returns>現在未使用のマスタータグリスト。</returns>
    public List<MasterTag> AvailableTagsToAdd
        => _settingsService.Current.MasterTags.Where(t => !_tagNames.Contains(t.Name)).ToList();

    /// <summary>メモに付与されたタグ名一覧。</summary>
    /// <returns>タグ名の読み取り専用リスト。</returns>
    public IReadOnlyList<string> TagNames => _tagNames;

    /// <summary>利用可能なフォントファミリー一覧。</summary>
    /// <returns>システムから取得したフォント名の読み取り専用リスト。</returns>
    public IReadOnlyList<string> AvailableFontFamilies => FontCatalog.FontFamilies;

    /// <summary>テキストモード編集部のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string TextFontFamily
    {
        get => _settingsService.Current.MemoTextFontFamily;
        set => SetFontSetting(value, _settingsService.Current.MemoTextFontFamily, v => _settingsService.Current.MemoTextFontFamily = v);
    }

    /// <summary>テキストモード編集部のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double TextFontSize
    {
        get => _settingsService.Current.MemoTextFontSize;
        set => SetFontSetting(value, _settingsService.Current.MemoTextFontSize, v => _settingsService.Current.MemoTextFontSize = v);
    }

    /// <summary>マークダウンエディット部のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string MarkdownEditFontFamily
    {
        get => _settingsService.Current.MemoMarkdownEditFontFamily;
        set => SetFontSetting(value, _settingsService.Current.MemoMarkdownEditFontFamily, v => _settingsService.Current.MemoMarkdownEditFontFamily = v);
    }

    /// <summary>マークダウンエディット部のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double MarkdownEditFontSize
    {
        get => _settingsService.Current.MemoMarkdownEditFontSize;
        set => SetFontSetting(value, _settingsService.Current.MemoMarkdownEditFontSize, v => _settingsService.Current.MemoMarkdownEditFontSize = v);
    }

    /// <summary>マークダウンビュー部のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string MarkdownViewFontFamily
    {
        get => _settingsService.Current.MemoMarkdownViewFontFamily;
        set => SetFontSetting(value, _settingsService.Current.MemoMarkdownViewFontFamily, v => _settingsService.Current.MemoMarkdownViewFontFamily = v);
    }

    /// <summary>マークダウンビュー部のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double MarkdownViewFontSize
    {
        get => _settingsService.Current.MemoMarkdownViewFontSize;
        set => SetFontSetting(value, _settingsService.Current.MemoMarkdownViewFontSize, v => _settingsService.Current.MemoMarkdownViewFontSize = v);
    }

    /// <summary>マークダウンコード部のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string MarkdownCodeFontFamily
    {
        get => _settingsService.Current.MemoMarkdownCodeFontFamily;
        set => SetFontSetting(value, _settingsService.Current.MemoMarkdownCodeFontFamily, v => _settingsService.Current.MemoMarkdownCodeFontFamily = v);
    }

    /// <summary>マークダウンコード部のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double MarkdownCodeFontSize
    {
        get => _settingsService.Current.MemoMarkdownCodeFontSize;
        set => SetFontSetting(value, _settingsService.Current.MemoMarkdownCodeFontSize, v => _settingsService.Current.MemoMarkdownCodeFontSize = v);
    }

    /// <summary>ダークモードが有効かどうか。</summary>
    /// <returns>ダークモード設定が有効の場合true。</returns>
    public bool IsDarkMode => _settingsService.Current.IsDarkMode;

    /// <summary>フォント設定値を変更し、保存とプロパティ変更通知を行う。</summary>
    private void SetFontSetting<T>(T value, T current, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (Equals(value, current))
        {
            return;
        }

        apply(value);
        _settingsService.Save();
        OnPropertyChanged(propertyName);
    }

    /// <summary>メモ編集ウィンドウ用ViewModelの初期化（新規作成・既存編集・ドラフト適用に対応）。</summary>
    /// <param name="settingsService">アプリ設定を管理するサービス。</param>
    /// <param name="existing">既存メモ（編集時は指定、新規作成時はnull）。</param>
    /// <param name="draft">テンプレートまたはドラフト（テンプレート適用時に指定）。</param>
    public MemoEditViewModel(SettingsService settingsService, MemoItemViewModel? existing, MemoDraft? draft = null)
    {
        _settingsService = settingsService;
        _tagNames = existing is not null
            ? new List<string>(existing.Model.Tags)
            : new List<string>(draft?.Tags ?? Array.Empty<string>());

        _windowTitle = LocalizationManager.Instance[existing is null ? "Memo_NewTitle" : "Memo_EditTitle"];

        if (existing is not null)
        {
            _title = existing.Title;
            _body = existing.Body;
            _isMarkdownMode = existing.Mode == MemoMode.Markdown;
        }
        else if (draft is not null)
        {
            _body = draft.Body;
            _isMarkdownMode = draft.IsMarkdown;
        }

        RebuildTags();
    }

    /// <summary>タグバッジ一覧を現在のタグ名から再構築する。</summary>
    private void RebuildTags()
    {
        Tags.Clear();
        foreach (string name in _tagNames)
        {
            var master = _settingsService.Current.MasterTags.FirstOrDefault(t => t.Name == name);
            Tags.Add(new TagBadgeViewModel(
                name,
                master?.ColorHex ?? "#607D8B",
                onClick: _ => { },
                onRemove: RemoveTag));
        }

        OnPropertyChanged(nameof(AvailableTagsToAdd));
    }

    /// <summary>タグを追加する。</summary>
    /// <param name="name">追加するタグ名。</param>
    public void AddTag(string name)
    {
        if (!_tagNames.Contains(name))
        {
            _tagNames.Add(name);
            RebuildTags();
        }
    }

    /// <summary>タグを解除する。</summary>
    /// <param name="name">解除するタグ名。</param>
    public void RemoveTag(string name)
    {
        _tagNames.Remove(name);
        RebuildTags();
    }
}
