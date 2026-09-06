namespace HasamiBoard.Models;

/// <summary>アプリ全体の設定値を保持するクラス (Settings.json にシリアライズされる)。</summary>
public class AppSettings
{
    // 言語 / テーマ
    public string Language { get; set; } = "auto"; // auto / ja / en
    public bool IsDarkMode { get; set; } = false;

    // マスタータグ
    public List<MasterTag> MasterTags { get; set; } = new()
    {
        new MasterTag { Name = "重要", ColorHex = "#E53935" },
        new MasterTag { Name = "作業中", ColorHex = "#FB8C00" },
        new MasterTag { Name = "完了", ColorHex = "#43A047" },
        new MasterTag { Name = "ColorPicker", ColorHex = "#3949AB" },
    };

    // 表示調整
    public double ItemMaxHeight { get; set; } = 50;
    public double ScreenshotThumbnailHeight { get; set; } = 120;

    // スクリーンショット保存設定
    public string ScreenshotImageFormat { get; set; } = "png"; // png / jpg / webp
    public int ScreenshotJpegQuality { get; set; } = 90;
    public int ScreenshotWebpQuality { get; set; } = 90;
    public bool ScreenshotWebpLossless { get; set; } = false;
    public bool IsDominantColorExtractionEnabled { get; set; } = true;

    // ホットキー
    public bool IsScreenshotHotkeyEnabled { get; set; } = true;
    public string ScreenshotHotkeyModifiers { get; set; } = "Ctrl+Shift";
    public string ScreenshotHotkeyKey { get; set; } = "S";

    public bool IsColorPickerHotkeyEnabled { get; set; } = true;
    public string ColorPickerHotkeyModifiers { get; set; } = "Ctrl+Shift";
    public string ColorPickerHotkeyKey { get; set; } = "C";

    public bool IsConverterHotkeyEnabled { get; set; } = true;
    public string ConverterHotkeyModifiers { get; set; } = "Ctrl+Shift";
    public string ConverterHotkeyKey { get; set; } = "T";

    public bool IsErrorListHotkeyEnabled { get; set; } = true;
    public string ErrorListHotkeyModifiers { get; set; } = "Ctrl+Shift";
    public string ErrorListHotkeyKey { get; set; } = "E";

    // ウィンドウ状態
    public bool IsTopmost { get; set; } = false;
    public bool IsConverterTopmost { get; set; } = false;
    public bool IsErrorListTopmost { get; set; } = false;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 420;
    public double WindowHeight { get; set; } = 640;

    // 変換ツール / エラーメッセージ一覧ウィンドウの位置・サイズ (未設定時は null にし、初回起動時のデフォルト配置に任せる)
    public double? ConverterWindowLeft { get; set; }
    public double? ConverterWindowTop { get; set; }
    public double ConverterWindowWidth { get; set; }
    public double ConverterWindowHeight { get; set; }

    public double? ErrorListWindowLeft { get; set; }
    public double? ErrorListWindowTop { get; set; }
    public double ErrorListWindowWidth { get; set; }
    public double ErrorListWindowHeight { get; set; }

    // 変換ツールの選択中モード / 各モードのオプション
    public string ConverterSelectedMode { get; set; } = "Hash";

    public string ConverterSqlKeywordCasing { get; set; } = "Uppercase";
    public string ConverterSqlCommaPlacement { get; set; } = "Trailing";
    public int ConverterSqlIndentationSize { get; set; } = 4;
    public bool ConverterSqlAlignClauseBodies { get; set; } = true;

    public string ConverterUuidVersion { get; set; } = "V4";
    public bool ConverterUuidNoHyphens { get; set; } = false;
    public bool ConverterUuidUppercase { get; set; } = false;

    public int ConverterPasswordLength { get; set; } = 12;
    public bool ConverterPasswordIncludeUppercase { get; set; } = true;
    public bool ConverterPasswordIncludeLowercase { get; set; } = true;
    public bool ConverterPasswordIncludeDigits { get; set; } = true;
    public bool ConverterPasswordIncludeSymbols { get; set; } = true;
    public bool ConverterPasswordExcludeAmbiguous { get; set; } = true;

    public bool ConverterRegexInteractive { get; set; } = true;
    public bool ConverterRegexShowMatchDetails { get; set; } = true;

    public bool ConverterNumberBaseUsePrefix { get; set; } = false;

    public string ConverterQrEccLevel { get; set; } = "M";
    public int ConverterQrPixelsPerModule { get; set; } = 8;

    public bool ConverterCronIncludeSeconds { get; set; } = false;
    public int ConverterCronOccurrenceCount { get; set; } = 5;

    public string ConverterHtpasswdAlgorithm { get; set; } = "Bcrypt";
    public int ConverterHtpasswdBcryptCost { get; set; } = 10;

    // エラーメッセージ一覧の選択中カテゴリ / 表示言語
    public string ErrorListSelectedMode { get; set; } = "Http";
    public string ErrorListSelectedLanguage { get; set; } = "En";

    // 保持数上限
    public int MaxHistoryCount { get; set; } = 100;
    public int MaxScreenshotHistoryCount { get; set; } = 100;

    // 自動起動 / UI表示
    public bool IsAutoStart { get; set; } = false;
    public bool IsQuickFilterVisible { get; set; } = true;

    // 機能有効無効
    public bool IsHistoryEnabled { get; set; } = true;
    public bool IsScreenshotEnabled { get; set; } = true;
    public bool IsMemoEnabled { get; set; } = true;
    public bool IsConverterEnabled { get; set; } = true;

    // 開発ツール内の各モードの有効無効 (無効化されたモード名の一覧、空の場合は全モード有効)
    public List<string> ConverterDisabledModes { get; set; } = new();

    // 開発ツール左メニューのサブ開発ツール表示順 (モード名の一覧、空の場合はデフォルト順)
    public List<string> ConverterModeOrder { get; set; } = new();

    // 開発ツール左メニューで折りたたまれているカテゴリ名の一覧 (空の場合は全カテゴリ展開)
    public List<string> ConverterCollapsedCategories { get; set; } = new();

    // タブ状態
    public int SelectedTabIndex { get; set; } = 0;

    // フォント設定
    public string HistoryFontFamily { get; set; } = "Consolas";
    public double HistoryFontSize { get; set; } = 13;

    public string ScreenshotMemoFontFamily { get; set; } = "Consolas";
    public double ScreenshotMemoFontSize { get; set; } = 13;

    public string MemoTextFontFamily { get; set; } = "Consolas";
    public double MemoTextFontSize { get; set; } = 13;

    public string MemoMarkdownEditFontFamily { get; set; } = "Consolas";
    public double MemoMarkdownEditFontSize { get; set; } = 13;

    public string MemoMarkdownViewFontFamily { get; set; } = "Segoe UI";
    public double MemoMarkdownViewFontSize { get; set; } = 14;

    public string MemoMarkdownCodeFontFamily { get; set; } = "Consolas";
    public double MemoMarkdownCodeFontSize { get; set; } = 13;

    // 変換ツール / エラーメッセージ一覧のフォント設定 (UI部 / 入出力・結果表示部)
    public string ConverterUiFontFamily { get; set; } = "Segoe UI";
    public double ConverterUiFontSize { get; set; } = 12;
    public string ConverterEditFontFamily { get; set; } = "Consolas";
    public double ConverterEditFontSize { get; set; } = 12;

    public string ErrorListUiFontFamily { get; set; } = "Segoe UI";
    public double ErrorListUiFontSize { get; set; } = 12;
    public string ErrorListEditFontFamily { get; set; } = "Consolas";
    public double ErrorListEditFontSize { get; set; } = 12;
}
