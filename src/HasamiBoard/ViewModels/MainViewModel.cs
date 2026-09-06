using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>メインウィンドウ全体のタブ切替・設定パネル・クイックフィルターを統括するビューモデル。</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;

    /// <summary>現在選択中のタブインデックス。</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>ウィンドウを常に手前に表示するかどうか。</summary>
    [ObservableProperty]
    private bool _isTopmost;

    /// <summary>クイックフィルターバーを表示するかどうか。</summary>
    [ObservableProperty]
    private bool _isQuickFilterVisible;

    /// <summary>クイックフィルターの入力文字列。</summary>
    [ObservableProperty]
    private string _quickFilterText = string.Empty;

    /// <summary>設定パネルが開いているかどうか。</summary>
    [ObservableProperty]
    private bool _isSettingsPanelOpen;

    /// <summary>ダークモードが有効かどうか。</summary>
    [ObservableProperty]
    private bool _isDarkMode;

    /// <summary>現在の表示言語設定。</summary>
    [ObservableProperty]
    private string _language;

    /// <summary>クリップボード履歴タブが有効かどうか。</summary>
    [ObservableProperty]
    private bool _isHistoryTabEnabled;

    /// <summary>スクリーンショットタブが有効かどうか。</summary>
    [ObservableProperty]
    private bool _isScreenshotTabEnabled;

    /// <summary>メモタブが有効かどうか。</summary>
    [ObservableProperty]
    private bool _isMemoTabEnabled;

    /// <summary>開発ツール機能 (🔁) が有効かどうか。</summary>
    [ObservableProperty]
    private bool _isConverterEnabled;

    /// <summary>クリップボード履歴タブのビューモデル。</summary>
    /// <returns>クリップボード履歴の状態と操作を管理するビューモデル。</returns>
    public ClipboardHistoryViewModel ClipboardHistory { get; }

    /// <summary>スクリーンショットタブのビューモデル。</summary>
    /// <returns>スクリーンショット履歴の状態と操作を管理するビューモデル。</returns>
    public ScreenshotViewModel Screenshot { get; }

    /// <summary>メモタブのビューモデル。</summary>
    /// <returns>メモ一覧の状態と操作を管理するビューモデル。</returns>
    public MemoViewModel Memo { get; }

    /// <summary>設定パネルのビューモデル。</summary>
    /// <returns>各種設定の状態と操作を管理するビューモデル。</returns>
    public SettingsViewModel Settings { get; }

    /// <summary>現在のタブのレコード数/保持数上限を表示する文字列 (例: "レコード: 12/100")。</summary>
    /// <returns>フォーマットされたレコード数表示文字列。</returns>
    public string RecordCountText
    {
        get
        {
            string format = LocalizationManager.Instance["RecordCount_Format"];
            return SelectedTabIndex switch
            {
                0 => string.Format(format, ClipboardHistory.TotalCount, ClipboardHistory.MaxCount),
                1 => string.Format(format, Screenshot.TotalCount, Screenshot.MaxCount),
                2 => string.Format(format, Memo.TotalCount, "∞"),
                _ => string.Empty,
            };
        }
    }

    /// <summary>子ビューモデルと設定を受け取り、初期状態とイベント連携を構築する。</summary>
    /// <param name="settingsService">アプリ設定を管理するサービス。</param>
    /// <param name="themeService">テーマ（ダーク/ライト）を適用するサービス。</param>
    /// <param name="clipboardHistoryViewModel">クリップボード履歴ビューモデル。</param>
    /// <param name="screenshotViewModel">スクリーンショット履歴ビューモデル。</param>
    /// <param name="memoViewModel">メモ一覧ビューモデル。</param>
    /// <param name="settingsViewModel">設定パネルビューモデル。</param>
    public MainViewModel(
        SettingsService settingsService,
        ThemeService themeService,
        ClipboardHistoryViewModel clipboardHistoryViewModel,
        ScreenshotViewModel screenshotViewModel,
        MemoViewModel memoViewModel,
        SettingsViewModel settingsViewModel)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        ClipboardHistory = clipboardHistoryViewModel;
        Screenshot = screenshotViewModel;
        Memo = memoViewModel;
        Settings = settingsViewModel;

        var settings = _settingsService.Current;
        _selectedTabIndex = settings.SelectedTabIndex;
        _isTopmost = settings.IsTopmost;
        _isQuickFilterVisible = settings.IsQuickFilterVisible;
        _isDarkMode = settings.IsDarkMode;
        _language = settings.Language;
        _isHistoryTabEnabled = settings.IsHistoryEnabled;
        _isScreenshotTabEnabled = settings.IsScreenshotEnabled;
        _isMemoTabEnabled = settings.IsMemoEnabled;
        _isConverterEnabled = settings.IsConverterEnabled;

        ClipboardHistory.PropertyChanged += (_, e) =>
        {
            // RefreshDisplaySettings (保持数上限スライダー変更時等) は string.Empty で
            // 「全プロパティ変更」を通知するため、TotalCount/MaxCount の変更もこれに含める。
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName is nameof(ClipboardHistoryViewModel.TotalCount) or nameof(ClipboardHistoryViewModel.MaxCount))
            {
                OnPropertyChanged(nameof(RecordCountText));
            }
        };
        Screenshot.PropertyChanged += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName is nameof(ScreenshotViewModel.TotalCount) or nameof(ScreenshotViewModel.MaxCount))
            {
                OnPropertyChanged(nameof(RecordCountText));
            }
        };
        Memo.PropertyChanged += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(MemoViewModel.TotalCount))
            {
                OnPropertyChanged(nameof(RecordCountText));
            }
        };
    }

    /// <summary>タブ切替時にレコード数表示を更新する。</summary>
    partial void OnSelectedTabIndexChanged(int value) => OnPropertyChanged(nameof(RecordCountText));

    /// <summary>文字列で指定されたインデックスのタブへ切り替える。</summary>
    [RelayCommand]
    private void SelectTab(string indexText)
    {
        if (int.TryParse(indexText, out int index))
        {
            SelectedTabIndex = index;
            _settingsService.Current.SelectedTabIndex = index;
            _settingsService.Save();
        }
    }

    /// <summary>ウィンドウの常に手前に表示設定を切り替える。</summary>
    [RelayCommand]
    private void ToggleTopmost()
    {
        IsTopmost = !IsTopmost;
        _settingsService.Current.IsTopmost = IsTopmost;
        _settingsService.Save();
    }

    /// <summary>設定パネルの開閉を切り替える。</summary>
    [RelayCommand]
    private void ToggleSettingsPanel()
    {
        IsSettingsPanelOpen = !IsSettingsPanelOpen;
    }

    /// <summary>設定パネル開閉時にWebView2プレビューの表示/非表示を切り替える。</summary>
    partial void OnIsSettingsPanelOpenChanged(bool value)
    {
        // WebView2 はネイティブ描画領域を持ち、設定パネルのオーバーレイより手前に
        // 描画されてしまう (いわゆる airspace 問題) ため、パネルを開いている間は
        // メモ一覧のMarkdownプレビュー(WebView2)を一時的に隠す。
        Memo.SetPreviewsSuspended(value);
    }

    /// <summary>タグバッジクリック時に "#タグ名" をクイックフィルターへ追加/解除する。</summary>
    /// <param name="tagName">トグルするタグ名（#プレフィックスなし）。</param>
    public void ToggleQuickFilterToken(string tagName)
    {
        string token = "#" + tagName;
        var tokens = QuickFilterText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        int existingIndex = tokens.FindIndex(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            tokens.RemoveAt(existingIndex);
        }
        else
        {
            tokens.Add(token);
        }

        QuickFilterText = string.Join(' ', tokens);
    }

    /// <summary>クイックフィルターの入力内容をクリアする。</summary>
    [RelayCommand]
    private void ClearQuickFilter()
    {
        QuickFilterText = string.Empty;
    }

    /// <summary>設定リセット等で外部から状態を再読込する。</summary>
    public void ReloadFromSettings()
    {
        var settings = _settingsService.Current;
        IsDarkMode = settings.IsDarkMode;
        Language = settings.Language;
        IsQuickFilterVisible = settings.IsQuickFilterVisible;
        IsHistoryTabEnabled = settings.IsHistoryEnabled;
        IsScreenshotTabEnabled = settings.IsScreenshotEnabled;
        IsMemoTabEnabled = settings.IsMemoEnabled;
        IsConverterEnabled = settings.IsConverterEnabled;
    }

    /// <summary>クイックフィルター文字列変更時に各タブへフィルターを適用する。</summary>
    partial void OnQuickFilterTextChanged(string value)
    {
        ClipboardHistory.ApplyFilter(value);
        Screenshot.ApplyFilter(value);
        Memo.ApplyFilter(value);
    }

    /// <summary>ダークモード変更時に設定を保存しテーマを適用する。</summary>
    partial void OnIsDarkModeChanged(bool value)
    {
        _settingsService.Current.IsDarkMode = value;
        _settingsService.Save();
        _themeService.ApplyTheme(value);
    }

    /// <summary>表示言語変更時に設定を保存し多言語リソースへ反映する。</summary>
    partial void OnLanguageChanged(string value)
    {
        _settingsService.Current.Language = value;
        _settingsService.Save();
        LocalizationManager.Instance.ApplyLanguage(value);
    }

    /// <summary>クイックフィルター表示設定変更時に設定を保存する。</summary>
    partial void OnIsQuickFilterVisibleChanged(bool value)
    {
        _settingsService.Current.IsQuickFilterVisible = value;
        _settingsService.Save();
    }

    /// <summary>クリップボード履歴タブ有効設定変更時に設定を保存する。</summary>
    partial void OnIsHistoryTabEnabledChanged(bool value)
    {
        _settingsService.Current.IsHistoryEnabled = value;
        _settingsService.Save();
        EnsureSelectedTabIsEnabled();
    }

    /// <summary>スクリーンショットタブ有効設定変更時に設定を保存する。</summary>
    partial void OnIsScreenshotTabEnabledChanged(bool value)
    {
        _settingsService.Current.IsScreenshotEnabled = value;
        _settingsService.Save();
        EnsureSelectedTabIsEnabled();
    }

    /// <summary>メモタブ有効設定変更時に設定を保存する。</summary>
    partial void OnIsMemoTabEnabledChanged(bool value)
    {
        _settingsService.Current.IsMemoEnabled = value;
        _settingsService.Save();
        EnsureSelectedTabIsEnabled();
    }

    /// <summary>開発ツール機能有効設定変更時に設定を保存する。</summary>
    partial void OnIsConverterEnabledChanged(bool value)
    {
        _settingsService.Current.IsConverterEnabled = value;
        _settingsService.Save();
    }

    /// <summary>選択中タブが無効化されていた場合、有効な別タブへ切り替える。</summary>
    private void EnsureSelectedTabIsEnabled()
    {
        bool[] enabled = { IsHistoryTabEnabled, IsScreenshotTabEnabled, IsMemoTabEnabled };
        if (SelectedTabIndex >= 0 && SelectedTabIndex < enabled.Length && enabled[SelectedTabIndex])
        {
            return;
        }

        for (int i = 0; i < enabled.Length; i++)
        {
            if (enabled[i])
            {
                SelectedTabIndex = i;
                return;
            }
        }
    }
}
