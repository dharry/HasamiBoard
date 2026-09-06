using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>エラーメッセージ一覧ウィンドウの表示・検索・フォント設定を管理するビューモデル。</summary>
public partial class ErrorListViewModel : ObservableObject
{
    /// <summary>常に手前に表示するかどうか。</summary>
    [ObservableProperty]
    private bool _isTopmost;

    /// <summary>現在選択中のエラーコード体系。</summary>
    [ObservableProperty]
    private ErrorListMode _selectedMode = ErrorListMode.Http;

    /// <summary>現在選択中の表示言語。</summary>
    [ObservableProperty]
    private ErrorListLanguage _selectedLanguage = ErrorListLanguage.En;

    /// <summary>検索ボックスの入力文字列。</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>フィルター済みエラーコード一覧。</summary>
    /// <returns>条件に合致したエラーコードエントリのコレクション。</returns>
    public ObservableCollection<ErrorCodeEntry> FilteredEntries { get; } = new();

    // フォント設定 (UI部 / 一覧結果表示部)。設定パネルからだけでなく、このウィンドウ自身の
    // 🔤 フォント設定ポップアップからも変更でき、変更は即座に反映・永続化される。
    /// <summary>利用可能なフォントファミリー一覧。</summary>
    /// <returns>システムから取得したフォント名の読み取り専用リスト。</returns>
    public IReadOnlyList<string> AvailableFontFamilies => FontCatalog.FontFamilies;

    /// <summary>エラーリストUI部のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string UiFontFamily
    {
        get => App.SettingsService.Current.ErrorListUiFontFamily;
        set => SetFontSetting(value, App.SettingsService.Current.ErrorListUiFontFamily, v => App.SettingsService.Current.ErrorListUiFontFamily = v);
    }

    /// <summary>エラーリストUI部のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double UiFontSize
    {
        get => App.SettingsService.Current.ErrorListUiFontSize;
        set => SetFontSetting(value, App.SettingsService.Current.ErrorListUiFontSize, v => App.SettingsService.Current.ErrorListUiFontSize = v);
    }

    /// <summary>エラーリスト検索結果表示のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string EditFontFamily
    {
        get => App.SettingsService.Current.ErrorListEditFontFamily;
        set => SetFontSetting(value, App.SettingsService.Current.ErrorListEditFontFamily, v => App.SettingsService.Current.ErrorListEditFontFamily = v);
    }

    /// <summary>エラーリスト検索結果表示のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double EditFontSize
    {
        get => App.SettingsService.Current.ErrorListEditFontSize;
        set => SetFontSetting(value, App.SettingsService.Current.ErrorListEditFontSize, v => App.SettingsService.Current.ErrorListEditFontSize = v);
    }

    /// <summary>値が変化した場合のみフォント設定を適用・保存する。</summary>
    private void SetFontSetting<T>(T value, T current, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (Equals(value, current))
        {
            return;
        }

        apply(value);
        App.SettingsService.Save();
        OnPropertyChanged(propertyName);
    }

    /// <summary>保存済み設定から前回の表示状態を復元する。</summary>
    public ErrorListViewModel()
    {
        var settings = App.SettingsService.Current;
        IsTopmost = settings.IsErrorListTopmost;

        if (Enum.TryParse<ErrorListMode>(settings.ErrorListSelectedMode, out var mode))
        {
            SelectedMode = mode;
        }

        if (Enum.TryParse<ErrorListLanguage>(settings.ErrorListSelectedLanguage, out var language))
        {
            SelectedLanguage = language;
        }

        RefreshEntries();
    }

    /// <summary>現在選択中のカテゴリ/表示言語を設定へ保存する。ウィンドウを閉じる際に呼び出す。</summary>
    public void PersistState()
    {
        var settings = App.SettingsService.Current;
        settings.ErrorListSelectedMode = SelectedMode.ToString();
        settings.ErrorListSelectedLanguage = SelectedLanguage.ToString();
    }

    /// <summary>常に手前に表示する設定を切り替える。</summary>
    [RelayCommand]
    private void ToggleTopmost()
    {
        IsTopmost = !IsTopmost;
        App.SettingsService.Current.IsErrorListTopmost = IsTopmost;
        App.SettingsService.Save();
    }

    /// <summary>文字列で指定されたモードへ切り替える。</summary>
    [RelayCommand]
    private void SelectMode(string modeName)
    {
        if (Enum.TryParse<ErrorListMode>(modeName, out var mode))
        {
            SelectedMode = mode;
        }
    }

    /// <summary>文字列で指定された表示言語へ切り替える。</summary>
    [RelayCommand]
    private void SelectLanguage(string languageName)
    {
        if (Enum.TryParse<ErrorListLanguage>(languageName, out var language))
        {
            SelectedLanguage = language;
        }
    }

    /// <summary>指定したエラーコードをJSON形式でクリップボードへコピーする。</summary>
    [RelayCommand]
    private void CopyEntryAsJson(ErrorCodeEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        Clipboard.SetText(ErrorCodeService.ToJson(entry, SelectedLanguage == ErrorListLanguage.Ja));
    }

    /// <summary>選択モード変更時に一覧を再抽出する。</summary>
    partial void OnSelectedModeChanged(ErrorListMode value) => RefreshEntries();

    /// <summary>検索文字列変更時に一覧を再抽出する。</summary>
    partial void OnSearchTextChanged(string value) => RefreshEntries();

    /// <summary>現在のモードと検索文字列に基づき一覧を再構築する。</summary>
    private void RefreshEntries()
    {
        var source = SelectedMode switch
        {
            ErrorListMode.Http => ErrorCodeService.HttpStatusCodes,
            ErrorListMode.Errno => ErrorCodeService.ErrnoCodes,
            ErrorListMode.WindowsError => ErrorCodeService.WindowsErrorCodes,
            ErrorListMode.Smtp => ErrorCodeService.SmtpCodes,
            _ => ErrorCodeService.HttpStatusCodes,
        };

        var filtered = ErrorCodeService.Search(source, SearchText);

        FilteredEntries.Clear();
        foreach (var entry in filtered)
        {
            FilteredEntries.Add(entry);
        }
    }
}
