using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cronos;
using HasamiBoard.Services;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using QRCoder;

namespace HasamiBoard.ViewModels;

/// <summary>変換ツールウィンドウの各変換モードの状態と処理を管理するビューモデル。</summary>
public partial class ConverterViewModel : ObservableObject
{
    /// <summary>常に手前に表示するかどうか。</summary>
    [ObservableProperty]
    private bool _isTopmost;

    /// <summary>現在選択中の変換モード。</summary>
    [ObservableProperty]
    private ConverterMode _selectedMode = ConverterMode.Hash;

    // Base64 / URL / HTML / JSON で共有する入出力
    /// <summary>変換の入力テキスト。</summary>
    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>変換の出力テキスト。</summary>
    [ObservableProperty]
    private string _outputText = string.Empty;

    /// <summary>変換エラーメッセージ。</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // ハッシュ生成
    /// <summary>ハッシュ生成の入力テキスト。</summary>
    [ObservableProperty]
    private string _hashInputText = string.Empty;

    /// <summary>算出されたMD5ハッシュ値。</summary>
    [ObservableProperty]
    private string _md5Result = string.Empty;

    /// <summary>算出されたSHA1ハッシュ値。</summary>
    [ObservableProperty]
    private string _sha1Result = string.Empty;

    /// <summary>算出されたSHA256ハッシュ値。</summary>
    [ObservableProperty]
    private string _sha256Result = string.Empty;

    /// <summary>算出されたSHA512ハッシュ値。</summary>
    [ObservableProperty]
    private string _sha512Result = string.Empty;

    // Unixタイムスタンプ変換
    /// <summary>タイムスタンプ→日時変換の入力値。</summary>
    [ObservableProperty]
    private string _timestampInputText = string.Empty;

    /// <summary>日時→タイムスタンプ変換の出力値。</summary>
    [ObservableProperty]
    private string _timestampOutputText = string.Empty;

    /// <summary>日時→タイムスタンプ変換の入力値。</summary>
    [ObservableProperty]
    private string _dateInputText = string.Empty;

    /// <summary>タイムスタンプ→日時変換の出力値。</summary>
    [ObservableProperty]
    private string _dateOutputText = string.Empty;

    /// <summary>UTC表記のISO8601/RFC3339出力値。</summary>
    [ObservableProperty]
    private string _iso8601UtcOutputText = string.Empty;

    /// <summary>ローカル表記のISO8601/RFC3339出力値。</summary>
    [ObservableProperty]
    private string _iso8601LocalOutputText = string.Empty;

    /// <summary>タイムスタンプ変換のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _timestampErrorMessage = string.Empty;

    // UUID生成
    /// <summary>生成するUUIDのバージョン。</summary>
    [ObservableProperty]
    private UuidVersion _uuidVersion = UuidVersion.V4;

    /// <summary>UUID v5生成用の名前空間文字列。</summary>
    [ObservableProperty]
    private string _uuidNamespaceText = ConverterService.NamespaceDns.ToString();

    /// <summary>UUID v5生成用の名前文字列。</summary>
    [ObservableProperty]
    private string _uuidNameText = string.Empty;

    /// <summary>UUID表示時にハイフンを除去するかどうか。</summary>
    [ObservableProperty]
    private bool _uuidNoHyphens;

    /// <summary>UUID表示を大文字にするかどうか。</summary>
    [ObservableProperty]
    private bool _uuidUppercase;

    /// <summary>整形後のUUID表示文字列。</summary>
    [ObservableProperty]
    private string _uuidResult = string.Empty;

    /// <summary>UUID生成のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _uuidErrorMessage = string.Empty;

    private Guid _currentUuid = Guid.NewGuid();

    // パスワード生成
    /// <summary>生成するパスワードの文字数。</summary>
    [ObservableProperty]
    private int _passwordLength = 12;

    /// <summary>パスワード文字数を有効範囲へ丸める。</summary>
    partial void OnPasswordLengthChanged(int value)
    {
        int clamped = Math.Clamp(value, 4, 64);
        if (clamped != value)
        {
            PasswordLength = clamped;
        }
    }

    /// <summary>パスワードに大文字を含めるかどうか。</summary>
    [ObservableProperty]
    private bool _passwordIncludeUppercase = true;

    /// <summary>パスワードに小文字を含めるかどうか。</summary>
    [ObservableProperty]
    private bool _passwordIncludeLowercase = true;

    /// <summary>パスワードに数字を含めるかどうか。</summary>
    [ObservableProperty]
    private bool _passwordIncludeDigits = true;

    /// <summary>パスワードに記号を含めるかどうか。</summary>
    [ObservableProperty]
    private bool _passwordIncludeSymbols = true;

    /// <summary>紛らわしい文字を除外するかどうか。</summary>
    [ObservableProperty]
    private bool _passwordExcludeAmbiguous = true;

    /// <summary>生成されたパスワード文字列。</summary>
    [ObservableProperty]
    private string _passwordResult = string.Empty;

    /// <summary>パスワード生成のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _passwordErrorMessage = string.Empty;

    // 正規表現テスター
    /// <summary>正規表現パターン文字列。"/pattern/" 形式のスラッシュ囲みも可 (スラッシュは自動除去)。</summary>
    [ObservableProperty]
    private string _regexPatternText = string.Empty;

    /// <summary>大文字小文字を区別しないかどうか (i)。</summary>
    [ObservableProperty]
    private bool _regexIgnoreCase;

    /// <summary>グローバル検索するかどうか (g)。オフの場合は最初の1件のみマッチする。</summary>
    [ObservableProperty]
    private bool _regexGlobal = true;

    /// <summary>複数行モードかどうか (m)。^$を行単位でマッチさせる。</summary>
    [ObservableProperty]
    private bool _regexMultiline;

    /// <summary>マッチ対象のテスト文字列。</summary>
    [ObservableProperty]
    private string _regexTestInputText = string.Empty;

    /// <summary>入力の都度リアルタイムに再評価するかどうか。無効時は「実行」ボタン押下まで結果を更新しない。</summary>
    [ObservableProperty]
    private bool _regexInteractive = true;

    /// <summary>マッチ詳細一覧 (キャプチャグループ内訳) を表示するかどうか。</summary>
    [ObservableProperty]
    private bool _regexShowMatchDetails = true;

    /// <summary>マッチ件数の表示文字列。</summary>
    [ObservableProperty]
    private string _regexMatchCountText = string.Empty;

    /// <summary>正規表現のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _regexErrorMessage = string.Empty;

    /// <summary>マッチ結果一覧 (キャプチャグループ詳細を含む)。</summary>
    public ObservableCollection<RegexMatchItem> RegexMatches { get; } = new();

    /// <summary>マッチ結果の再計算が完了した際に発火する。ハイライト表示の再描画をビュー側へ通知する。</summary>
    public event EventHandler? RegexMatchesUpdated;

    // JWTデコーダー
    /// <summary>デコード対象のJWT文字列 ("header.payload.signature" 形式)。</summary>
    [ObservableProperty]
    private string _jwtTokenText = string.Empty;

    /// <summary>整形済みHeader JSON。</summary>
    [ObservableProperty]
    private string _jwtHeaderJson = string.Empty;

    /// <summary>整形済みPayload JSON。</summary>
    [ObservableProperty]
    private string _jwtPayloadJson = string.Empty;

    /// <summary>Signature部分 (Base64URL文字列、検証は行わない)。</summary>
    [ObservableProperty]
    private string _jwtSignatureText = string.Empty;

    /// <summary>JWTデコードのエラーメッセージ。</summary>
    [ObservableProperty]
    private string _jwtErrorMessage = string.Empty;

    /// <summary>Payloadから検出した exp/iat/nbf 等の日時系クレーム一覧。</summary>
    public ObservableCollection<JwtClaimDateItem> JwtClaimDates { get; } = new();

    // ケース変換
    /// <summary>ケース変換の入力文字列。</summary>
    [ObservableProperty]
    private string _caseInputText = string.Empty;

    /// <summary>camelCase変換結果。</summary>
    [ObservableProperty]
    private string _caseCamelResult = string.Empty;

    /// <summary>PascalCase変換結果。</summary>
    [ObservableProperty]
    private string _casePascalResult = string.Empty;

    /// <summary>snake_case変換結果。</summary>
    [ObservableProperty]
    private string _caseSnakeResult = string.Empty;

    /// <summary>kebab-case変換結果。</summary>
    [ObservableProperty]
    private string _caseKebabResult = string.Empty;

    /// <summary>CONSTANT_CASE変換結果。</summary>
    [ObservableProperty]
    private string _caseConstantResult = string.Empty;

    // 進数変換
    /// <summary>10進数の文字列。</summary>
    [ObservableProperty]
    private string _numberBaseDecimalText = string.Empty;

    /// <summary>16進数の文字列。</summary>
    [ObservableProperty]
    private string _numberBaseHexText = string.Empty;

    /// <summary>8進数の文字列。</summary>
    [ObservableProperty]
    private string _numberBaseOctalText = string.Empty;

    /// <summary>2進数の文字列。</summary>
    [ObservableProperty]
    private string _numberBaseBinaryText = string.Empty;

    /// <summary>進数変換のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _numberBaseErrorMessage = string.Empty;

    /// <summary>16進は"0x"、8進は"0o"、2進は"0B"の接頭辞を付けて表示するかどうか。</summary>
    [ObservableProperty]
    private bool _numberBaseUsePrefix;

    /// <summary>4つの進数欄を相互同期する際の再入防止フラグ。</summary>
    private bool _isUpdatingNumberBase;

    // QRコード生成
    /// <summary>QRコードとして生成するテキスト。</summary>
    [ObservableProperty]
    private string _qrInputText = string.Empty;

    /// <summary>QRコード生成時の誤り訂正レベル。</summary>
    [ObservableProperty]
    private QRCodeGenerator.ECCLevel _qrEccLevel = QRCodeGenerator.ECCLevel.M;

    /// <summary>QRコード生成時の1モジュールあたりのピクセル数。</summary>
    [ObservableProperty]
    private int _qrPixelsPerModule = 8;

    /// <summary>生成されたQRコード画像。</summary>
    [ObservableProperty]
    private BitmapSource? _qrResultImage;

    /// <summary>QRコード生成のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _qrGenerateErrorMessage = string.Empty;

    private byte[]? _qrResultPngBytes;

    // QRコードデコード
    /// <summary>デコード対象として読み込んだ画像。</summary>
    [ObservableProperty]
    private BitmapSource? _qrDecodeImage;

    /// <summary>QRコードから読み取られたテキスト。</summary>
    [ObservableProperty]
    private string _qrDecodeResultText = string.Empty;

    /// <summary>QRコードデコードのエラーメッセージ。</summary>
    [ObservableProperty]
    private string _qrDecodeErrorMessage = string.Empty;

    // Cron式ジェネレーター
    /// <summary>選択中の頻度テンプレート。Customの場合はCron式欄を直接編集する。</summary>
    [ObservableProperty]
    private CronFrequency _cronFrequency = CronFrequency.Daily;

    /// <summary>「毎秒」の間隔 (秒)。</summary>
    [ObservableProperty]
    private int _cronBuilderSecondInterval = 30;

    /// <summary>「毎分」の間隔 (分)。</summary>
    [ObservableProperty]
    private int _cronBuilderMinuteInterval = 5;

    /// <summary>毎時/毎日/毎週/毎月/毎年で使用する分 (0-59)。</summary>
    [ObservableProperty]
    private int _cronBuilderMinute;

    /// <summary>毎日/毎週/毎月/毎年で使用する時 (0-23)。</summary>
    [ObservableProperty]
    private int _cronBuilderHour = 9;

    /// <summary>毎月/毎年で使用する日 (1-31)。</summary>
    [ObservableProperty]
    private int _cronBuilderDayOfMonth = 1;

    /// <summary>毎年で使用する月 (1-12)。</summary>
    [ObservableProperty]
    private int _cronBuilderMonth = 1;

    /// <summary>毎週の対象曜日 (日曜)。</summary>
    [ObservableProperty]
    private bool _cronBuilderSunday;

    /// <summary>毎週の対象曜日 (月曜)。</summary>
    [ObservableProperty]
    private bool _cronBuilderMonday = true;

    /// <summary>毎週の対象曜日 (火曜)。</summary>
    [ObservableProperty]
    private bool _cronBuilderTuesday;

    /// <summary>毎週の対象曜日 (水曜)。</summary>
    [ObservableProperty]
    private bool _cronBuilderWednesday;

    /// <summary>毎週の対象曜日 (木曜)。</summary>
    [ObservableProperty]
    private bool _cronBuilderThursday;

    /// <summary>毎週の対象曜日 (金曜)。</summary>
    [ObservableProperty]
    private bool _cronBuilderFriday;

    /// <summary>毎週の対象曜日 (土曜)。</summary>
    [ObservableProperty]
    private bool _cronBuilderSaturday;

    /// <summary>頻度テンプレートがCustom (直接編集) かどうか。</summary>
    public bool IsCronCustomMode => CronFrequency == CronFrequency.Custom;

    /// <summary>Cron式欄を読み取り専用にするかどうか (Custom以外はジェネレーターが管理するため読み取り専用)。</summary>
    public bool IsCronExpressionReadOnly => !IsCronCustomMode;

    /// <summary>「秒フィールドを含む」チェックボックスを操作可能にするかどうか。
    /// 毎秒は構造上必ず秒フィールドが必要なため、その場合のみ固定 (操作不可) とする。</summary>
    public bool IsCronSecondsToggleEnabled => CronFrequency != CronFrequency.EverySecond;

    /// <summary>時選択コンボボックスの選択肢 (0-23)。</summary>
    public IReadOnlyList<int> CronHourOptions { get; } = Enumerable.Range(0, 24).ToList();

    /// <summary>分選択コンボボックスの選択肢 (0-59)。</summary>
    public IReadOnlyList<int> CronMinuteOptions { get; } = Enumerable.Range(0, 60).ToList();

    /// <summary>「毎秒」間隔選択コンボボックスの選択肢 (1-59)。</summary>
    public IReadOnlyList<int> CronSecondIntervalOptions { get; } = Enumerable.Range(1, 59).ToList();

    /// <summary>「毎分」間隔選択コンボボックスの選択肢 (1-59)。</summary>
    public IReadOnlyList<int> CronMinuteIntervalOptions { get; } = Enumerable.Range(1, 59).ToList();

    /// <summary>日選択コンボボックスの選択肢 (1-31)。</summary>
    public IReadOnlyList<int> CronDayOfMonthOptions { get; } = Enumerable.Range(1, 31).ToList();

    /// <summary>月選択コンボボックスの選択肢 (1-12)。</summary>
    public IReadOnlyList<int> CronMonthOptions { get; } = Enumerable.Range(1, 12).ToList();

    // Cron式パーサー
    /// <summary>解析対象のCron式。頻度テンプレート選択時はジェネレーターにより自動生成される。</summary>
    [ObservableProperty]
    private string _cronExpressionText = string.Empty;

    /// <summary>秒フィールドを含む6フィールド形式として解釈するかどうか。falseの場合は標準5フィールド。</summary>
    [ObservableProperty]
    private bool _cronIncludeSeconds;

    /// <summary>次回実行時刻の計算起点となる日時文字列 ("yyyy/MM/dd HH:mm:ss"形式。空の場合は現在時刻)。</summary>
    [ObservableProperty]
    private string _cronFromDateTimeText = string.Empty;

    /// <summary>計算する次回実行時刻の件数。</summary>
    [ObservableProperty]
    private int _cronOccurrenceCount = 5;

    /// <summary>Cron式の人間が読める説明文 (日本語)。</summary>
    [ObservableProperty]
    private string _cronDescriptionText = string.Empty;

    /// <summary>Cron式解析のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _cronErrorMessage = string.Empty;

    /// <summary>計算された次回実行時刻の一覧。</summary>
    public ObservableCollection<string> CronOccurrences { get; } = new();

    /// <summary>現在選択中のフィールド形式に応じたフィールド構成の説明文 (入力補助)。</summary>
    public string CronFieldsHintText => LocalizationManager.Instance[
        CronIncludeSeconds ? "Converter_Cron_FieldsHintWithSeconds" : "Converter_Cron_FieldsHintStandard"];

    // Basic認証作成
    /// <summary>Basic認証のユーザー名。</summary>
    [ObservableProperty]
    private string _basicAuthUsername = string.Empty;

    /// <summary>Basic認証のパスワード。</summary>
    [ObservableProperty]
    private string _basicAuthPassword = string.Empty;

    /// <summary>生成された "Authorization: Basic ..." ヘッダー文字列。</summary>
    [ObservableProperty]
    private string _basicAuthHeaderText = string.Empty;

    /// <summary>生成された userinfo形式のURL ("http://user:pass@example.com")。</summary>
    [ObservableProperty]
    private string _basicAuthUserInfoText = string.Empty;

    /// <summary>.htpasswd用パスワードハッシュ方式。</summary>
    [ObservableProperty]
    private HtpasswdAlgorithm _htpasswdAlgorithm = HtpasswdAlgorithm.Bcrypt;

    /// <summary>bcrypt選択時のコスト係数。</summary>
    [ObservableProperty]
    private int _htpasswdBcryptCost = 10;

    /// <summary>生成された .htpasswd 形式の1行 ("ユーザー名:ハッシュ")。</summary>
    [ObservableProperty]
    private string _htpasswdLineText = string.Empty;

    /// <summary>.htpasswd生成のエラーメッセージ。</summary>
    [ObservableProperty]
    private string _htpasswdErrorMessage = string.Empty;

    /// <summary>bcryptのコスト係数スライダーを表示するかどうか (bcrypt選択時のみ)。</summary>
    public bool IsHtpasswdBcryptCostVisible => HtpasswdAlgorithm == HtpasswdAlgorithm.Bcrypt;

    // jqテスト
    /// <summary>jqテストの入力JSON。</summary>
    [ObservableProperty]
    private string _jqInputText = string.Empty;

    /// <summary>jqテストのフィルタ式。</summary>
    [ObservableProperty]
    private string _jqFilterText = string.Empty;

    /// <summary>jqテストの評価結果テキスト。</summary>
    [ObservableProperty]
    private string _jqOutputText = string.Empty;

    /// <summary>jqテストのエラーメッセージ。</summary>
    [ObservableProperty]
    private string _jqErrorMessage = string.Empty;

    /// <summary>生文字列出力 (jqの -r 相当)。</summary>
    [ObservableProperty]
    private bool _jqRawOutput;

    /// <summary>コンパクト出力 (jqの -c 相当)。</summary>
    [ObservableProperty]
    private bool _jqCompactOutput;

    /// <summary>slurpモード (jqの -s 相当)。</summary>
    [ObservableProperty]
    private bool _jqSlurpInput;

    /// <summary>null入力モード (jqの -n 相当)。</summary>
    [ObservableProperty]
    private bool _jqNullInput;

    /// <summary>インタラクティブモード (ONの場合、入力JSON・フィルタ・オプションの変更のたびに自動で再評価する)。</summary>
    [ObservableProperty]
    private bool _jqInteractiveMode;

    /// <summary>入力JSONから構築した、キーパス・ナビゲーター表示用のツリー。</summary>
    [ObservableProperty]
    private IReadOnlyList<JqPathNode> _jqTreeRootNodes = Array.Empty<JqPathNode>();

    // SQLフォーマット オプション
    /// <summary>SQLフォーマットのキーワード大文字/小文字設定。</summary>
    [ObservableProperty]
    private KeywordCasing _sqlKeywordCasing = KeywordCasing.Uppercase;

    /// <summary>SQLフォーマットのカンマ位置設定。</summary>
    [ObservableProperty]
    private CommaPlacement _sqlCommaPlacement = CommaPlacement.Trailing;

    /// <summary>SQLフォーマットのインデント幅。</summary>
    [ObservableProperty]
    private int _sqlIndentationSize = 4;

    /// <summary>SQLフォーマットで句の本体を整列するかどうか。</summary>
    [ObservableProperty]
    private bool _sqlAlignClauseBodies = true;

    // フォント設定 (UI部 / 入出力欄部)。設定パネルからだけでなく、このウィンドウ自身の
    // 🔤 フォント設定ポップアップからも変更でき、変更は即座に反映・永続化される。
    /// <summary>利用可能なフォントファミリー一覧。</summary>
    /// <returns>システムから取得したフォント名の読み取り専用リスト。</returns>
    public IReadOnlyList<string> AvailableFontFamilies => FontCatalog.FontFamilies;

    /// <summary>変換ツールUI部のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string UiFontFamily
    {
        get => App.SettingsService.Current.ConverterUiFontFamily;
        set => SetFontSetting(value, App.SettingsService.Current.ConverterUiFontFamily, v => App.SettingsService.Current.ConverterUiFontFamily = v);
    }

    /// <summary>変換ツールUI部のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double UiFontSize
    {
        get => App.SettingsService.Current.ConverterUiFontSize;
        set => SetFontSetting(value, App.SettingsService.Current.ConverterUiFontSize, v => App.SettingsService.Current.ConverterUiFontSize = v);
    }

    /// <summary>変換ツール入出力欄のフォントファミリー。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public string EditFontFamily
    {
        get => App.SettingsService.Current.ConverterEditFontFamily;
        set => SetFontSetting(value, App.SettingsService.Current.ConverterEditFontFamily, v => App.SettingsService.Current.ConverterEditFontFamily = v);
    }

    /// <summary>変換ツール入出力欄のフォントサイズ。</summary>
    /// <value>設定から取得・設定し即座に永続化。</value>
    public double EditFontSize
    {
        get => App.SettingsService.Current.ConverterEditFontSize;
        set => SetFontSetting(value, App.SettingsService.Current.ConverterEditFontSize, v => App.SettingsService.Current.ConverterEditFontSize = v);
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

    /// <summary>保存済み設定から前回のモード・オプション・生成結果を復元する。</summary>
    public ConverterViewModel()
    {
        var settings = App.SettingsService.Current;
        IsTopmost = settings.IsConverterTopmost;

        if (Enum.TryParse<ConverterMode>(settings.ConverterSelectedMode, out var mode))
        {
            SelectedMode = mode;
        }

        if (Enum.TryParse<KeywordCasing>(settings.ConverterSqlKeywordCasing, out var casing))
        {
            SqlKeywordCasing = casing;
        }

        if (Enum.TryParse<CommaPlacement>(settings.ConverterSqlCommaPlacement, out var placement))
        {
            SqlCommaPlacement = placement;
        }

        SqlIndentationSize = settings.ConverterSqlIndentationSize;
        SqlAlignClauseBodies = settings.ConverterSqlAlignClauseBodies;

        if (Enum.TryParse<UuidVersion>(settings.ConverterUuidVersion, out var uuidVersion))
        {
            UuidVersion = uuidVersion;
        }

        UuidNoHyphens = settings.ConverterUuidNoHyphens;
        UuidUppercase = settings.ConverterUuidUppercase;

        PasswordLength = settings.ConverterPasswordLength;
        PasswordIncludeUppercase = settings.ConverterPasswordIncludeUppercase;
        PasswordIncludeLowercase = settings.ConverterPasswordIncludeLowercase;
        PasswordIncludeDigits = settings.ConverterPasswordIncludeDigits;
        PasswordIncludeSymbols = settings.ConverterPasswordIncludeSymbols;
        PasswordExcludeAmbiguous = settings.ConverterPasswordExcludeAmbiguous;

        RegexInteractive = settings.ConverterRegexInteractive;
        RegexShowMatchDetails = settings.ConverterRegexShowMatchDetails;

        NumberBaseUsePrefix = settings.ConverterNumberBaseUsePrefix;

        if (Enum.TryParse<QRCodeGenerator.ECCLevel>(settings.ConverterQrEccLevel, out var qrEccLevel))
        {
            QrEccLevel = qrEccLevel;
        }

        QrPixelsPerModule = settings.ConverterQrPixelsPerModule;

        CronIncludeSeconds = settings.ConverterCronIncludeSeconds;
        CronOccurrenceCount = settings.ConverterCronOccurrenceCount;
        ApplyCronBuilder();

        if (Enum.TryParse<HtpasswdAlgorithm>(settings.ConverterHtpasswdAlgorithm, out var htpasswdAlgorithm))
        {
            HtpasswdAlgorithm = htpasswdAlgorithm;
        }

        HtpasswdBcryptCost = settings.ConverterHtpasswdBcryptCost;
        RecomputeBasicAuthHeader();

        FormatUuid();
        GeneratePassword();
    }

    /// <summary>現在選択中のモードと各モードのオプションを設定へ保存する。ウィンドウを閉じる際に呼び出す。</summary>
    public void PersistState()
    {
        var settings = App.SettingsService.Current;
        settings.ConverterSelectedMode = SelectedMode.ToString();

        settings.ConverterSqlKeywordCasing = SqlKeywordCasing.ToString();
        settings.ConverterSqlCommaPlacement = SqlCommaPlacement.ToString();
        settings.ConverterSqlIndentationSize = SqlIndentationSize;
        settings.ConverterSqlAlignClauseBodies = SqlAlignClauseBodies;

        settings.ConverterUuidVersion = UuidVersion.ToString();
        settings.ConverterUuidNoHyphens = UuidNoHyphens;
        settings.ConverterUuidUppercase = UuidUppercase;

        settings.ConverterPasswordLength = PasswordLength;
        settings.ConverterPasswordIncludeUppercase = PasswordIncludeUppercase;
        settings.ConverterPasswordIncludeLowercase = PasswordIncludeLowercase;
        settings.ConverterPasswordIncludeDigits = PasswordIncludeDigits;
        settings.ConverterPasswordIncludeSymbols = PasswordIncludeSymbols;
        settings.ConverterPasswordExcludeAmbiguous = PasswordExcludeAmbiguous;

        settings.ConverterRegexInteractive = RegexInteractive;
        settings.ConverterRegexShowMatchDetails = RegexShowMatchDetails;

        settings.ConverterNumberBaseUsePrefix = NumberBaseUsePrefix;

        settings.ConverterQrEccLevel = QrEccLevel.ToString();
        settings.ConverterQrPixelsPerModule = QrPixelsPerModule;

        settings.ConverterCronIncludeSeconds = CronIncludeSeconds;
        settings.ConverterCronOccurrenceCount = CronOccurrenceCount;

        settings.ConverterHtpasswdAlgorithm = HtpasswdAlgorithm.ToString();
        settings.ConverterHtpasswdBcryptCost = HtpasswdBcryptCost;
    }

    /// <summary>常に手前に表示する設定を切り替える。</summary>
    [RelayCommand]
    private void ToggleTopmost()
    {
        IsTopmost = !IsTopmost;
        App.SettingsService.Current.IsConverterTopmost = IsTopmost;
        App.SettingsService.Save();
    }

    /// <summary>文字列で指定された変換モードへ切り替える。</summary>
    [RelayCommand]
    private void SelectMode(string modeName)
    {
        if (Enum.TryParse<ConverterMode>(modeName, out var mode))
        {
            SelectedMode = mode;
        }
    }

    /// <summary>直前に「⇄」で出力を入力へ戻した直後かどうか。次のモード切替でのみ入力を保持する。</summary>
    private bool _preserveInputAfterSwap;

    /// <summary>モード切替時に入力・出力・エラー表示をクリアする。ただし「⇄」による連結変換の直後は入力を保持する。</summary>
    partial void OnSelectedModeChanged(ConverterMode value)
    {
        if (_preserveInputAfterSwap)
        {
            _preserveInputAfterSwap = false;
            OutputText = string.Empty;
            ErrorMessage = string.Empty;
            return;
        }

        InputText = string.Empty;
        OutputText = string.Empty;
        ErrorMessage = string.Empty;
    }

    /// <summary>ハッシュ入力変更時に各種ハッシュ値を再計算する。</summary>
    partial void OnHashInputTextChanged(string value)
    {
        Md5Result = ConverterService.ComputeMd5(value);
        Sha1Result = ConverterService.ComputeSha1(value);
        Sha256Result = ConverterService.ComputeSha256(value);
        Sha512Result = ConverterService.ComputeSha512(value);
    }

    /// <summary>ケース変換入力変更時に各記法への変換結果を再計算する。</summary>
    partial void OnCaseInputTextChanged(string value)
    {
        CaseCamelResult = ConverterService.ToCamelCase(value);
        CasePascalResult = ConverterService.ToPascalCase(value);
        CaseSnakeResult = ConverterService.ToSnakeCase(value);
        CaseKebabResult = ConverterService.ToKebabCase(value);
        CaseConstantResult = ConverterService.ToConstantCase(value);
    }

    /// <summary>ケース変換の入力をクリアする。</summary>
    [RelayCommand]
    private void ClearCase() => CaseInputText = string.Empty;

    /// <summary>10進数欄変更時に他の進数欄へ同期する。</summary>
    partial void OnNumberBaseDecimalTextChanged(string value) => SyncNumberBase(value, 10);

    /// <summary>16進数欄変更時に他の進数欄へ同期する。</summary>
    partial void OnNumberBaseHexTextChanged(string value) => SyncNumberBase(value, 16);

    /// <summary>8進数欄変更時に他の進数欄へ同期する。</summary>
    partial void OnNumberBaseOctalTextChanged(string value) => SyncNumberBase(value, 8);

    /// <summary>2進数欄変更時に他の進数欄へ同期する。</summary>
    partial void OnNumberBaseBinaryTextChanged(string value) => SyncNumberBase(value, 2);

    /// <summary>指定した基数の欄が変更された際に、値を解析して他の3つの基数欄へ反映する。</summary>
    private void SyncNumberBase(string text, int fromBase)
    {
        if (_isUpdatingNumberBase)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            NumberBaseErrorMessage = string.Empty;
            return;
        }

        if (!ConverterService.TryParseNumberBase(text, fromBase, out ulong value))
        {
            NumberBaseErrorMessage = LocalizationManager.Instance["Converter_NumberBase_InvalidFormat"];
            return;
        }

        NumberBaseErrorMessage = string.Empty;
        _isUpdatingNumberBase = true;
        try
        {
            if (fromBase != 10)
            {
                NumberBaseDecimalText = ConverterService.FormatNumberBase(value, 10);
            }

            if (fromBase != 16)
            {
                NumberBaseHexText = ConverterService.FormatNumberBase(value, 16, NumberBaseUsePrefix);
            }

            if (fromBase != 8)
            {
                NumberBaseOctalText = ConverterService.FormatNumberBase(value, 8, NumberBaseUsePrefix);
            }

            if (fromBase != 2)
            {
                NumberBaseBinaryText = ConverterService.FormatNumberBase(value, 2, NumberBaseUsePrefix);
            }
        }
        finally
        {
            _isUpdatingNumberBase = false;
        }
    }

    /// <summary>接頭辞表示設定の切替時、現在の値を保ったまま16/8/2進の各欄を再フォーマットする。</summary>
    partial void OnNumberBaseUsePrefixChanged(bool value)
    {
        if (!ConverterService.TryParseNumberBase(NumberBaseDecimalText, 10, out ulong current))
        {
            return;
        }

        _isUpdatingNumberBase = true;
        try
        {
            NumberBaseHexText = ConverterService.FormatNumberBase(current, 16, value);
            NumberBaseOctalText = ConverterService.FormatNumberBase(current, 8, value);
            NumberBaseBinaryText = ConverterService.FormatNumberBase(current, 2, value);
        }
        finally
        {
            _isUpdatingNumberBase = false;
        }
    }

    /// <summary>進数変換の4つの欄をすべてクリアする。</summary>
    [RelayCommand]
    private void ClearNumberBase()
    {
        NumberBaseDecimalText = string.Empty;
        NumberBaseHexText = string.Empty;
        NumberBaseOctalText = string.Empty;
        NumberBaseBinaryText = string.Empty;
        NumberBaseErrorMessage = string.Empty;
    }

    /// <summary>文字列で指定された誤り訂正レベルへ切り替える。</summary>
    [RelayCommand]
    private void SelectQrEccLevel(string levelName)
    {
        if (Enum.TryParse<QRCodeGenerator.ECCLevel>(levelName, out var level))
        {
            QrEccLevel = level;
        }
    }

    /// <summary>入力テキストからQRコード画像を生成する。</summary>
    [RelayCommand]
    private void GenerateQrCode()
    {
        QrGenerateErrorMessage = string.Empty;
        if (string.IsNullOrEmpty(QrInputText))
        {
            QrResultImage = null;
            _qrResultPngBytes = null;
            return;
        }

        try
        {
            _qrResultPngBytes = ConverterService.GenerateQrCodePng(QrInputText, QrEccLevel, QrPixelsPerModule);
            QrResultImage = ConverterService.PngBytesToBitmapImage(_qrResultPngBytes);
        }
        catch (Exception ex)
        {
            QrResultImage = null;
            _qrResultPngBytes = null;
            QrGenerateErrorMessage = ex.Message;
        }
    }

    /// <summary>生成されたQRコード画像をOSクリップボードへ画像としてコピーする。</summary>
    [RelayCommand]
    private void CopyQrImageToClipboard()
    {
        if (QrResultImage is not null)
        {
            Clipboard.SetImage(QrResultImage);
        }
    }

    /// <summary>生成されたQRコード画像をPNGファイルとして保存する。</summary>
    [RelayCommand]
    private void SaveQrImage()
    {
        if (_qrResultPngBytes is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"QRCode_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            Filter = "PNG files (*.png)|*.png",
            DefaultExt = ".png",
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllBytes(dialog.FileName, _qrResultPngBytes);
        }
    }

    /// <summary>QRコード生成の入力・結果をすべてクリアする。</summary>
    [RelayCommand]
    private void ClearQrGenerate()
    {
        QrInputText = string.Empty;
        QrResultImage = null;
        _qrResultPngBytes = null;
        QrGenerateErrorMessage = string.Empty;
    }

    /// <summary>ファイル選択ダイアログから画像を読み込みQRコードをデコードする。</summary>
    [RelayCommand]
    private void BrowseQrDecodeImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(dialog.FileName, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            SetQrDecodeImage(bitmap);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            QrDecodeErrorMessage = ex.Message;
        }
    }

    /// <summary>クリップボードの画像をデコード対象として読み込む。</summary>
    [RelayCommand]
    private void PasteQrDecodeImage()
    {
        BitmapSource? image = ClipboardImageHelper.GetImage();
        if (image is not null)
        {
            SetQrDecodeImage(image);
        }
    }

    /// <summary>デコード対象画像を設定し、即座にQRコードのデコードを試みる。</summary>
    private void SetQrDecodeImage(BitmapSource image)
    {
        QrDecodeImage = image;
        QrDecodeErrorMessage = string.Empty;
        try
        {
            string? decoded = ConverterService.DecodeQrCode(image);
            if (decoded is null)
            {
                QrDecodeResultText = string.Empty;
                QrDecodeErrorMessage = LocalizationManager.Instance["Converter_QrCode_NotDetected"];
            }
            else
            {
                QrDecodeResultText = decoded;
            }
        }
        catch (Exception ex)
        {
            QrDecodeResultText = string.Empty;
            QrDecodeErrorMessage = ex.Message;
        }
    }

    /// <summary>外部 (スクリーンショット機能等) から渡された画像をQRコードモードのデコード対象として読み込む。
    /// QRコードモードへの切り替えも合わせて行う。</summary>
    /// <param name="image">解析対象の画像</param>
    public void LoadQrDecodeImageFromExternalSource(BitmapSource image)
    {
        SelectedMode = ConverterMode.QrCode;
        SetQrDecodeImage(image);
    }

    /// <summary>QRコードデコードの入力・結果をすべてクリアする。</summary>
    [RelayCommand]
    private void ClearQrDecode()
    {
        QrDecodeImage = null;
        QrDecodeResultText = string.Empty;
        QrDecodeErrorMessage = string.Empty;
    }

    /// <summary>文字列で指定された頻度テンプレートへ切り替える。</summary>
    [RelayCommand]
    private void SelectCronFrequency(string frequencyName)
    {
        if (Enum.TryParse<CronFrequency>(frequencyName, out var frequency))
        {
            CronFrequency = frequency;
        }
    }

    /// <summary>頻度テンプレート変更時、Custom以外ならCron式を再生成する。
    /// 毎秒は構造上必ず秒フィールドが必要なため、選択時は自動的にONへ切り替える。</summary>
    partial void OnCronFrequencyChanged(CronFrequency value)
    {
        OnPropertyChanged(nameof(IsCronCustomMode));
        OnPropertyChanged(nameof(IsCronExpressionReadOnly));
        OnPropertyChanged(nameof(IsCronSecondsToggleEnabled));

        if (value == CronFrequency.EverySecond)
        {
            CronIncludeSeconds = true;
        }

        ApplyCronBuilder();
    }

    partial void OnCronBuilderSecondIntervalChanged(int value) => ApplyCronBuilder();
    partial void OnCronBuilderMinuteIntervalChanged(int value) => ApplyCronBuilder();
    partial void OnCronBuilderMinuteChanged(int value) => ApplyCronBuilder();
    partial void OnCronBuilderHourChanged(int value) => ApplyCronBuilder();
    partial void OnCronBuilderDayOfMonthChanged(int value) => ApplyCronBuilder();
    partial void OnCronBuilderMonthChanged(int value) => ApplyCronBuilder();
    partial void OnCronBuilderSundayChanged(bool value) => ApplyCronBuilder();
    partial void OnCronBuilderMondayChanged(bool value) => ApplyCronBuilder();
    partial void OnCronBuilderTuesdayChanged(bool value) => ApplyCronBuilder();
    partial void OnCronBuilderWednesdayChanged(bool value) => ApplyCronBuilder();
    partial void OnCronBuilderThursdayChanged(bool value) => ApplyCronBuilder();
    partial void OnCronBuilderFridayChanged(bool value) => ApplyCronBuilder();
    partial void OnCronBuilderSaturdayChanged(bool value) => ApplyCronBuilder();

    /// <summary>選択中の頻度テンプレートと各構成要素から、Cron式を組み立てて<see cref="CronExpressionText"/>へ反映する。
    /// Customの場合は何もしない (Cron式欄への直接入力に委ねる)。毎秒以外は<see cref="CronIncludeSeconds"/>がONの場合、
    /// 秒=0のフィールドを先頭に付与した6フィールド形式で生成する。</summary>
    private void ApplyCronBuilder()
    {
        if (CronFrequency == CronFrequency.Custom)
        {
            return;
        }

        // 毎秒は構造上必ず6フィールドだが、それ以外のテンプレートは秒フィールドを含む設定がONの場合のみ
        // 秒=0を先頭に付与する (Cronosの CronFormat.IncludeSeconds は常に6フィールドを要求するため)。
        string secondsPrefix = CronFrequency != CronFrequency.EverySecond && CronIncludeSeconds ? "0 " : string.Empty;

        switch (CronFrequency)
        {
            case CronFrequency.EverySecond:
                CronExpressionText = CronBuilderSecondInterval <= 1 ? "* * * * * *" : $"*/{CronBuilderSecondInterval} * * * * *";
                break;

            case CronFrequency.EveryMinute:
                CronExpressionText = secondsPrefix + (CronBuilderMinuteInterval <= 1 ? "* * * * *" : $"*/{CronBuilderMinuteInterval} * * * *");
                break;

            case CronFrequency.Hourly:
                CronExpressionText = secondsPrefix + $"{CronBuilderMinute} * * * *";
                break;

            case CronFrequency.Daily:
                CronExpressionText = secondsPrefix + $"{CronBuilderMinute} {CronBuilderHour} * * *";
                break;

            case CronFrequency.Weekly:
                if (ConverterService.TryBuildCronDayOfWeekField(GetSelectedCronDays(), out string dayField))
                {
                    CronExpressionText = secondsPrefix + $"{CronBuilderMinute} {CronBuilderHour} * * {dayField}";
                }
                else
                {
                    CronErrorMessage = LocalizationManager.Instance["Converter_Cron_Builder_SelectAtLeastOneDay"];
                    CronDescriptionText = string.Empty;
                    CronOccurrences.Clear();
                }

                break;

            case CronFrequency.Monthly:
                CronExpressionText = secondsPrefix + $"{CronBuilderMinute} {CronBuilderHour} {CronBuilderDayOfMonth} * *";
                break;

            case CronFrequency.Yearly:
                CronExpressionText = secondsPrefix + $"{CronBuilderMinute} {CronBuilderHour} {CronBuilderDayOfMonth} {CronBuilderMonth} *";
                break;
        }
    }

    /// <summary>毎週の対象曜日チェックボックスから、選択された曜日番号 (0=日曜〜6=土曜) の一覧を取得する。</summary>
    private List<int> GetSelectedCronDays()
    {
        var days = new List<int>();
        if (CronBuilderSunday) days.Add(0);
        if (CronBuilderMonday) days.Add(1);
        if (CronBuilderTuesday) days.Add(2);
        if (CronBuilderWednesday) days.Add(3);
        if (CronBuilderThursday) days.Add(4);
        if (CronBuilderFriday) days.Add(5);
        if (CronBuilderSaturday) days.Add(6);
        return days;
    }

    /// <summary>Cron式の変更時に再解析する。</summary>
    partial void OnCronExpressionTextChanged(string value) => RecomputeCron();

    /// <summary>フィールド形式変更時、ヒント表示を更新する。Customの場合は現在のCron式をそのまま新形式で再解析し、
    /// テンプレート使用時はCron式自体を (秒フィールドの有無を反映して) 再生成する。</summary>
    partial void OnCronIncludeSecondsChanged(bool value)
    {
        OnPropertyChanged(nameof(CronFieldsHintText));

        if (CronFrequency == CronFrequency.Custom)
        {
            RecomputeCron();
        }
        else
        {
            ApplyCronBuilder();
        }
    }

    /// <summary>計算起点日時の変更時に再解析する。</summary>
    partial void OnCronFromDateTimeTextChanged(string value) => RecomputeCron();

    /// <summary>計算件数の変更時に再解析する。</summary>
    partial void OnCronOccurrenceCountChanged(int value) => RecomputeCron();

    /// <summary>現在のCron式・オプションから、説明文と次回実行時刻一覧を再計算する。</summary>
    private void RecomputeCron()
    {
        CronErrorMessage = string.Empty;
        CronDescriptionText = string.Empty;
        CronOccurrences.Clear();

        if (string.IsNullOrWhiteSpace(CronExpressionText))
        {
            return;
        }

        try
        {
            var format = CronIncludeSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard;
            var occurrences = ConverterService.GetNextCronOccurrences(
                CronExpressionText, format, CronFromDateTimeText, Math.Max(1, CronOccurrenceCount));

            foreach (var occurrence in occurrences)
            {
                CronOccurrences.Add(occurrence);
            }

            CronDescriptionText = ConverterService.DescribeCron(CronExpressionText);
        }
        catch (Exception ex)
        {
            CronErrorMessage = ex.Message;
        }
    }

    /// <summary>Cron式の計算起点日時に現在時刻を設定する。</summary>
    [RelayCommand]
    private void UseCurrentDateTimeForCron() => CronFromDateTimeText = DateTime.Now.ToString(ConverterService.TimestampDateFormat);

    /// <summary>Cron式パーサーの入力・結果をすべてクリアする。</summary>
    [RelayCommand]
    private void ClearCron()
    {
        CronExpressionText = string.Empty;
        CronFromDateTimeText = string.Empty;
    }

    /// <summary>ユーザー名またはパスワードの変更時に、Authorizationヘッダーを再計算する。</summary>
    partial void OnBasicAuthUsernameChanged(string value) => RecomputeBasicAuthHeader();

    /// <summary>ユーザー名またはパスワードの変更時に、Authorizationヘッダーを再計算する。</summary>
    partial void OnBasicAuthPasswordChanged(string value) => RecomputeBasicAuthHeader();

    /// <summary>ユーザー名とパスワードから、Authorizationヘッダーとuserinfo形式のURLを再計算する。</summary>
    private void RecomputeBasicAuthHeader()
    {
        BasicAuthHeaderText = ConverterService.BuildBasicAuthHeader(BasicAuthUsername, BasicAuthPassword);
        BasicAuthUserInfoText = ConverterService.BuildBasicAuthUserInfoUrl(BasicAuthUsername, BasicAuthPassword);
    }

    /// <summary>ハッシュ方式変更時、コスト係数スライダーの表示状態を更新する。</summary>
    partial void OnHtpasswdAlgorithmChanged(HtpasswdAlgorithm value) => OnPropertyChanged(nameof(IsHtpasswdBcryptCostVisible));

    /// <summary>文字列で指定されたハッシュ方式へ切り替える。</summary>
    [RelayCommand]
    private void SelectHtpasswdAlgorithm(string algorithmName)
    {
        if (Enum.TryParse<HtpasswdAlgorithm>(algorithmName, out var algorithm))
        {
            HtpasswdAlgorithm = algorithm;
        }
    }

    /// <summary>現在の入力・設定から .htpasswd 形式の1行を生成する。</summary>
    [RelayCommand]
    private void GenerateHtpasswdLine()
    {
        HtpasswdErrorMessage = string.Empty;
        HtpasswdLineText = string.Empty;

        if (string.IsNullOrEmpty(BasicAuthUsername))
        {
            HtpasswdErrorMessage = LocalizationManager.Instance["Converter_BasicAuth_UsernameRequired"];
            return;
        }

        if (BasicAuthUsername.Contains(':'))
        {
            HtpasswdErrorMessage = LocalizationManager.Instance["Converter_BasicAuth_UsernameNoColon"];
            return;
        }

        try
        {
            HtpasswdLineText = HtpasswdAlgorithm switch
            {
                HtpasswdAlgorithm.Bcrypt => ConverterService.BuildHtpasswdLineBcrypt(BasicAuthUsername, BasicAuthPassword, HtpasswdBcryptCost),
                HtpasswdAlgorithm.Apr1Md5 => ConverterService.BuildHtpasswdLineApr1(BasicAuthUsername, BasicAuthPassword),
                HtpasswdAlgorithm.Sha1 => ConverterService.BuildHtpasswdLineSha1(BasicAuthUsername, BasicAuthPassword),
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            HtpasswdErrorMessage = ex.Message;
        }
    }

    /// <summary>Basic認証作成ツールの入力・結果をすべてクリアする。</summary>
    [RelayCommand]
    private void ClearBasicAuth()
    {
        BasicAuthUsername = string.Empty;
        BasicAuthPassword = string.Empty;
        HtpasswdLineText = string.Empty;
        HtpasswdErrorMessage = string.Empty;
    }

    /// <summary>入力JSONの変更時に、キーパス・ナビゲーターのツリーを再構築し、インタラクティブモードなら再評価する。</summary>
    partial void OnJqInputTextChanged(string value)
    {
        try
        {
            JqTreeRootNodes = ConverterService.BuildJqPathTree(value);
        }
        catch (Exception)
        {
            JqTreeRootNodes = Array.Empty<JqPathNode>();
        }

        ReevaluateJqIfInteractive();
    }

    /// <summary>フィルタ式の変更時、インタラクティブモードなら再評価する。</summary>
    partial void OnJqFilterTextChanged(string value) => ReevaluateJqIfInteractive();

    /// <summary>各種オプション変更時、インタラクティブモードなら再評価する。</summary>
    partial void OnJqRawOutputChanged(bool value) => ReevaluateJqIfInteractive();

    /// <summary>各種オプション変更時、インタラクティブモードなら再評価する。</summary>
    partial void OnJqCompactOutputChanged(bool value) => ReevaluateJqIfInteractive();

    /// <summary>各種オプション変更時、インタラクティブモードなら再評価する。</summary>
    partial void OnJqSlurpInputChanged(bool value) => ReevaluateJqIfInteractive();

    /// <summary>各種オプション変更時、インタラクティブモードなら再評価する。</summary>
    partial void OnJqNullInputChanged(bool value) => ReevaluateJqIfInteractive();

    /// <summary>インタラクティブモードをONにした直後にも、現在の内容で即座に再評価する。</summary>
    partial void OnJqInteractiveModeChanged(bool value) => ReevaluateJqIfInteractive();

    /// <summary>インタラクティブモードがONの場合のみ、jqの評価を再実行する。</summary>
    private void ReevaluateJqIfInteractive()
    {
        if (JqInteractiveMode)
        {
            EvaluateJq();
        }
    }

    /// <summary>現在の入力JSON・フィルタ・オプションからjqの評価を実行する。</summary>
    [RelayCommand]
    private void EvaluateJq()
    {
        JqErrorMessage = string.Empty;

        try
        {
            var results = ConverterService.EvaluateJq(JqInputText, JqFilterText, JqRawOutput, JqCompactOutput, JqSlurpInput, JqNullInput);
            JqOutputText = string.Join(Environment.NewLine, results);
        }
        catch (Exception ex)
        {
            JqOutputText = string.Empty;
            JqErrorMessage = ex.Message;
        }
    }

    /// <summary>jqテストの入力・フィルタ・出力・エラーをすべてクリアする (ツリーは保持)。</summary>
    [RelayCommand]
    private void ClearJq()
    {
        JqInputText = string.Empty;
        JqFilterText = string.Empty;
        JqOutputText = string.Empty;
        JqErrorMessage = string.Empty;
    }

    /// <summary>jqの出力を入力JSONへ戻し、フィルタを重ねてテストできるようにする。</summary>
    [RelayCommand]
    private void SwapJqInputOutput() => JqInputText = JqOutputText;

    /// <summary>入力JSON欄に、指定パターンの動作確認用サンプルJSONを挿入する。</summary>
    /// <param name="sampleKey">"Simple" (フラットなオブジェクト) / "Nested" (ネストしたオブジェクト+配列) / "Array" (ルートが配列)</param>
    [RelayCommand]
    private void InsertJqSampleJson(string sampleKey)
    {
        JqInputText = sampleKey switch
        {
            "Simple" => ConverterService.JqSampleJsonSimple,
            "Array" => ConverterService.JqSampleJsonArray,
            _ => ConverterService.JqSampleJsonNested,
        };
    }

    /// <summary>現在のモードに応じて入力テキストをエンコードする。</summary>
    [RelayCommand]
    private void Encode()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = SelectedMode switch
            {
                ConverterMode.Base64 => ConverterService.Base64Encode(InputText),
                ConverterMode.Url => ConverterService.UrlEncode(InputText),
                ConverterMode.Html => ConverterService.HtmlEncode(InputText),
                _ => OutputText,
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>現在のモードに応じて入力テキストをデコードする。</summary>
    [RelayCommand]
    private void Decode()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = SelectedMode switch
            {
                ConverterMode.Base64 => ConverterService.Base64Decode(InputText),
                ConverterMode.Url => ConverterService.UrlDecode(InputText),
                ConverterMode.Html => ConverterService.HtmlDecode(InputText),
                _ => OutputText,
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>入力テキストをJSONとして整形する。</summary>
    [RelayCommand]
    private void FormatJson()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = ConverterService.FormatJson(InputText);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>入力テキストのJSONを圧縮する。</summary>
    [RelayCommand]
    private void MinifyJson()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = ConverterService.MinifyJson(InputText);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>入力テキストをXMLとして整形する。</summary>
    [RelayCommand]
    private void FormatXml()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = ConverterService.FormatXml(InputText);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>入力テキストをHTMLとして整形する。</summary>
    [RelayCommand]
    private void FormatHtml()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = ConverterService.FormatHtml(InputText);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>入力テキストをCSSとして整形する。</summary>
    [RelayCommand]
    private void FormatCss()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = ConverterService.FormatCss(InputText);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>入力テキストをJavaScriptとして整形する。</summary>
    [RelayCommand]
    private void FormatJavaScript()
    {
        ErrorMessage = string.Empty;
        try
        {
            OutputText = ConverterService.FormatJavaScript(InputText);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>入力テキストを現在のオプションでSQLとして整形する。</summary>
    [RelayCommand]
    private void FormatSql()
    {
        ErrorMessage = string.Empty;
        try
        {
            var options = new SqlScriptGeneratorOptions
            {
                KeywordCasing = SqlKeywordCasing,
                CommaPlacement = SqlCommaPlacement,
                IndentationSize = SqlIndentationSize,
                AlignClauseBodies = SqlAlignClauseBodies,
            };
            OutputText = ConverterService.FormatSql(InputText, options);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>文字列で指定されたキーワード大文字/小文字設定へ切り替える。</summary>
    [RelayCommand]
    private void SelectSqlKeywordCasing(string casingName)
    {
        if (Enum.TryParse<KeywordCasing>(casingName, out var casing))
        {
            SqlKeywordCasing = casing;
        }
    }

    /// <summary>文字列で指定されたカンマ位置設定へ切り替える。</summary>
    [RelayCommand]
    private void SelectSqlCommaPlacement(string placementName)
    {
        if (Enum.TryParse<CommaPlacement>(placementName, out var placement))
        {
            SqlCommaPlacement = placement;
        }
    }

    /// <summary>出力テキストを入力へ戻し、連結変換を可能にする。</summary>
    [RelayCommand]
    private void Swap()
    {
        InputText = OutputText;
        OutputText = string.Empty;
        ErrorMessage = string.Empty;
        _preserveInputAfterSwap = true;
    }

    /// <summary>入力・出力・エラー表示をクリアする。</summary>
    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        ErrorMessage = string.Empty;
    }

    /// <summary>パターンまたはテスト文字列の変更時にマッチ結果を再計算する。
    /// マッチ詳細 (キャプチャグループ内訳) は表示が有効な場合のみ構築し、非表示時の再計算コストを抑える。
    /// 完了後は必ず <see cref="RegexMatchesUpdated"/> を1回だけ発火し、ハイライト表示の再描画をビュー側に一括で通知する。</summary>
    private void RecomputeRegexMatches()
    {
        RegexErrorMessage = string.Empty;
        RegexMatches.Clear();
        RegexMatchCountText = string.Empty;

        if (!string.IsNullOrEmpty(RegexPatternText))
        {
            try
            {
                string pattern = ConverterService.ExtractRegexPattern(RegexPatternText);
                var options = RegexOptions.None;
                if (RegexIgnoreCase)
                {
                    options |= RegexOptions.IgnoreCase;
                }

                if (RegexMultiline)
                {
                    options |= RegexOptions.Multiline;
                }

                var allMatches = ConverterService.FindRegexMatches(pattern, RegexTestInputText, options);
                var matches = RegexGlobal ? allMatches : allMatches.Take(1).ToList();

                int number = 0;
                foreach (var match in matches)
                {
                    number++;
                    string header = string.Empty;
                    IReadOnlyList<RegexGroupItem> groups = Array.Empty<RegexGroupItem>();
                    if (RegexShowMatchDetails)
                    {
                        header = string.Format(LocalizationManager.Instance["Converter_Regex_MatchHeaderFormat"], number, match.Index);
                        groups = match.Groups.Cast<Group>()
                            .Skip(1)
                            .Where(g => g.Success)
                            .Select(g => new RegexGroupItem(g.Name, g.Value))
                            .ToList();
                    }

                    RegexMatches.Add(new RegexMatchItem(header, match.Value, match.Index, groups));
                }

                RegexMatchCountText = matches.Count == ConverterService.MaxRegexMatches
                    ? string.Format(LocalizationManager.Instance["Converter_Regex_MatchCountTruncatedFormat"], ConverterService.MaxRegexMatches)
                    : string.Format(LocalizationManager.Instance["Converter_Regex_MatchCountFormat"], matches.Count);
            }
            catch (Exception ex)
            {
                RegexErrorMessage = ex.Message;
            }
        }

        RegexMatchesUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>パターン変更時、インタラクティブモードが有効ならマッチ結果を再計算する。</summary>
    partial void OnRegexPatternTextChanged(string value)
    {
        if (RegexInteractive)
        {
            RecomputeRegexMatches();
        }
    }

    /// <summary>テスト文字列変更時、インタラクティブモードが有効ならマッチ結果を再計算する。</summary>
    partial void OnRegexTestInputTextChanged(string value)
    {
        if (RegexInteractive)
        {
            RecomputeRegexMatches();
        }
    }

    /// <summary>大文字小文字区別オプション変更時、インタラクティブモードが有効ならマッチ結果を再計算する。</summary>
    partial void OnRegexIgnoreCaseChanged(bool value)
    {
        if (RegexInteractive)
        {
            RecomputeRegexMatches();
        }
    }

    /// <summary>グローバル検索オプション変更時、インタラクティブモードが有効ならマッチ結果を再計算する。</summary>
    partial void OnRegexGlobalChanged(bool value)
    {
        if (RegexInteractive)
        {
            RecomputeRegexMatches();
        }
    }

    /// <summary>複数行モードオプション変更時、インタラクティブモードが有効ならマッチ結果を再計算する。</summary>
    partial void OnRegexMultilineChanged(bool value)
    {
        if (RegexInteractive)
        {
            RecomputeRegexMatches();
        }
    }

    /// <summary>インタラクティブモードをONにした際、現在の入力内容で即座に再評価する。</summary>
    partial void OnRegexInteractiveChanged(bool value)
    {
        if (value)
        {
            RecomputeRegexMatches();
        }
    }

    /// <summary>マッチ詳細表示の切替時は常に再評価し、詳細データの有無を最新化する。</summary>
    partial void OnRegexShowMatchDetailsChanged(bool value) => RecomputeRegexMatches();

    /// <summary>非インタラクティブモード時に、現在の入力内容で手動評価を実行する。</summary>
    [RelayCommand]
    private void EvaluateRegex() => RecomputeRegexMatches();

    /// <summary>正規表現テスターの入力・結果をすべてクリアする。</summary>
    [RelayCommand]
    private void ClearRegex()
    {
        RegexPatternText = string.Empty;
        RegexTestInputText = string.Empty;
        RecomputeRegexMatches();
    }

    /// <summary>パターン・テスト文字列欄に、指定パターンの動作確認用サンプルを挿入し、即座に評価する。</summary>
    /// <param name="sampleKey">"Email" (メールアドレス) / "Date" (日付/キャプチャグループ) / "Phone" (電話番号/単語境界)</param>
    [RelayCommand]
    private void InsertRegexSample(string sampleKey)
    {
        (RegexPatternText, RegexTestInputText) = sampleKey switch
        {
            "Date" => (ConverterService.RegexSampleDatePattern, ConverterService.RegexSampleDateInput),
            "Phone" => (ConverterService.RegexSamplePhonePattern, ConverterService.RegexSamplePhoneInput),
            _ => (ConverterService.RegexSampleEmailPattern, ConverterService.RegexSampleEmailInput),
        };

        RecomputeRegexMatches();
    }

    /// <summary>トークン変更時にJWTのデコード結果を再計算する。</summary>
    partial void OnJwtTokenTextChanged(string value) => RecomputeJwt();

    /// <summary>JWTをHeader/Payload/Signatureへデコードし、日時系クレームを抽出する。</summary>
    private void RecomputeJwt()
    {
        JwtErrorMessage = string.Empty;
        JwtHeaderJson = string.Empty;
        JwtPayloadJson = string.Empty;
        JwtSignatureText = string.Empty;
        JwtClaimDates.Clear();

        if (string.IsNullOrWhiteSpace(JwtTokenText))
        {
            return;
        }

        try
        {
            var (headerJson, payloadJson, signature) = ConverterService.DecodeJwt(JwtTokenText);
            JwtHeaderJson = headerJson;
            JwtPayloadJson = payloadJson;
            JwtSignatureText = signature;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (var (claimName, seconds) in ConverterService.ExtractJwtTimestampClaims(payloadJson))
            {
                string secondsText = seconds.ToString(CultureInfo.InvariantCulture);
                bool isExpired = claimName == "exp" && DateTimeOffset.FromUnixTimeSeconds(seconds) < now;
                JwtClaimDates.Add(new JwtClaimDateItem(
                    claimName,
                    ConverterService.UnixTimestampToDateTime(secondsText),
                    ConverterService.UnixTimestampToIso8601Utc(secondsText),
                    isExpired ? LocalizationManager.Instance["Converter_Jwt_Expired"] : string.Empty));
            }
        }
        catch (FormatException)
        {
            JwtErrorMessage = LocalizationManager.Instance["Converter_Jwt_InvalidFormat"];
        }
        catch (Exception ex)
        {
            JwtErrorMessage = ex.Message;
        }
    }

    /// <summary>JWTデコーダーの入力・結果をすべてクリアする。</summary>
    [RelayCommand]
    private void ClearJwt() => JwtTokenText = string.Empty;

    /// <summary>入力欄に、指定パターンの動作確認用サンプルJWTを挿入する。</summary>
    /// <param name="sampleKey">"Basic" (標準的なクレーム) / "Expired" (期限切れ) / "Roles" (配列クレーム)</param>
    [RelayCommand]
    private void InsertJwtSample(string sampleKey)
    {
        JwtTokenText = sampleKey switch
        {
            "Expired" => ConverterService.JwtSampleTokenExpired,
            "Roles" => ConverterService.JwtSampleTokenWithRoles,
            _ => ConverterService.JwtSampleTokenBasic,
        };
    }

    /// <summary>Unixタイムスタンプを日時・ISO8601形式へ変換する。</summary>
    [RelayCommand]
    private void TimestampToDate()
    {
        TimestampErrorMessage = string.Empty;
        try
        {
            DateOutputText = ConverterService.UnixTimestampToDateTime(TimestampInputText);
            Iso8601UtcOutputText = ConverterService.UnixTimestampToIso8601Utc(TimestampInputText);
            Iso8601LocalOutputText = ConverterService.UnixTimestampToIso8601Local(TimestampInputText);
        }
        catch (Exception ex)
        {
            TimestampErrorMessage = ex.Message;
        }
    }

    /// <summary>日時文字列をUnixタイムスタンプへ変換する。</summary>
    [RelayCommand]
    private void DateToTimestamp()
    {
        TimestampErrorMessage = string.Empty;
        try
        {
            TimestampOutputText = ConverterService.DateTimeToUnixTimestamp(DateInputText);
        }
        catch (Exception ex)
        {
            TimestampErrorMessage = ex.Message;
        }
    }

    /// <summary>現在時刻のUnixタイムスタンプを入力欄に設定する。</summary>
    [RelayCommand]
    private void UseCurrentTimestamp() => TimestampInputText = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    /// <summary>現在の日時を入力欄に設定する。</summary>
    [RelayCommand]
    private void UseCurrentDate() => DateInputText = DateTime.Now.ToString(ConverterService.TimestampDateFormat);

    /// <summary>文字列で指定されたUUIDバージョンへ切り替える。</summary>
    [RelayCommand]
    private void SelectUuidVersion(string versionName)
    {
        if (Enum.TryParse<UuidVersion>(versionName, out var version))
        {
            UuidVersion = version;
        }
    }

    /// <summary>UUID v5用の名前空間をプリセット値に設定する。</summary>
    [RelayCommand]
    private void SetNamespacePreset(string preset)
    {
        UuidNamespaceText = preset switch
        {
            "Dns" => ConverterService.NamespaceDns.ToString(),
            "Url" => ConverterService.NamespaceUrl.ToString(),
            "Oid" => ConverterService.NamespaceOid.ToString(),
            "X500" => ConverterService.NamespaceX500.ToString(),
            _ => UuidNamespaceText,
        };
    }

    /// <summary>選択中のバージョンに応じてUUIDを生成する。</summary>
    [RelayCommand]
    private void GenerateUuid()
    {
        UuidErrorMessage = string.Empty;
        try
        {
            _currentUuid = UuidVersion switch
            {
                UuidVersion.V7 => ConverterService.CreateUuidV7(),
                UuidVersion.V5 => ConverterService.CreateUuidV5(Guid.Parse(UuidNamespaceText.Trim()), UuidNameText),
                _ => Guid.NewGuid(),
            };
            FormatUuid();
        }
        catch (Exception ex)
        {
            UuidErrorMessage = ex.Message;
        }
    }

    /// <summary>ハイフン除去設定変更時にUUID表示を更新する。</summary>
    partial void OnUuidNoHyphensChanged(bool value) => FormatUuid();

    /// <summary>大文字表示設定変更時にUUID表示を更新する。</summary>
    partial void OnUuidUppercaseChanged(bool value) => FormatUuid();

    /// <summary>現在のUUIDを表示オプションに従って整形する。</summary>
    private void FormatUuid() => UuidResult = ConverterService.FormatUuid(_currentUuid, UuidNoHyphens, UuidUppercase);

    /// <summary>現在のオプションでパスワードを生成する。</summary>
    [RelayCommand]
    private void GeneratePassword()
    {
        PasswordErrorMessage = string.Empty;
        try
        {
            PasswordResult = ConverterService.GeneratePassword(
                PasswordLength,
                PasswordIncludeUppercase,
                PasswordIncludeLowercase,
                PasswordIncludeDigits,
                PasswordIncludeSymbols,
                PasswordExcludeAmbiguous);
        }
        catch (InvalidOperationException)
        {
            PasswordErrorMessage = LocalizationManager.Instance["Converter_Password_NoCharacterSetSelected"];
        }
        catch (Exception ex)
        {
            PasswordErrorMessage = ex.Message;
        }
    }

    /// <summary>指定したテキストをOSクリップボードへコピーする。</summary>
    [RelayCommand]
    private static void CopyToClipboard(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }
}
