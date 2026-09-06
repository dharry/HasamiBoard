using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>メモテンプレートの新規作成・編集用ウィンドウのViewModel。</summary>
public partial class MemoTemplateEditViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly List<string> _tagNames;

    [ObservableProperty]
    private string _windowTitle;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextMode))]
    private bool _isMarkdownMode;

    public bool IsTextMode => !IsMarkdownMode;

    public ObservableCollection<TagBadgeViewModel> Tags { get; } = new();

    public List<MasterTag> AvailableTagsToAdd
        => _settingsService.Current.MasterTags.Where(t => !_tagNames.Contains(t.Name)).ToList();

    public bool HasNoAvailableTags => AvailableTagsToAdd.Count == 0;

    public IReadOnlyList<string> TagNames => _tagNames;

    /// <summary>編集対象の既存テンプレート。null かつ Id が空の場合は新規作成。</summary>
    public MemoTemplate? Existing { get; }

    /// <summary>テンプレート編集ウィンドウ用ViewModelの初期化。</summary>
    /// <param name="settingsService">アプリ設定サービス</param>
    /// <param name="seed">初期値テンプレート (新規作成時はnull)</param>
    public MemoTemplateEditViewModel(SettingsService settingsService, MemoTemplate? seed)
    {
        _settingsService = settingsService;
        _tagNames = seed is null ? new List<string>() : new List<string>(seed.Tags);

        bool isEditingExisting = !string.IsNullOrEmpty(seed?.Id);
        Existing = isEditingExisting ? seed : null;
        _windowTitle = LocalizationManager.Instance[isEditingExisting ? "Template_EditTitle" : "Template_NewTitle"];

        if (seed is not null)
        {
            _name = seed.Name;
            _body = seed.Body;
            _isMarkdownMode = seed.Mode == MemoMode.Markdown;
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
        OnPropertyChanged(nameof(HasNoAvailableTags));
    }

    /// <summary>タグを追加する。</summary>
    /// <param name="name">追加するタグ名</param>
    /// <returns>なし</returns>
    public void AddTag(string name)
    {
        if (!_tagNames.Contains(name))
        {
            _tagNames.Add(name);
            RebuildTags();
        }
    }

    /// <summary>タグを解除する。</summary>
    /// <param name="name">削除するタグ名</param>
    /// <returns>なし</returns>
    public void RemoveTag(string name)
    {
        _tagNames.Remove(name);
        RebuildTags();
    }

    /// <summary>現在のタグ名一覧のコピーを取得する。</summary>
    /// <returns>タグ名一覧のコピー</returns>
    public List<string> BuildTagList() => new(_tagNames);
}
