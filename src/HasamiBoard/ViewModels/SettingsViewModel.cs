using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.ViewModels;

/// <summary>⚙️ 設定パネル全体の状態と各種設定項目の保存・反映を管理するViewModel。</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly StartupRegistryService _startupRegistryService;
    private readonly ClipboardHistoryViewModel _clipboardHistory;
    private readonly ScreenshotViewModel _screenshot;
    private readonly MemoViewModel _memo;
    private readonly Action _reloadMainViewModel;
    private readonly Action _refreshHotkeys;
    private readonly Action _prepareForRestore;
    private readonly Action _exitApplication;
    private readonly BackupService _backupService = new();

    private bool _isLoading;

    [ObservableProperty] private int _settingsTabIndex;

    [ObservableProperty] private double _itemMaxHeight;
    [ObservableProperty] private double _screenshotThumbnailHeight;
    [ObservableProperty] private int _maxHistoryCount;
    [ObservableProperty] private int _maxScreenshotHistoryCount;
    [ObservableProperty] private bool _isAutoStart;

    [ObservableProperty] private string _screenshotImageFormat = "png";
    [ObservableProperty] private int _screenshotJpegQuality;
    [ObservableProperty] private int _screenshotWebpQuality;
    [ObservableProperty] private bool _screenshotWebpLossless;
    [ObservableProperty] private bool _isDominantColorExtractionEnabled;

    [ObservableProperty] private bool _isScreenshotHotkeyEnabled;
    [ObservableProperty] private string _screenshotHotkeyModifiers = "Ctrl+Shift";
    [ObservableProperty] private string _screenshotHotkeyKey = "S";

    [ObservableProperty] private bool _isColorPickerHotkeyEnabled;
    [ObservableProperty] private string _colorPickerHotkeyModifiers = "Ctrl+Shift";
    [ObservableProperty] private string _colorPickerHotkeyKey = "C";

    [ObservableProperty] private bool _isConverterHotkeyEnabled;
    [ObservableProperty] private string _converterHotkeyModifiers = "Ctrl+Shift";
    [ObservableProperty] private string _converterHotkeyKey = "T";

    [ObservableProperty] private bool _isErrorListHotkeyEnabled;
    [ObservableProperty] private string _errorListHotkeyModifiers = "Ctrl+Shift";
    [ObservableProperty] private string _errorListHotkeyKey = "E";

    [ObservableProperty] private string _newTagName = string.Empty;

    [ObservableProperty] private string _historyFontFamily = "Consolas";
    [ObservableProperty] private double _historyFontSize;
    [ObservableProperty] private string _screenshotMemoFontFamily = "Consolas";
    [ObservableProperty] private double _screenshotMemoFontSize;
    [ObservableProperty] private string _memoTextFontFamily = "Consolas";
    [ObservableProperty] private double _memoTextFontSize;
    [ObservableProperty] private string _memoMarkdownEditFontFamily = "Consolas";
    [ObservableProperty] private double _memoMarkdownEditFontSize;
    [ObservableProperty] private string _memoMarkdownViewFontFamily = "Segoe UI";
    [ObservableProperty] private double _memoMarkdownViewFontSize;
    [ObservableProperty] private string _memoMarkdownCodeFontFamily = "Consolas";
    [ObservableProperty] private double _memoMarkdownCodeFontSize;

    [ObservableProperty] private string _converterUiFontFamily = "Segoe UI";
    [ObservableProperty] private double _converterUiFontSize;
    [ObservableProperty] private string _converterEditFontFamily = "Consolas";
    [ObservableProperty] private double _converterEditFontSize;

    [ObservableProperty] private string _errorListUiFontFamily = "Segoe UI";
    [ObservableProperty] private double _errorListUiFontSize;
    [ObservableProperty] private string _errorListEditFontFamily = "Consolas";
    [ObservableProperty] private double _errorListEditFontSize;

    public IReadOnlyList<string> AvailableFontFamilies => FontCatalog.FontFamilies;

    public string AppVersion => "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?");

    public ObservableCollection<MasterTagEditViewModel> MasterTags { get; } = new();

    public ObservableCollection<ConverterModeCategoryGroup> ConverterModeCategoryGroups { get; } = new();

    public bool IsPngFormat => ScreenshotImageFormat == "png";
    public bool IsJpegFormat => ScreenshotImageFormat == "jpg";
    public bool IsWebpFormat => ScreenshotImageFormat == "webp";

    /// <summary>設定ViewModelの初期化と現在設定の読み込み。</summary>
    public SettingsViewModel(
        SettingsService settingsService,
        StartupRegistryService startupRegistryService,
        ClipboardHistoryViewModel clipboardHistory,
        ScreenshotViewModel screenshot,
        MemoViewModel memo,
        Action reloadMainViewModel,
        Action refreshHotkeys,
        Action prepareForRestore,
        Action exitApplication)
    {
        _settingsService = settingsService;
        _startupRegistryService = startupRegistryService;
        _clipboardHistory = clipboardHistory;
        _screenshot = screenshot;
        _memo = memo;
        _reloadMainViewModel = reloadMainViewModel;
        _refreshHotkeys = refreshHotkeys;
        _prepareForRestore = prepareForRestore;
        _exitApplication = exitApplication;

        LoadFromSettings();
    }

    /// <summary>永続化された設定値を各プロパティへ読み込む。</summary>
    private void LoadFromSettings()
    {
        _isLoading = true;
        var s = _settingsService.Current;

        ItemMaxHeight = s.ItemMaxHeight;
        ScreenshotThumbnailHeight = s.ScreenshotThumbnailHeight;
        MaxHistoryCount = s.MaxHistoryCount;
        MaxScreenshotHistoryCount = s.MaxScreenshotHistoryCount;
        IsAutoStart = s.IsAutoStart;

        ScreenshotImageFormat = s.ScreenshotImageFormat;
        ScreenshotJpegQuality = s.ScreenshotJpegQuality;
        ScreenshotWebpQuality = s.ScreenshotWebpQuality;
        ScreenshotWebpLossless = s.ScreenshotWebpLossless;
        IsDominantColorExtractionEnabled = s.IsDominantColorExtractionEnabled;

        IsScreenshotHotkeyEnabled = s.IsScreenshotHotkeyEnabled;
        ScreenshotHotkeyModifiers = s.ScreenshotHotkeyModifiers;
        ScreenshotHotkeyKey = s.ScreenshotHotkeyKey;

        IsColorPickerHotkeyEnabled = s.IsColorPickerHotkeyEnabled;
        ColorPickerHotkeyModifiers = s.ColorPickerHotkeyModifiers;
        ColorPickerHotkeyKey = s.ColorPickerHotkeyKey;

        IsConverterHotkeyEnabled = s.IsConverterHotkeyEnabled;
        ConverterHotkeyModifiers = s.ConverterHotkeyModifiers;
        ConverterHotkeyKey = s.ConverterHotkeyKey;

        IsErrorListHotkeyEnabled = s.IsErrorListHotkeyEnabled;
        ErrorListHotkeyModifiers = s.ErrorListHotkeyModifiers;
        ErrorListHotkeyKey = s.ErrorListHotkeyKey;

        HistoryFontFamily = s.HistoryFontFamily;
        HistoryFontSize = s.HistoryFontSize;
        ScreenshotMemoFontFamily = s.ScreenshotMemoFontFamily;
        ScreenshotMemoFontSize = s.ScreenshotMemoFontSize;
        MemoTextFontFamily = s.MemoTextFontFamily;
        MemoTextFontSize = s.MemoTextFontSize;
        MemoMarkdownEditFontFamily = s.MemoMarkdownEditFontFamily;
        MemoMarkdownEditFontSize = s.MemoMarkdownEditFontSize;
        MemoMarkdownViewFontFamily = s.MemoMarkdownViewFontFamily;
        MemoMarkdownViewFontSize = s.MemoMarkdownViewFontSize;
        MemoMarkdownCodeFontFamily = s.MemoMarkdownCodeFontFamily;
        MemoMarkdownCodeFontSize = s.MemoMarkdownCodeFontSize;

        ConverterUiFontFamily = s.ConverterUiFontFamily;
        ConverterUiFontSize = s.ConverterUiFontSize;
        ConverterEditFontFamily = s.ConverterEditFontFamily;
        ConverterEditFontSize = s.ConverterEditFontSize;

        ErrorListUiFontFamily = s.ErrorListUiFontFamily;
        ErrorListUiFontSize = s.ErrorListUiFontSize;
        ErrorListEditFontFamily = s.ErrorListEditFontFamily;
        ErrorListEditFontSize = s.ErrorListEditFontSize;

        RebuildMasterTags();
        RebuildConverterModeCategoryGroups();

        _isLoading = false;
    }

    /// <summary>マスタータグ編集行の一覧を現在の設定から再構築する。</summary>
    private void RebuildMasterTags()
    {
        MasterTags.Clear();
        foreach (var tag in _settingsService.Current.MasterTags)
        {
            MasterTags.Add(new MasterTagEditViewModel(tag.Name, tag.ColorHex, OnTagColorChanged, OnTagDeleted, OnTagRenamed));
        }
    }

    /// <summary>開発ツールの各モードのローカライズ済み表示名 (サイドバーと同じキーを使用)。</summary>
    private static readonly (ConverterMode Mode, string ResourceKey)[] ConverterModeLabels =
    {
        (ConverterMode.Hash, "Converter_Mode_Hash"),
        (ConverterMode.Base64, "Converter_Mode_Base64"),
        (ConverterMode.Url, "Converter_Mode_Url"),
        (ConverterMode.Html, "Converter_Mode_Html"),
        (ConverterMode.Json, "Converter_Mode_Json"),
        (ConverterMode.Xml, "Converter_Mode_Xml"),
        (ConverterMode.HtmlFormat, "Converter_Mode_HtmlFormat"),
        (ConverterMode.Css, "Converter_Mode_Css"),
        (ConverterMode.JavaScript, "Converter_Mode_JavaScript"),
        (ConverterMode.Sql, "Converter_Mode_Sql"),
        (ConverterMode.Timestamp, "Converter_Mode_Timestamp"),
        (ConverterMode.Uuid, "Converter_Mode_Uuid"),
        (ConverterMode.Password, "Converter_Mode_Password"),
        (ConverterMode.RegexTester, "Converter_Mode_RegexTester"),
        (ConverterMode.JwtDecoder, "Converter_Mode_JwtDecoder"),
        (ConverterMode.CaseConverter, "Converter_Mode_CaseConverter"),
        (ConverterMode.NumberBaseConverter, "Converter_Mode_NumberBaseConverter"),
        (ConverterMode.QrCode, "Converter_Mode_QrCode"),
        (ConverterMode.CronParser, "Converter_Mode_CronParser"),
        (ConverterMode.BasicAuth, "Converter_Mode_BasicAuth"),
        (ConverterMode.Jq, "Converter_Mode_Jq"),
    };

    /// <summary>開発ツールのモードカテゴリ分類 (開発ツールのサイドバー(<c>ConverterWindow.xaml</c>)と同じグルーピング・見出しキーを使用)。
    /// サイドバー側のカテゴリ構成を変更した場合は、こちらも合わせて更新すること。</summary>
    private static readonly (string CategoryKey, string HeaderResourceKey, ConverterMode[] Modes)[] ConverterModeCategoryDefinitions =
    {
        ("EncodeDecode", "ConverterCategory_EncodeDecode", new[] { ConverterMode.Hash, ConverterMode.Base64, ConverterMode.Url, ConverterMode.Html, ConverterMode.JwtDecoder }),
        ("Formatter", "ConverterCategory_Formatter", new[] { ConverterMode.Json, ConverterMode.Xml, ConverterMode.HtmlFormat, ConverterMode.Css, ConverterMode.JavaScript, ConverterMode.Sql, ConverterMode.Jq }),
        ("DateTime", "ConverterCategory_DateTime", new[] { ConverterMode.Timestamp, ConverterMode.CronParser }),
        ("Generator", "ConverterCategory_Generator", new[] { ConverterMode.Uuid, ConverterMode.Password, ConverterMode.QrCode, ConverterMode.BasicAuth }),
        ("TextAnalysis", "ConverterCategory_TextAnalysis", new[] { ConverterMode.RegexTester, ConverterMode.CaseConverter, ConverterMode.NumberBaseConverter }),
    };

    /// <summary>開発ツールのモード有効/無効チェックボックス一覧を、カテゴリ別に現在の設定 (並び順含む) から再構築する。</summary>
    private void RebuildConverterModeCategoryGroups()
    {
        var disabled = _settingsService.Current.ConverterDisabledModes;
        var savedOrder = _settingsService.Current.ConverterModeOrder;

        var remaining = new List<(ConverterMode Mode, string ResourceKey)>(ConverterModeLabels);
        var ordered = new List<(ConverterMode Mode, string ResourceKey)>();
        foreach (var modeName in savedOrder)
        {
            int index = remaining.FindIndex(entry => entry.Mode.ToString() == modeName);
            if (index >= 0)
            {
                ordered.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
        }
        ordered.AddRange(remaining);

        ConverterModeCategoryGroups.Clear();
        foreach (var (_, headerResourceKey, categoryModes) in ConverterModeCategoryDefinitions)
        {
            var group = new ConverterModeCategoryGroup(LocalizationManager.Instance[headerResourceKey]);
            foreach (var (mode, resourceKey) in ordered.Where(entry => categoryModes.Contains(entry.Mode)))
            {
                string modeName = mode.ToString();
                group.Items.Add(new ConverterModeToggleItem(
                    modeName,
                    LocalizationManager.Instance[resourceKey],
                    !disabled.Contains(modeName),
                    OnConverterModeToggleChanged,
                    OnConverterModeMoveUp,
                    OnConverterModeMoveDown));
            }

            ConverterModeCategoryGroups.Add(group);
        }
    }

    /// <summary>指定したチェックボックス項目が属するカテゴリグループを探す。</summary>
    private ConverterModeCategoryGroup? FindContainingCategoryGroup(ConverterModeToggleItem item)
        => ConverterModeCategoryGroups.FirstOrDefault(group => group.Items.Contains(item));

    /// <summary>開発ツールのモードを、所属カテゴリ内で1つ上へ移動し、並び順を保存する。</summary>
    private void OnConverterModeMoveUp(ConverterModeToggleItem item)
    {
        var group = FindContainingCategoryGroup(item);
        if (group is null)
        {
            return;
        }

        int index = group.Items.IndexOf(item);
        if (index <= 0)
        {
            return;
        }

        group.Items.Move(index, index - 1);
        PersistConverterModeOrder();
    }

    /// <summary>開発ツールのモードを、所属カテゴリ内で1つ下へ移動し、並び順を保存する。</summary>
    private void OnConverterModeMoveDown(ConverterModeToggleItem item)
    {
        var group = FindContainingCategoryGroup(item);
        if (group is null)
        {
            return;
        }

        int index = group.Items.IndexOf(item);
        if (index < 0 || index >= group.Items.Count - 1)
        {
            return;
        }

        group.Items.Move(index + 1, index);
        PersistConverterModeOrder();
    }

    /// <summary>現在の全カテゴリのチェックボックス一覧を1本の並び順へ連結し、設定へ保存する。</summary>
    private void PersistConverterModeOrder()
    {
        _settingsService.Current.ConverterModeOrder = ConverterModeCategoryGroups
            .SelectMany(group => group.Items)
            .Select(item => item.ModeName)
            .ToList();
        Save();
    }

    /// <summary>開発ツールのモード有効/無効切替時に設定を保存する。</summary>
    private void OnConverterModeToggleChanged(string modeName, bool isEnabled)
    {
        if (_isLoading)
        {
            return;
        }

        var allItems = ConverterModeCategoryGroups.SelectMany(group => group.Items).ToList();

        // 開発ツールのサイドバーが空にならないよう、最後の1件は無効化させない
        if (!isEnabled && !allItems.Any(item => item.IsEnabled))
        {
            var lastItem = allItems.FirstOrDefault(item => item.ModeName == modeName);
            if (lastItem is not null)
            {
                lastItem.IsEnabled = true;
            }

            return;
        }

        var disabled = _settingsService.Current.ConverterDisabledModes;
        if (isEnabled)
        {
            disabled.Remove(modeName);
        }
        else if (!disabled.Contains(modeName))
        {
            disabled.Add(modeName);
        }

        Save();
    }

    /// <summary>設定を永続化保存する。</summary>
    private void Save() => _settingsService.Save();

    /// <summary>表示テキストエリア高さ変更時に設定を保存し表示へ反映する。</summary>
    partial void OnItemMaxHeightChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.ItemMaxHeight = value;
        Save();
        _clipboardHistory.RefreshDisplaySettings();
        _memo.RefreshDisplaySettings();
    }

    /// <summary>サムネイル高さ変更時に設定を保存し表示へ反映する。</summary>
    partial void OnScreenshotThumbnailHeightChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotThumbnailHeight = value;
        Save();
        _screenshot.RefreshDisplaySettings();
    }

    /// <summary>クリップボード履歴保持数上限変更時に設定を保存し反映する。</summary>
    partial void OnMaxHistoryCountChanged(int value)
    {
        if (_isLoading) return;
        _settingsService.Current.MaxHistoryCount = value;
        Save();
        _clipboardHistory.RefreshDisplaySettings();
    }

    /// <summary>スクリーンショット保持数上限変更時に設定を保存し反映する。</summary>
    partial void OnMaxScreenshotHistoryCountChanged(int value)
    {
        if (_isLoading) return;
        _settingsService.Current.MaxScreenshotHistoryCount = value;
        Save();
        _screenshot.RefreshDisplaySettings();
    }

    /// <summary>自動起動設定変更時にレジストリへ同期する。</summary>
    partial void OnIsAutoStartChanged(bool value)
    {
        if (_isLoading) return;
        _settingsService.Current.IsAutoStart = value;
        Save();
        _startupRegistryService.SetEnabled(value);
    }

    /// <summary>スクリーンショット保存画像形式変更時に設定を保存し関連プロパティを通知する。</summary>
    partial void OnScreenshotImageFormatChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotImageFormat = value;
        Save();
        OnPropertyChanged(nameof(IsPngFormat));
        OnPropertyChanged(nameof(IsJpegFormat));
        OnPropertyChanged(nameof(IsWebpFormat));
    }

    /// <summary>JPEG画質設定変更時に設定を保存する。</summary>
    partial void OnScreenshotJpegQualityChanged(int value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotJpegQuality = value;
        Save();
    }

    /// <summary>WebP画質設定変更時に設定を保存する。</summary>
    partial void OnScreenshotWebpQualityChanged(int value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotWebpQuality = value;
        Save();
    }

    /// <summary>WebP可逆圧縮設定変更時に設定を保存する。</summary>
    partial void OnScreenshotWebpLosslessChanged(bool value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotWebpLossless = value;
        Save();
    }

    /// <summary>主要色自動抽出設定変更時に設定を保存する。</summary>
    partial void OnIsDominantColorExtractionEnabledChanged(bool value)
    {
        if (_isLoading) return;
        _settingsService.Current.IsDominantColorExtractionEnabled = value;
        Save();
    }

    /// <summary>スクリーンショットホットキー有効設定変更時に保存しホットキーを再登録する。</summary>
    partial void OnIsScreenshotHotkeyEnabledChanged(bool value)
    {
        if (_isLoading) return;
        _settingsService.Current.IsScreenshotHotkeyEnabled = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>スクリーンショットホットキー修飾キー変更時に保存しホットキーを再登録する。</summary>
    partial void OnScreenshotHotkeyModifiersChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotHotkeyModifiers = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>スクリーンショットホットキーキー変更時に保存しホットキーを再登録する。</summary>
    partial void OnScreenshotHotkeyKeyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotHotkeyKey = value.ToUpperInvariant();
        Save();
        _refreshHotkeys();
    }

    /// <summary>カラーピッカーホットキー有効設定変更時に保存しホットキーを再登録する。</summary>
    partial void OnIsColorPickerHotkeyEnabledChanged(bool value)
    {
        if (_isLoading) return;
        _settingsService.Current.IsColorPickerHotkeyEnabled = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>カラーピッカーホットキー修飾キー変更時に保存しホットキーを再登録する。</summary>
    partial void OnColorPickerHotkeyModifiersChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ColorPickerHotkeyModifiers = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>カラーピッカーホットキーキー変更時に保存しホットキーを再登録する。</summary>
    partial void OnColorPickerHotkeyKeyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ColorPickerHotkeyKey = value.ToUpperInvariant();
        Save();
        _refreshHotkeys();
    }

    /// <summary>変換ツールホットキー有効設定変更時に保存しホットキーを再登録する。</summary>
    partial void OnIsConverterHotkeyEnabledChanged(bool value)
    {
        if (_isLoading) return;
        _settingsService.Current.IsConverterHotkeyEnabled = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>変換ツールホットキー修飾キー変更時に保存しホットキーを再登録する。</summary>
    partial void OnConverterHotkeyModifiersChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ConverterHotkeyModifiers = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>変換ツールホットキーキー変更時に保存しホットキーを再登録する。</summary>
    partial void OnConverterHotkeyKeyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ConverterHotkeyKey = value.ToUpperInvariant();
        Save();
        _refreshHotkeys();
    }

    /// <summary>エラー一覧ホットキー有効設定変更時に保存しホットキーを再登録する。</summary>
    partial void OnIsErrorListHotkeyEnabledChanged(bool value)
    {
        if (_isLoading) return;
        _settingsService.Current.IsErrorListHotkeyEnabled = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>エラー一覧ホットキー修飾キー変更時に保存しホットキーを再登録する。</summary>
    partial void OnErrorListHotkeyModifiersChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ErrorListHotkeyModifiers = value;
        Save();
        _refreshHotkeys();
    }

    /// <summary>エラー一覧ホットキーキー変更時に保存しホットキーを再登録する。</summary>
    partial void OnErrorListHotkeyKeyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ErrorListHotkeyKey = value.ToUpperInvariant();
        Save();
        _refreshHotkeys();
    }

    /// <summary>クリップボード履歴フォント変更時に設定を保存し表示へ反映する。</summary>
    partial void OnHistoryFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.HistoryFontFamily = value;
        Save();
        _clipboardHistory.RefreshDisplaySettings();
    }

    /// <summary>クリップボード履歴フォントサイズ変更時に設定を保存し表示へ反映する。</summary>
    partial void OnHistoryFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.HistoryFontSize = value;
        Save();
        _clipboardHistory.RefreshDisplaySettings();
    }

    /// <summary>スクリーンショットメモフォント変更時に設定を保存し表示へ反映する。</summary>
    partial void OnScreenshotMemoFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotMemoFontFamily = value;
        Save();
        _screenshot.RefreshDisplaySettings();
    }

    /// <summary>スクリーンショットメモフォントサイズ変更時に設定を保存し表示へ反映する。</summary>
    partial void OnScreenshotMemoFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.ScreenshotMemoFontSize = value;
        Save();
        _screenshot.RefreshDisplaySettings();
    }

    /// <summary>メモテキストモードフォント変更時に設定を保存する。</summary>
    partial void OnMemoTextFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoTextFontFamily = value;
        Save();
    }

    /// <summary>メモテキストモードフォントサイズ変更時に設定を保存する。</summary>
    partial void OnMemoTextFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoTextFontSize = value;
        Save();
    }

    /// <summary>メモMarkdownエディタフォント変更時に設定を保存する。</summary>
    partial void OnMemoMarkdownEditFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoMarkdownEditFontFamily = value;
        Save();
    }

    /// <summary>メモMarkdownエディタフォントサイズ変更時に設定を保存する。</summary>
    partial void OnMemoMarkdownEditFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoMarkdownEditFontSize = value;
        Save();
    }

    /// <summary>メモMarkdownプレビューフォント変更時に設定を保存する。</summary>
    partial void OnMemoMarkdownViewFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoMarkdownViewFontFamily = value;
        Save();
    }

    /// <summary>メモMarkdownプレビューフォントサイズ変更時に設定を保存する。</summary>
    partial void OnMemoMarkdownViewFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoMarkdownViewFontSize = value;
        Save();
    }

    /// <summary>メモMarkdownコードブロックフォント変更時に設定を保存する。</summary>
    partial void OnMemoMarkdownCodeFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoMarkdownCodeFontFamily = value;
        Save();
    }

    /// <summary>メモMarkdownコードブロックフォントサイズ変更時に設定を保存する。</summary>
    partial void OnMemoMarkdownCodeFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.MemoMarkdownCodeFontSize = value;
        Save();
    }

    /// <summary>変換ツールUIフォント変更時に設定を保存する。</summary>
    partial void OnConverterUiFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ConverterUiFontFamily = value;
        Save();
    }

    /// <summary>変換ツールUIフォントサイズ変更時に設定を保存する。</summary>
    partial void OnConverterUiFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.ConverterUiFontSize = value;
        Save();
    }

    /// <summary>変換ツール入力欄フォント変更時に設定を保存する。</summary>
    partial void OnConverterEditFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ConverterEditFontFamily = value;
        Save();
    }

    /// <summary>変換ツール入力欄フォントサイズ変更時に設定を保存する。</summary>
    partial void OnConverterEditFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.ConverterEditFontSize = value;
        Save();
    }

    /// <summary>エラー一覧UIフォント変更時に設定を保存する。</summary>
    partial void OnErrorListUiFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ErrorListUiFontFamily = value;
        Save();
    }

    /// <summary>エラー一覧UIフォントサイズ変更時に設定を保存する。</summary>
    partial void OnErrorListUiFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.ErrorListUiFontSize = value;
        Save();
    }

    /// <summary>エラー一覧編集部フォント変更時に設定を保存する。</summary>
    partial void OnErrorListEditFontFamilyChanged(string value)
    {
        if (_isLoading) return;
        _settingsService.Current.ErrorListEditFontFamily = value;
        Save();
    }

    /// <summary>エラー一覧編集部フォントサイズ変更時に設定を保存する。</summary>
    partial void OnErrorListEditFontSizeChanged(double value)
    {
        if (_isLoading) return;
        _settingsService.Current.ErrorListEditFontSize = value;
        Save();
    }

    /// <summary>マスタータグのカラー変更を設定へ反映する。</summary>
    private void OnTagColorChanged(string name, string hex)
    {
        var tag = _settingsService.Current.MasterTags.FirstOrDefault(t => t.Name == name);
        if (tag is not null)
        {
            tag.ColorHex = hex;
            Save();
            RefreshAllDisplays();
        }
    }

    /// <summary>マスタータグを削除する。</summary>
    private void OnTagDeleted(string name)
    {
        _settingsService.Current.MasterTags.RemoveAll(t => t.Name == name);
        Save();

        _clipboardHistory.RemoveTag(name);
        _screenshot.RemoveTag(name);
        _memo.RemoveTag(name);

        RebuildMasterTags();
        RefreshAllDisplays();
    }

    /// <summary>マスタータグ名の変更を設定と各機能の既存レコードへ反映する。</summary>
    private void OnTagRenamed(string oldName, string newName)
    {
        bool duplicate = _settingsService.Current.MasterTags.Any(t => t.Name == newName);
        if (duplicate)
        {
            MessageBox.Show(
                LocalizationManager.Instance["Settings_TagRenameDuplicateMessage"],
                LocalizationManager.Instance["Settings_TagRenameDuplicateTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var tag = _settingsService.Current.MasterTags.FirstOrDefault(t => t.Name == oldName);
        if (tag is null)
        {
            return;
        }

        tag.Name = newName;
        Save();

        _clipboardHistory.RenameTag(oldName, newName);
        _screenshot.RenameTag(oldName, newName);
        _memo.RenameTag(oldName, newName);

        RebuildMasterTags();
        RefreshAllDisplays();
    }

    /// <summary>設定パネルの表示タブを切り替える。</summary>
    [RelayCommand]
    private void SelectSettingsTab(string indexText)
    {
        if (int.TryParse(indexText, out int index))
        {
            SettingsTabIndex = index;
        }
    }

    /// <summary>新規マスタータグを追加する。</summary>
    [RelayCommand]
    private void AddTag()
    {
        string name = NewTagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (_settingsService.Current.MasterTags.Any(t => t.Name == name))
        {
            NewTagName = string.Empty;
            return;
        }

        string color = MasterTagEditViewModel.Palette[_settingsService.Current.MasterTags.Count % MasterTagEditViewModel.Palette.Count];
        _settingsService.Current.MasterTags.Add(new MasterTag { Name = name, ColorHex = color });
        Save();
        NewTagName = string.Empty;
        RebuildMasterTags();
        RefreshAllDisplays();
    }

    /// <summary>クリップボード履歴・スクリーンショット・メモの全表示設定を再反映する。</summary>
    private void RefreshAllDisplays()
    {
        _clipboardHistory.RefreshDisplaySettings();
        _screenshot.RefreshDisplaySettings();
        _memo.RefreshDisplaySettings();
    }

    /// <summary>設定ファイル格納フォルダをエクスプローラーで開く。</summary>
    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = AppPaths.DataFolder, UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>設定とデータベース・画像をZIPへまとめてバックアップを作成する。</summary>
    [RelayCommand]
    private void CreateBackup()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"HasamiBoard_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            Filter = "Zip files (*.zip)|*.zip",
            DefaultExt = ".zip",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _backupService.CreateBackup(dialog.FileName);
            MessageBox.Show(
                LocalizationManager.Instance["Backup_CreateSuccessMessage"],
                LocalizationManager.Instance["Backup_CreateSuccessTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(LocalizationManager.Instance["Backup_CreateFailedMessage"], ex.Message),
                LocalizationManager.Instance["Backup_CreateFailedTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>選択したZIPファイルからバックアップを復元し、アプリを終了する。</summary>
    [RelayCommand]
    private void RestoreBackup()
    {
        var confirmResult = MessageBox.Show(
            LocalizationManager.Instance["Backup_RestoreConfirmMessage"],
            LocalizationManager.Instance["Backup_RestoreConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Zip files (*.zip)|*.zip",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _prepareForRestore();
            _backupService.RestoreBackup(dialog.FileName);

            MessageBox.Show(
                LocalizationManager.Instance["Backup_RestoreSuccessMessage"],
                LocalizationManager.Instance["Backup_RestoreSuccessTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _exitApplication();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(LocalizationManager.Instance["Backup_RestoreFailedMessage"], ex.Message),
                LocalizationManager.Instance["Backup_RestoreFailedTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>確認ダイアログの上、全設定項目を初期既定値へ復元する。</summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        var result = MessageBox.Show(
            LocalizationManager.Instance["Settings_ResetConfirmMessage"],
            LocalizationManager.Instance["Settings_ResetConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _settingsService.ResetToDefault();
        LoadFromSettings();
        _reloadMainViewModel();
        RefreshAllDisplays();
        _refreshHotkeys();
        LocalizationManager.Instance.ApplyLanguage(_settingsService.Current.Language);
    }
}
