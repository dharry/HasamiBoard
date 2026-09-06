using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using Cronos;
using Devlooped;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using NUglify;
using NUglify.Css;
using NUglify.Html;
using NUglify.JavaScript;
using QRCoder;
using SkiaSharp;
using ZXing.SkiaSharp;

namespace HasamiBoard.Services;

/// <summary>開発でよく使うテキスト変換 (ハッシュ、エンコード、JSON整形等) の純粋な変換ロジック。</summary>
public static class ConverterService
{
    public const string TimestampDateFormat = "yyyy/MM/dd HH:mm:ss";
    public const string Rfc3339LocalFormat = "yyyy-MM-ddTHH:mm:sszzz";
    public const string Rfc3339UtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

    // RFC4122 で定義済みの名前空間UUID (v5/v3 の名前ベースUUID生成で使用)
    public static readonly Guid NamespaceDns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    public static readonly Guid NamespaceUrl = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
    public static readonly Guid NamespaceOid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
    public static readonly Guid NamespaceX500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");

    private const string PasswordUppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string PasswordLowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string PasswordDigitChars = "0123456789";
    private const string PasswordSymbolChars = "!@#$%^&*()-_=+[]{}<>?";
    private const string AmbiguousChars = "0O1lI";

    /// <summary>テキストのMD5ハッシュ値を16進文字列で返す。</summary>
    /// <param name="text">ハッシュ計算対象テキスト</param>
    /// <returns>MD5ハッシュ値（16進文字列）</returns>
    public static string ComputeMd5(string text) => ToHex(MD5.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)));

    /// <summary>テキストのSHA1ハッシュ値を16進文字列で返す。</summary>
    /// <param name="text">ハッシュ計算対象テキスト</param>
    /// <returns>SHA1ハッシュ値（16進文字列）</returns>
    public static string ComputeSha1(string text) => ToHex(SHA1.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)));

    /// <summary>テキストのSHA256ハッシュ値を16進文字列で返す。</summary>
    /// <param name="text">ハッシュ計算対象テキスト</param>
    /// <returns>SHA256ハッシュ値（16進文字列）</returns>
    public static string ComputeSha256(string text) => ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)));

    /// <summary>テキストのSHA512ハッシュ値を16進文字列で返す。</summary>
    /// <param name="text">ハッシュ計算対象テキスト</param>
    /// <returns>SHA512ハッシュ値（16進文字列）</returns>
    public static string ComputeSha512(string text) => ToHex(SHA512.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)));

    /// <summary>バイト配列を小文字の16進文字列へ変換する。</summary>
    private static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();

    /// <summary>テキストをBase64文字列へエンコードする。</summary>
    /// <param name="text">エンコード対象テキスト</param>
    /// <returns>Base64エンコード文字列</returns>
    public static string Base64Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? string.Empty));

    /// <summary>Base64文字列をテキストへデコードする。</summary>
    /// <param name="text">デコード対象Base64文字列</param>
    /// <returns>デコード後のテキスト</returns>
    public static string Base64Decode(string text) => Encoding.UTF8.GetString(Convert.FromBase64String(text ?? string.Empty));

    /// <summary>テキストをURLエンコードする。</summary>
    /// <param name="text">URLエンコード対象テキスト</param>
    /// <returns>URLエンコード済み文字列</returns>
    public static string UrlEncode(string text) => Uri.EscapeDataString(text ?? string.Empty);

    /// <summary>URLエンコード済みテキストをデコードする。</summary>
    /// <param name="text">URLデコード対象文字列</param>
    /// <returns>デコード済みテキスト</returns>
    public static string UrlDecode(string text) => Uri.UnescapeDataString(text ?? string.Empty);

    /// <summary>テキストをHTMLエンティティへエンコードする。</summary>
    /// <param name="text">エンコード対象テキスト</param>
    /// <returns>HTMLエンティティ文字列</returns>
    public static string HtmlEncode(string text) => WebUtility.HtmlEncode(text ?? string.Empty) ?? string.Empty;

    /// <summary>HTMLエンティティをテキストへデコードする。</summary>
    /// <param name="text">HTMLエンティティ文字列</param>
    /// <returns>デコード済みテキスト</returns>
    public static string HtmlDecode(string text) => WebUtility.HtmlDecode(text ?? string.Empty) ?? string.Empty;

    /// <summary>日本語等の非ASCII文字を \uXXXX へエスケープせず、そのまま出力するための整形済みJSON用シリアライズオプション。
    /// (既定の <see cref="JavaScriptEncoder.Default"/> はHTML埋め込みを想定して非ASCII文字を積極的にエスケープするため、
    /// 単純なテキスト表示用途のこのアプリでは可読性を優先し <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> を使う。)</summary>
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>非ASCII文字をエスケープしない、圧縮 (1行) 出力用のJSONシリアライズオプション。<see cref="PrettyJsonOptions"/> 参照。</summary>
    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>JSON文字列をインデント付きで整形する。</summary>
    /// <param name="json">整形対象JSON文字列</param>
    /// <returns>整形済みJSON文字列</returns>
    public static string FormatJson(string json)
    {
        using var document = JsonDocument.Parse(json ?? string.Empty);
        return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
    }

    /// <summary>JSON文字列を空白を除去して圧縮する。</summary>
    /// <param name="json">圧縮対象JSON文字列</param>
    /// <returns>圧縮済みJSON文字列</returns>
    public static string MinifyJson(string json)
    {
        using var document = JsonDocument.Parse(json ?? string.Empty);
        return JsonSerializer.Serialize(document.RootElement, CompactJsonOptions);
    }

    /// <summary>XML文字列をインデント付きで整形する。</summary>
    /// <param name="xml">整形対象XML文字列</param>
    /// <returns>整形済みXML文字列</returns>
    public static string FormatXml(string xml)
    {
        var document = XDocument.Parse(xml ?? string.Empty, LoadOptions.PreserveWhitespace);
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = document.Declaration is null,
        };

        using var stringWriter = new StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
        {
            document.Save(xmlWriter);
        }

        return stringWriter.ToString();
    }

    /// <summary>HTML文字列をNUglifyで整形する。</summary>
    /// <param name="html">整形対象HTML文字列</param>
    /// <returns>整形済みHTML文字列</returns>
    public static string FormatHtml(string html) => RunNUglify(Uglify.Html(html ?? string.Empty, HtmlSettings.Pretty()));

    /// <summary>CSS文字列をNUglifyで整形する。</summary>
    /// <param name="css">整形対象CSS文字列</param>
    /// <returns>整形済みCSS文字列</returns>
    public static string FormatCss(string css) => RunNUglify(Uglify.Css(css ?? string.Empty, CssSettings.Pretty()));

    /// <summary>JavaScript文字列をNUglifyで整形する。</summary>
    /// <param name="js">整形対象JavaScript文字列</param>
    /// <returns>整形済みJavaScript文字列</returns>
    public static string FormatJavaScript(string js) => RunNUglify(Uglify.Js(js ?? string.Empty, CodeSettings.Pretty()));

    /// <summary>SQL文字列を構文解析し、指定オプションに従って整形する。</summary>
    /// <param name="sql">整形対象SQL文字列</param>
    /// <param name="options">整形オプション</param>
    /// <returns>整形済みSQL文字列</returns>
    public static string FormatSql(string sql, SqlScriptGeneratorOptions options)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql ?? string.Empty);
        TSqlFragment fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(e => e.Message)));
        }

        var generator = new Sql160ScriptGenerator(options);
        generator.GenerateScript(fragment, out string formatted);
        return formatted;
    }

    /// <summary>NUglifyの整形結果を検証し、エラー時は例外を送出する。</summary>
    private static string RunNUglify(UglifyResult result)
    {
        if (result.HasErrors)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        }

        return result.Code ?? string.Empty;
    }

    /// <summary>Unixタイムスタンプをローカル日時文字列へ変換する。</summary>
    /// <param name="timestampText">Unixタイムスタンプ</param>
    /// <returns>ローカル日時文字列</returns>
    public static string UnixTimestampToDateTime(string timestampText)
    {
        long seconds = long.Parse((timestampText ?? string.Empty).Trim(), CultureInfo.InvariantCulture);
        return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime.ToString(TimestampDateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Unixタイムスタンプを UTC の RFC3339/ISO8601 文字列へ変換する。</summary>
    /// <param name="timestampText">Unixタイムスタンプ</param>
    /// <returns>UTC RFC3339文字列</returns>
    public static string UnixTimestampToIso8601Utc(string timestampText)
    {
        long seconds = long.Parse((timestampText ?? string.Empty).Trim(), CultureInfo.InvariantCulture);
        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.ToString(Rfc3339UtcFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Unixタイムスタンプをローカルの RFC3339/ISO8601 文字列へ変換する。</summary>
    /// <param name="timestampText">Unixタイムスタンプ</param>
    /// <returns>ローカルRFC3339文字列</returns>
    public static string UnixTimestampToIso8601Local(string timestampText)
    {
        long seconds = long.Parse((timestampText ?? string.Empty).Trim(), CultureInfo.InvariantCulture);
        return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString(Rfc3339LocalFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>日時文字列 (通常形式またはRFC3339/ISO8601) をUnixタイムスタンプへ変換する。</summary>
    /// <param name="dateText">日時文字列</param>
    /// <returns>Unixタイムスタンプ</returns>
    public static string DateTimeToUnixTimestamp(string dateText)
    {
        string trimmed = (dateText ?? string.Empty).Trim();

        if (DateTime.TryParseExact(trimmed, TimestampDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime localDateTime))
        {
            return new DateTimeOffset(localDateTime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        }

        // yyyy/MM/dd HH:mm:ss 形式でなければ RFC3339/ISO8601 形式として解釈する
        var offset = DateTimeOffset.Parse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
        return offset.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>UUIDをハイフン有無・大文字小文字を指定して文字列化する。</summary>
    /// <param name="uuid">対象UUID</param>
    /// <param name="noHyphens">ハイフンを除去する場合true</param>
    /// <param name="uppercase">大文字表示する場合true</param>
    /// <returns>文字列化されたUUID</returns>
    public static string FormatUuid(Guid uuid, bool noHyphens, bool uppercase)
    {
        string text = uuid.ToString(noHyphens ? "N" : "D");
        return uppercase ? text.ToUpperInvariant() : text;
    }

    /// <summary>時刻順に並ぶバージョン7のUUIDを生成する。</summary>
    /// <returns>生成されたUUID v7</returns>
    public static Guid CreateUuidV7() => Guid.CreateVersion7();

    /// <summary>RFC4122 の名前ベース (SHA-1) v5 UUIDを生成する。</summary>
    /// <param name="namespaceId">名前空間UUID</param>
    /// <param name="name">名前文字列</param>
    /// <returns>生成されたUUID v5</returns>
    public static Guid CreateUuidV5(Guid namespaceId, string name)
    {
        byte[] namespaceBytes = ToRfc4122ByteOrder(namespaceId.ToByteArray());
        byte[] nameBytes = Encoding.UTF8.GetBytes(name ?? string.Empty);

        byte[] combined = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, combined, namespaceBytes.Length, nameBytes.Length);

        byte[] hash = SHA1.HashData(combined);
        byte[] guidBytes = hash[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50); // version 5
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // variant RFC4122

        return new Guid(ToRfc4122ByteOrder(guidBytes));
    }

    /// <summary>.NETの Guid.ToByteArray() の混在エンディアン順と RFC4122 のネットワークバイトオーダーを相互変換する (自己逆変換)。</summary>
    private static byte[] ToRfc4122ByteOrder(byte[] guidBytes)
    {
        Array.Reverse(guidBytes, 0, 4);
        Array.Reverse(guidBytes, 4, 2);
        Array.Reverse(guidBytes, 6, 2);
        return guidBytes;
    }

    /// <summary>指定条件に基づき暗号学的に安全なランダムパスワードを生成する。</summary>
    /// <param name="length">パスワード長</param>
    /// <param name="includeUppercase">大文字を含める場合true</param>
    /// <param name="includeLowercase">小文字を含める場合true</param>
    /// <param name="includeDigits">数字を含める場合true</param>
    /// <param name="includeSymbols">記号を含める場合true</param>
    /// <param name="excludeAmbiguous">紛らわしい文字を除外する場合true</param>
    /// <returns>生成されたパスワード</returns>
    public static string GeneratePassword(
        int length,
        bool includeUppercase,
        bool includeLowercase,
        bool includeDigits,
        bool includeSymbols,
        bool excludeAmbiguous)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var charsetBuilder = new StringBuilder();
        if (includeUppercase)
        {
            charsetBuilder.Append(PasswordUppercaseChars);
        }

        if (includeLowercase)
        {
            charsetBuilder.Append(PasswordLowercaseChars);
        }

        if (includeDigits)
        {
            charsetBuilder.Append(PasswordDigitChars);
        }

        if (includeSymbols)
        {
            charsetBuilder.Append(PasswordSymbolChars);
        }

        string charset = charsetBuilder.ToString();
        if (excludeAmbiguous)
        {
            charset = new string(charset.Where(c => !AmbiguousChars.Contains(c)).ToArray());
        }

        if (charset.Length == 0)
        {
            throw new InvalidOperationException("少なくとも1つの文字種を選択してください。");
        }

        return new string(RandomNumberGenerator.GetItems<char>(charset.ToCharArray(), length));
    }

    /// <summary>"/pattern/" 形式のリテラル文字列からスラッシュを除いたパターン本体を取り出す。
    /// 先頭と末尾がスラッシュで囲まれていない場合は文字列全体をそのままパターンとして扱う。</summary>
    /// <param name="literalText">ユーザー入力文字列</param>
    /// <returns>スラッシュを除いた正規表現パターン</returns>
    public static string ExtractRegexPattern(string literalText)
    {
        string text = literalText ?? string.Empty;
        if (text.Length >= 2 && text[0] == '/' && text[^1] == '/')
        {
            return text[1..^1];
        }

        return text;
    }

    /// <summary>正規表現テスターの評価タイムアウト。壊滅的バックトラッキング等によるUIのフリーズを防ぐ。</summary>
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>正規表現テスターで一度に処理するマッチ数の上限。大量マッチによる評価・描画コストを抑える。</summary>
    public const int MaxRegexMatches = 500;

    /// <summary>テスト文字列に対する正規表現マッチ一覧を返す (最大 <see cref="MaxRegexMatches"/> 件)。
    /// 不正なパターンや評価タイムアウト超過時は例外を送出する。</summary>
    /// <param name="pattern">正規表現パターン</param>
    /// <param name="input">マッチ対象のテスト文字列</param>
    /// <param name="options">正規表現オプション</param>
    /// <returns>マッチ結果一覧</returns>
    public static IReadOnlyList<Match> FindRegexMatches(string pattern, string input, RegexOptions options)
    {
        var regex = new Regex(pattern, options, RegexMatchTimeout);
        return regex.Matches(input ?? string.Empty).Cast<Match>().Take(MaxRegexMatches).ToList();
    }

    /// <summary>Unixタイムスタンプ (秒) の日時系クレームとして扱うJWTクレーム名。</summary>
    private static readonly string[] JwtTimestampClaimNames = ["exp", "iat", "nbf"];

    /// <summary>JWTのサンプルボタン(標準的なクレーム)で挿入する、jwt.ioの定番サンプルと同じ構成のJWT。</summary>
    public const string JwtSampleTokenBasic =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ." +
        "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    /// <summary>JWTのサンプルボタン(期限切れ)で挿入する、"exp"クレームが過去日時のJWT。</summary>
    public const string JwtSampleTokenExpired =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJzdWIiOiJ1c2VyLTEiLCJuYW1lIjoiRXhwaXJlZCBVc2VyIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjE1MTYyMzkwMjJ9." +
        "demo-signature-not-verified";

    /// <summary>JWTのサンプルボタン(配列クレーム)で挿入する、"roles"配列クレームを含むJWT。</summary>
    public const string JwtSampleTokenWithRoles =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJzdWIiOiJ1c2VyLTQyIiwibmFtZSI6IkFsaWNlIiwicm9sZXMiOlsiYWRtaW4iLCJlZGl0b3IiXSwiaWF0IjoxNzAwMDAwMDAwLCJleHAiOjE5OTk5OTk5OTl9." +
        "demo-signature-not-verified";

    /// <summary>正規表現テスターのサンプルボタン(メールアドレス)で挿入するパターン。</summary>
    public const string RegexSampleEmailPattern = @"[\w.+-]+@[\w-]+\.[\w.-]+";

    /// <summary>正規表現テスターのサンプルボタン(メールアドレス)で挿入するテスト文字列。</summary>
    public const string RegexSampleEmailInput = "お問い合わせは support@example.com または sales@example.co.jp までご連絡ください。";

    /// <summary>正規表現テスターのサンプルボタン(日付/キャプチャグループ)で挿入するパターン。</summary>
    public const string RegexSampleDatePattern = @"(\d{4})-(\d{2})-(\d{2})";

    /// <summary>正規表現テスターのサンプルボタン(日付/キャプチャグループ)で挿入するテスト文字列。</summary>
    public const string RegexSampleDateInput = "開始日: 2026-01-15、終了日: 2026-12-31";

    /// <summary>正規表現テスターのサンプルボタン(電話番号/単語境界)で挿入するパターン。</summary>
    public const string RegexSamplePhonePattern = @"\b0\d{1,4}-\d{1,4}-\d{4}\b";

    /// <summary>正規表現テスターのサンプルボタン(電話番号/単語境界)で挿入するテスト文字列。</summary>
    public const string RegexSamplePhoneInput = "電話番号は 03-1234-5678 または 090-1234-5678 です。";

    /// <summary>JWT (JSON Web Token) を Header / Payload / Signature の3パートへ分解し、Header/Payloadを整形JSON文字列で返す。
    /// 署名の検証は秘密鍵/公開鍵を要するため行わない。不正な形式・Base64URL・JSONの場合は例外を送出する。</summary>
    /// <param name="token">"header.payload.signature" 形式のJWT文字列</param>
    /// <returns>整形済みHeader JSON、整形済みPayload JSON、Signature (Base64URL文字列) のタプル</returns>
    public static (string HeaderJson, string PayloadJson, string Signature) DecodeJwt(string token)
    {
        string trimmed = (token ?? string.Empty).Trim();
        string[] parts = trimmed.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
        {
            throw new FormatException("JWT must consist of three dot-separated, non-empty parts (header.payload.signature).");
        }

        string headerJson = FormatJson(DecodeBase64UrlToString(parts[0]));
        string payloadJson = FormatJson(DecodeBase64UrlToString(parts[1]));
        return (headerJson, payloadJson, parts[2]);
    }

    /// <summary>整形済みJWTペイロードJSONから exp/iat/nbf の日時系クレームを抽出する (存在するクレームのみ)。</summary>
    /// <param name="payloadJson">整形済みペイロードJSON文字列</param>
    /// <returns>クレーム名とUnixタイムスタンプ (秒) のペア一覧</returns>
    public static IReadOnlyList<(string ClaimName, long UnixSeconds)> ExtractJwtTimestampClaims(string payloadJson)
    {
        var result = new List<(string, long)>();
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson);
        foreach (string claimName in JwtTimestampClaimNames)
        {
            if (document.RootElement.TryGetProperty(claimName, out var value) && value.TryGetInt64(out long seconds))
            {
                result.Add((claimName, seconds));
            }
        }

        return result;
    }

    /// <summary>Base64URL (RFC4648 §5、パディングなし) 文字列をUTF-8テキストへデコードする。</summary>
    private static string DecodeBase64UrlToString(string base64Url)
    {
        string base64 = base64Url.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid Base64URL string length."),
        };

        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    /// <summary>識別子の単語区切りを検出する正規表現。
    /// 明示的な区切り文字 (スペース・ハイフン・アンダースコア・ピリオド) に加え、
    /// camelCase/PascalCaseの大文字境界、および連続大文字 (略語) の直後の単語境界を単語の区切りとみなす。</summary>
    private static readonly Regex CaseWordSplitRegex = new(
        @"[-_.\s]+|(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])",
        RegexOptions.Compiled);

    /// <summary>入力文字列を記法によらず単語単位に分割する (camelCase/PascalCase/snake_case/kebab-case/CONSTANT_CASE等すべてに対応)。</summary>
    /// <param name="text">分割対象の文字列</param>
    /// <returns>分割された単語一覧</returns>
    private static IReadOnlyList<string> SplitIntoWords(string text)
    {
        return CaseWordSplitRegex.Split(text ?? string.Empty)
            .Where(w => !string.IsNullOrEmpty(w))
            .ToList();
    }

    /// <summary>単語の先頭のみ大文字化し、残りを小文字化する。</summary>
    private static string CapitalizeWord(string word)
        => word.Length <= 1 ? word.ToUpperInvariant() : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    /// <summary>任意の記法の識別子を camelCase へ変換する。</summary>
    /// <param name="text">変換対象の文字列</param>
    /// <returns>camelCase文字列</returns>
    public static string ToCamelCase(string text)
    {
        var words = SplitIntoWords(text);
        if (words.Count == 0)
        {
            return string.Empty;
        }

        return words[0].ToLowerInvariant() + string.Concat(words.Skip(1).Select(CapitalizeWord));
    }

    /// <summary>任意の記法の識別子を PascalCase へ変換する。</summary>
    /// <param name="text">変換対象の文字列</param>
    /// <returns>PascalCase文字列</returns>
    public static string ToPascalCase(string text) => string.Concat(SplitIntoWords(text).Select(CapitalizeWord));

    /// <summary>任意の記法の識別子を snake_case へ変換する。</summary>
    /// <param name="text">変換対象の文字列</param>
    /// <returns>snake_case文字列</returns>
    public static string ToSnakeCase(string text) => string.Join("_", SplitIntoWords(text).Select(w => w.ToLowerInvariant()));

    /// <summary>任意の記法の識別子を kebab-case へ変換する。</summary>
    /// <param name="text">変換対象の文字列</param>
    /// <returns>kebab-case文字列</returns>
    public static string ToKebabCase(string text) => string.Join("-", SplitIntoWords(text).Select(w => w.ToLowerInvariant()));

    /// <summary>任意の記法の識別子を CONSTANT_CASE (SCREAMING_SNAKE_CASE) へ変換する。</summary>
    /// <param name="text">変換対象の文字列</param>
    /// <returns>CONSTANT_CASE文字列</returns>
    public static string ToConstantCase(string text) => string.Join("_", SplitIntoWords(text).Select(w => w.ToUpperInvariant()));

    /// <summary>進数変換の入力文字列から接頭辞 (0x, 0o, 0b) とアンダースコア区切りを取り除く。</summary>
    private static string CleanNumberBaseText(string text, int fromBase)
    {
        string cleaned = (text ?? string.Empty).Trim().Replace("_", string.Empty);
        if (fromBase == 16 && cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[2..];
        }
        else if (fromBase == 8 && cleaned.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[2..];
        }
        else if (fromBase == 2 && cleaned.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[2..];
        }

        return cleaned;
    }

    /// <summary>指定した基数の文字列を64bit符号なし整数への変換を試みる。
    /// "0x"/"0b"接頭辞やアンダースコア区切り (例: "1_000", "0xFF_FF") にも対応する。</summary>
    /// <param name="text">変換対象の文字列</param>
    /// <param name="fromBase">入力の基数 (2, 8, 10, 16のいずれか)</param>
    /// <param name="value">変換結果</param>
    /// <returns>変換に成功した場合true</returns>
    public static bool TryParseNumberBase(string text, int fromBase, out ulong value)
    {
        value = 0;
        string cleaned = CleanNumberBaseText(text, fromBase);
        if (cleaned.Length == 0)
        {
            return false;
        }

        try
        {
            value = Convert.ToUInt64(cleaned, fromBase);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>64bit符号なし整数を指定した基数の文字列へ変換する。
    /// 2/8/16進は64bitのビットパターンをそのまま表現する (符号拡張を利用)。16進は大文字で返す。</summary>
    /// <param name="value">変換対象の値</param>
    /// <param name="toBase">出力の基数 (2, 8, 10, 16のいずれか)</param>
    /// <param name="includePrefix">true の場合、16進は"0x"、8進は"0o"、2進は"0B"を先頭に付与する (10進には付与しない)</param>
    /// <returns>変換後の文字列</returns>
    public static string FormatNumberBase(ulong value, int toBase, bool includePrefix = false)
    {
        string formatted = toBase switch
        {
            2 => Convert.ToString(unchecked((long)value), 2),
            8 => Convert.ToString(unchecked((long)value), 8),
            16 => Convert.ToString(unchecked((long)value), 16).ToUpperInvariant(),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

        if (!includePrefix)
        {
            return formatted;
        }

        return toBase switch
        {
            16 => "0x" + formatted,
            8 => "0o" + formatted,
            2 => "0B" + formatted,
            _ => formatted,
        };
    }

    /// <summary>指定テキストをQRコードとして生成し、PNG形式の画像バイト列として返す。</summary>
    /// <param name="text">エンコードする文字列</param>
    /// <param name="eccLevel">誤り訂正レベル</param>
    /// <param name="pixelsPerModule">1モジュールあたりのピクセル数</param>
    /// <returns>生成されたQRコード画像のPNGバイト列</returns>
    public static byte[] GenerateQrCodePng(string text, QRCodeGenerator.ECCLevel eccLevel, int pixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(text, eccLevel);
        var pngQrCode = new PngByteQRCode(qrData);
        return pngQrCode.GetGraphic(pixelsPerModule, drawQuietZones: true);
    }

    /// <summary>PNGバイト列をWPFで表示可能な<see cref="BitmapImage"/>へ変換する。</summary>
    /// <param name="pngBytes">PNG形式の画像バイト列</param>
    /// <returns>読み込み済みの<see cref="BitmapImage"/></returns>
    public static BitmapImage PngBytesToBitmapImage(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>画像内のQRコードを読み取り、埋め込まれたテキストを返す。</summary>
    /// <param name="image">読み取り対象の画像</param>
    /// <returns>デコードされた文字列。QRコードが検出できなかった場合はnull</returns>
    public static string? DecodeQrCode(BitmapSource image)
    {
        using var pngStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(pngStream);
        pngStream.Position = 0;

        using var bitmap = SKBitmap.Decode(pngStream);
        if (bitmap is null)
        {
            return null;
        }

        var reader = new BarcodeReader
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                PossibleFormats = new List<ZXing.BarcodeFormat> { ZXing.BarcodeFormat.QR_CODE },
                TryHarder = true,
            },
        };

        return reader.Decode(bitmap)?.Text;
    }

    /// <summary>Cron式を解析し、指定した起点日時より後の次回実行日時を指定件数分計算する。</summary>
    /// <param name="expression">Cron式</param>
    /// <param name="format">フィールド形式 (標準5フィールド or 秒付き6フィールド)</param>
    /// <param name="fromText">計算の起点となる日時文字列 (空の場合は現在時刻)</param>
    /// <param name="count">計算する件数</param>
    /// <returns>次回実行日時の一覧 (ローカル日時文字列、起点に近い順)</returns>
    public static IReadOnlyList<string> GetNextCronOccurrences(string expression, CronFormat format, string fromText, int count)
    {
        var cron = CronExpression.Parse(expression, format);
        DateTimeOffset current = ParseCronFromDateTime(fromText);

        var results = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            DateTimeOffset? next = cron.GetNextOccurrence(current, TimeZoneInfo.Local);
            if (next is null)
            {
                break;
            }

            results.Add(next.Value.ToString(TimestampDateFormat, CultureInfo.InvariantCulture));
            current = next.Value;
        }

        return results;
    }

    /// <summary>Cron式の計算起点日時文字列を解析する。空の場合は現在時刻を返す。</summary>
    private static DateTimeOffset ParseCronFromDateTime(string fromText)
    {
        string trimmed = (fromText ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return DateTimeOffset.Now;
        }

        if (DateTime.TryParseExact(trimmed, TimestampDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime localDateTime))
        {
            return new DateTimeOffset(localDateTime);
        }

        // yyyy/MM/dd HH:mm:ss 形式でなければ RFC3339/ISO8601 形式として解釈する
        return DateTimeOffset.Parse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    /// <summary>選択された曜日番号 (0=日曜〜6=土曜) から、Cron式の曜日フィールド文字列を組み立てる。</summary>
    /// <param name="selectedDays">選択された曜日番号の集合</param>
    /// <param name="field">組み立てられた曜日フィールド文字列 (全曜日選択時は"*")</param>
    /// <returns>1つ以上の曜日が選択されていればtrue。選択が無い場合はfalseで<paramref name="field"/>は空文字</returns>
    public static bool TryBuildCronDayOfWeekField(IReadOnlyCollection<int> selectedDays, out string field)
    {
        if (selectedDays.Count == 0)
        {
            field = string.Empty;
            return false;
        }

        field = selectedDays.Count == 7 ? "*" : string.Join(",", selectedDays.OrderBy(d => d));
        return true;
    }

    /// <summary>Cron式を人間が読める日本語の説明文へ変換する。変換できない場合は空文字を返す (次回実行時刻計算の成否には影響しない)。</summary>
    /// <param name="expression">Cron式</param>
    /// <returns>説明文。変換できない場合は空文字</returns>
    public static string DescribeCron(string expression)
    {
        try
        {
            return CronExpressionDescriptor.ExpressionDescriptor.GetDescription(
                expression,
                new CronExpressionDescriptor.Options { Locale = "ja" });
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>ユーザー名とパスワードから、HTTP Basic認証の Authorization ヘッダー値を生成する。</summary>
    /// <param name="username">ユーザー名</param>
    /// <param name="password">パスワード</param>
    /// <returns>"Authorization: Basic ..." 形式の文字列</returns>
    public static string BuildBasicAuthHeader(string username, string password)
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return $"Authorization: Basic {encoded}";
    }

    /// <summary>ユーザー名とパスワードから、userinfo形式のURL ("http://user:pass@example.com") を生成する。</summary>
    /// <param name="username">ユーザー名</param>
    /// <param name="password">パスワード</param>
    /// <returns>userinfo形式のURL文字列</returns>
    public static string BuildBasicAuthUserInfoUrl(string username, string password)
    {
        string encodedUsername = Uri.EscapeDataString(username);
        string encodedPassword = Uri.EscapeDataString(password);
        return $"http://{encodedUsername}:{encodedPassword}@example.com";
    }

    /// <summary>bcryptでハッシュ化した .htpasswd 形式の1行 ("ユーザー名:ハッシュ") を生成する。</summary>
    /// <param name="username">ユーザー名</param>
    /// <param name="password">パスワード</param>
    /// <param name="cost">bcryptのコスト係数</param>
    /// <returns>.htpasswd形式の1行</returns>
    public static string BuildHtpasswdLineBcrypt(string username, string password, int cost)
        => $"{username}:{BCrypt.Net.BCrypt.HashPassword(password, cost)}";

    /// <summary>Apache httpd 独自のMD5crypt (APR1) でハッシュ化した .htpasswd 形式の1行を生成する。
    /// ソルトは呼び出しごとにランダム生成するため、実行のたびに異なるハッシュ文字列になる (どちらも有効な値)。</summary>
    /// <param name="username">ユーザー名</param>
    /// <param name="password">パスワード</param>
    /// <returns>.htpasswd形式の1行</returns>
    public static string BuildHtpasswdLineApr1(string username, string password)
        => BuildHtpasswdLineApr1(username, password, GenerateApr1Salt());

    /// <summary>ソルトを指定してAPR1ハッシュの .htpasswd 形式の1行を生成する (主にテスト用)。</summary>
    /// <param name="username">ユーザー名</param>
    /// <param name="password">パスワード</param>
    /// <param name="salt">ソルト (英数字と './' の8文字)</param>
    /// <returns>.htpasswd形式の1行</returns>
    public static string BuildHtpasswdLineApr1(string username, string password, string salt)
        => $"{username}:{Apr1Crypt(password, salt)}";

    /// <summary>SHA1 (レガシー、"{SHA}"接頭辞、ソルト無し) でハッシュ化した .htpasswd 形式の1行を生成する。</summary>
    /// <param name="username">ユーザー名</param>
    /// <param name="password">パスワード</param>
    /// <returns>.htpasswd形式の1行</returns>
    public static string BuildHtpasswdLineSha1(string username, string password)
        => $"{username}:{{SHA}}{Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(password)))}";

    private const string Apr1SaltChars = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>APR1形式のソルトとして使う、英数字と './' からなる8文字のランダム文字列を生成する。</summary>
    private static string GenerateApr1Salt()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);

        var builder = new StringBuilder(8);
        foreach (byte b in buffer)
        {
            // Apr1SaltChars は64文字 (256 は64の倍数) のため、剰余による偏りは生じない
            builder.Append(Apr1SaltChars[b % Apr1SaltChars.Length]);
        }

        return builder.ToString();
    }

    /// <summary>Apache httpd の apr_md5.c と同一のAPR1 (Apache版MD5crypt) アルゴリズムでパスワードをハッシュ化する。
    /// "openssl passwd -apr1" と同一の出力になることをテストで確認済み。</summary>
    /// <param name="password">パスワード</param>
    /// <param name="salt">ソルト (英数字と './' の8文字)</param>
    /// <returns>"$apr1$ソルト$ハッシュ" 形式の文字列</returns>
    private static string Apr1Crypt(string password, string salt)
    {
        byte[] pwBytes = Encoding.UTF8.GetBytes(password);
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        byte[] magic = Encoding.ASCII.GetBytes("$apr1$");

        using var md5 = MD5.Create();

        var ctx2Input = new List<byte>(pwBytes.Length * 2 + saltBytes.Length);
        ctx2Input.AddRange(pwBytes);
        ctx2Input.AddRange(saltBytes);
        ctx2Input.AddRange(pwBytes);
        byte[] finalBytes = md5.ComputeHash(ctx2Input.ToArray());

        var ctx1Input = new List<byte>();
        ctx1Input.AddRange(pwBytes);
        ctx1Input.AddRange(magic);
        ctx1Input.AddRange(saltBytes);

        for (int pl = pwBytes.Length; pl > 0; pl -= 16)
        {
            ctx1Input.AddRange(finalBytes.Take(Math.Min(pl, 16)));
        }

        for (int i = pwBytes.Length; i != 0; i >>= 1)
        {
            ctx1Input.Add((i & 1) != 0 ? (byte)0 : pwBytes[0]);
        }

        finalBytes = md5.ComputeHash(ctx1Input.ToArray());

        for (int i = 0; i < 1000; i++)
        {
            var roundInput = new List<byte>();
            roundInput.AddRange((i & 1) != 0 ? pwBytes : finalBytes);

            if (i % 3 != 0)
            {
                roundInput.AddRange(saltBytes);
            }

            if (i % 7 != 0)
            {
                roundInput.AddRange(pwBytes);
            }

            roundInput.AddRange((i & 1) != 0 ? finalBytes : pwBytes);
            finalBytes = md5.ComputeHash(roundInput.ToArray());
        }

        var result = new StringBuilder();
        void EncodeTriplet(byte b2, byte b1, byte b0, int charCount)
        {
            uint w = ((uint)b2 << 16) | ((uint)b1 << 8) | b0;
            for (int i = 0; i < charCount; i++)
            {
                result.Append(Apr1SaltChars[(int)(w & 0x3f)]);
                w >>= 6;
            }
        }

        EncodeTriplet(finalBytes[0], finalBytes[6], finalBytes[12], 4);
        EncodeTriplet(finalBytes[1], finalBytes[7], finalBytes[13], 4);
        EncodeTriplet(finalBytes[2], finalBytes[8], finalBytes[14], 4);
        EncodeTriplet(finalBytes[3], finalBytes[9], finalBytes[15], 4);
        EncodeTriplet(finalBytes[4], finalBytes[10], finalBytes[5], 4);
        EncodeTriplet(0, 0, finalBytes[11], 2);

        return $"$apr1${salt}${result}";
    }

    /// <summary>jqテストのキーパス・ナビゲーターで一度に表示するノード数の上限。巨大JSONによるUI描画コストを抑える。</summary>
    public const int MaxJqTreeNodes = 500;

    /// <summary>jqテストのサンプルJSONボタン(シンプル)で挿入する、フラットなオブジェクトのサンプルJSON。</summary>
    public const string JqSampleJsonSimple = """
        {
          "id": 101,
          "name": "Wireless Mouse",
          "price": 29.99,
          "inStock": true,
          "discount": null
        }
        """;

    /// <summary>jqテストのサンプルJSONボタン(ネスト)で挿入する、ネストしたオブジェクト・配列を含むサンプルJSON。</summary>
    public const string JqSampleJsonNested = """
        {
          "users": [
            { "id": 1, "name": "Alice", "active": true, "roles": ["admin", "editor"] },
            { "id": 2, "name": "Bob", "active": false, "roles": ["viewer"] }
          ],
          "total": 2
        }
        """;

    /// <summary>jqテストのサンプルJSONボタン(配列)で挿入する、ルートが配列のサンプルJSON。</summary>
    public const string JqSampleJsonArray = """
        [
          { "id": "ord-1", "customer": "Alice", "amount": 120.5, "items": ["pen", "notebook"] },
          { "id": "ord-2", "customer": "Bob", "amount": 75, "items": ["mug"] },
          { "id": "ord-3", "customer": "Carol", "amount": 200, "items": ["laptop", "mouse", "bag"] }
        ]
        """;

    private static readonly Regex JqIdentifierKeyRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>入力JSONとjqフィルタ式から評価結果を返す (一覧の各要素がjqの1出力行に相当)。</summary>
    /// <param name="json">入力JSON文字列</param>
    /// <param name="filter">jqフィルタ式</param>
    /// <param name="rawOutput">true の場合、文字列型の結果を引用符なしの生文字列として出力する (jqの <c>-r</c> 相当)</param>
    /// <param name="compactOutput">true の場合、結果をコンパクトな1行JSONとして出力する (jqの <c>-c</c> 相当)</param>
    /// <param name="slurpInput">true の場合、入力内の複数のJSON値 (JSON Lines等) をまとめて1つの配列として評価する (jqの <c>-s</c> 相当)</param>
    /// <param name="nullInput">true の場合、<paramref name="json"/> を無視して null を入力として評価する (jqの <c>-n</c> 相当)</param>
    /// <returns>評価結果の一覧 (フィルタが値を生成しない場合は空)</returns>
    /// <exception cref="JqException">フィルタの構文または評価時エラー</exception>
    /// <exception cref="JsonException">入力JSONが不正な場合</exception>
    public static IReadOnlyList<string> EvaluateJq(string json, string filter, bool rawOutput, bool compactOutput, bool slurpInput, bool nullInput)
    {
        string effectiveJson = nullInput ? "null" : slurpInput ? BuildJqSlurpArrayJson(json) : json ?? string.Empty;
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(effectiveJson) ? "null" : effectiveJson);

        var serializerOptions = compactOutput ? CompactJsonOptions : PrettyJsonOptions;
        var lines = new List<string>();
        foreach (var element in Jq.Evaluate(filter, document.RootElement))
        {
            lines.Add(rawOutput && element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? string.Empty
                : JsonSerializer.Serialize(element, serializerOptions));
        }

        return lines;
    }

    /// <summary>複数のトップレベルJSON値 (JSON Lines等) を含む文字列から、1つのJSON配列文字列を組み立てる (jqの <c>-s</c> / slurp 相当)。</summary>
    /// <param name="json">連続する複数のJSON値を含みうる入力文字列</param>
    /// <returns>各値を要素として結合したJSON配列の文字列表現</returns>
    private static string BuildJqSlurpArrayJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "[]";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var values = new List<string>();
        int offset = 0;

        // Utf8JsonReader(isFinalBlock: true) は「バッファ全体が単一のJSON値」を前提に末尾の余剰データを検証するため、
        // 複数のトップレベル値を読むには、1値読むごとに残りのバイト列で新しいReaderを作り直す必要がある。
        while (offset < bytes.Length)
        {
            var reader = new Utf8JsonReader(bytes.AsSpan(offset), isFinalBlock: true, state: default);
            if (!reader.Read())
            {
                break;
            }

            reader.Skip();
            int consumed = (int)reader.BytesConsumed;
            string value = Encoding.UTF8.GetString(bytes, offset, consumed).Trim();
            if (value.Length > 0)
            {
                values.Add(value);
            }

            offset += consumed;
        }

        return $"[{string.Join(",", values)}]";
    }

    /// <summary>jqテストのキーパス・ナビゲーター用に、入力JSONの構造をツリー化する。
    /// 入力が空/不正なJSONの場合は空配列を返す (呼び出し側でのライブ更新を安全にするため例外を投げない)。</summary>
    /// <param name="json">入力JSON文字列</param>
    /// <returns>ルート直下のノード一覧 (総ノード数は <see cref="MaxJqTreeNodes"/> で打ち切られる)</returns>
    public static IReadOnlyList<JqPathNode> BuildJqPathTree(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<JqPathNode>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            int remaining = MaxJqTreeNodes;
            return BuildJqPathNodeChildren(document.RootElement, string.Empty, ref remaining);
        }
        catch (JsonException)
        {
            return Array.Empty<JqPathNode>();
        }
    }

    private static IReadOnlyList<JqPathNode> BuildJqPathNodeChildren(JsonElement element, string parentPath, ref int remaining)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var nodes = new List<JqPathNode>();
                foreach (var property in element.EnumerateObject())
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    remaining--;
                    string path = parentPath + FormatJqObjectKeySegment(property.Name);
                    var children = BuildJqPathNodeChildren(property.Value, path, ref remaining);
                    nodes.Add(new JqPathNode($"{property.Name}: {DescribeJqValue(property.Value)}", path, children));
                }

                return nodes;
            }
            case JsonValueKind.Array:
            {
                var nodes = new List<JqPathNode>();
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    remaining--;
                    string path = (parentPath.Length == 0 ? "." : parentPath) + $"[{index}]";
                    var children = BuildJqPathNodeChildren(item, path, ref remaining);
                    nodes.Add(new JqPathNode($"[{index}]: {DescribeJqValue(item)}", path, children));
                    index++;
                }

                return nodes;
            }
            default:
                return Array.Empty<JqPathNode>();
        }
    }

    /// <summary>オブジェクトキーから、識別子として安全ならドット記法、そうでなければブラケット記法のjqパス断片を組み立てる。</summary>
    private static string FormatJqObjectKeySegment(string key)
    {
        if (JqIdentifierKeyRegex.IsMatch(key))
        {
            return $".{key}";
        }

        string escaped = key.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $".[\"{escaped}\"]";
    }

    /// <summary>ツリーノードのラベルに添える、値の種類に応じた短いプレビュー文字列を返す。</summary>
    private static string DescribeJqValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => $"{{{value.EnumerateObject().Count()}}}",
        JsonValueKind.Array => $"[{value.GetArrayLength()}]",
        JsonValueKind.String => $"\"{TruncateForPreview(value.GetString() ?? string.Empty)}\"",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => "null",
    };

    private static string TruncateForPreview(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
