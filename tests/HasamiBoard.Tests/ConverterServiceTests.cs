using System;
using System.Linq;
using System.Text.RegularExpressions;
using Cronos;
using Devlooped;
using HasamiBoard.Services;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Xunit;

namespace HasamiBoard.Tests;

public class ConverterServiceTests
{
    [Fact]
    public void ComputeMd5_ReturnsKnownVector() =>
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", ConverterService.ComputeMd5("abc"));

    [Fact]
    public void ComputeSha1_ReturnsKnownVector() =>
        Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", ConverterService.ComputeSha1("abc"));

    [Fact]
    public void ComputeSha256_ReturnsKnownVector() =>
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ConverterService.ComputeSha256("abc"));

    [Fact]
    public void ComputeSha512_ReturnsKnownVector() =>
        Assert.Equal(
            "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f",
            ConverterService.ComputeSha512("abc"));

    [Theory]
    [InlineData("Hello, World!")]
    [InlineData("")]
    [InlineData("日本語テキスト")]
    public void Base64_RoundTrips(string text)
        => Assert.Equal(text, ConverterService.Base64Decode(ConverterService.Base64Encode(text)));

    [Fact]
    public void Base64Decode_InvalidInput_Throws()
        => Assert.ThrowsAny<FormatException>(() => ConverterService.Base64Decode("not-valid-base64!!"));

    [Theory]
    [InlineData("a b/c?d=e&f")]
    [InlineData("日本語 パス")]
    public void UrlEncode_RoundTrips(string text)
        => Assert.Equal(text, ConverterService.UrlDecode(ConverterService.UrlEncode(text)));

    [Fact]
    public void UrlEncode_EncodesSpaceAsPercent20()
        => Assert.Equal("a%20b", ConverterService.UrlEncode("a b"));

    [Fact]
    public void HtmlEncode_EscapesReservedCharacters()
        => Assert.Equal("&lt;div class=&quot;a&quot;&gt;Tom &amp; Jerry&lt;/div&gt;", ConverterService.HtmlEncode("<div class=\"a\">Tom & Jerry</div>"));

    [Fact]
    public void HtmlDecode_ReversesHtmlEncode()
    {
        string original = "<div>Tom & Jerry's \"cafe\"</div>";
        Assert.Equal(original, ConverterService.HtmlDecode(ConverterService.HtmlEncode(original)));
    }

    [Fact]
    public void FormatJson_AddsIndentation()
    {
        string result = ConverterService.FormatJson("{\"a\":1,\"b\":[1,2]}");
        Assert.Contains("\n", result);
        Assert.Contains("  ", result);
    }

    [Fact]
    public void MinifyJson_RemovesWhitespace()
    {
        string result = ConverterService.MinifyJson("{\n  \"a\": 1,\n  \"b\": [1, 2]\n}");
        Assert.Equal("{\"a\":1,\"b\":[1,2]}", result);
    }

    [Fact]
    public void FormatJson_InvalidJson_Throws()
        => Assert.ThrowsAny<Exception>(() => ConverterService.FormatJson("{not valid json"));

    [Fact]
    public void FormatJson_DoesNotEscapeNonAsciiCharacters()
    {
        string result = ConverterService.FormatJson("""{"name":"こんにちは"}""");
        Assert.Contains("こんにちは", result);
        Assert.DoesNotContain("\\u", result);
    }

    [Fact]
    public void MinifyJson_DoesNotEscapeNonAsciiCharacters()
    {
        string result = ConverterService.MinifyJson("""{"name":"こんにちは"}""");
        Assert.Equal("""{"name":"こんにちは"}""", result);
    }

    [Fact]
    public void FormatXml_AddsIndentation()
    {
        string result = ConverterService.FormatXml("<root><a>1</a><b>2</b></root>");
        Assert.Contains("\n", result);
        Assert.Contains("<a>1</a>", result);
        Assert.Contains("<b>2</b>", result);
    }

    [Fact]
    public void FormatXml_InvalidXml_Throws()
        => Assert.ThrowsAny<Exception>(() => ConverterService.FormatXml("<root><a></root>"));

    [Fact]
    public void FormatHtml_AddsIndentationAndPreservesContent()
    {
        string result = ConverterService.FormatHtml("<div><p>hello</p><p>world</p></div>");
        Assert.Contains("\n", result);
        Assert.Contains("hello", result);
        Assert.Contains("world", result);
    }

    [Fact]
    public void FormatCss_AddsIndentationAndPreservesDeclarations()
    {
        string result = ConverterService.FormatCss("body{margin:0;padding:10px}");
        Assert.Contains("\n", result);
        Assert.Contains("margin", result);
        Assert.Contains("padding", result);
        Assert.Contains("10px", result);
    }

    [Fact]
    public void FormatCss_InvalidCss_Throws()
        => Assert.ThrowsAny<Exception>(() => ConverterService.FormatCss("body { color: ; ] }"));

    [Fact]
    public void FormatJavaScript_AddsIndentationAndPreservesLogic()
    {
        string result = ConverterService.FormatJavaScript("function add(a,b){return a+b;}");
        Assert.Contains("\n", result);
        Assert.Contains("function", result);
        Assert.Contains("add", result);
    }

    [Fact]
    public void FormatJavaScript_InvalidSyntax_Throws()
        => Assert.ThrowsAny<Exception>(() => ConverterService.FormatJavaScript("function ( { + + + )"));

    private static readonly SqlScriptGeneratorOptions DefaultSqlOptions = new()
    {
        KeywordCasing = KeywordCasing.Uppercase,
        CommaPlacement = CommaPlacement.Trailing,
        IndentationSize = 4,
        IncludeSemicolons = true,
        AlignClauseBodies = true,
    };

    [Fact]
    public void FormatSql_AddsIndentationAndUppercasesKeywords()
    {
        string result = ConverterService.FormatSql("select a.id, a.name from tablea a where a.id = 1", DefaultSqlOptions);
        Assert.Contains("\n", result);
        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
        Assert.Contains("tablea", result);
    }

    [Fact]
    public void FormatSql_InvalidSql_Throws()
        => Assert.ThrowsAny<Exception>(() => ConverterService.FormatSql("SELECT * FROM (", DefaultSqlOptions));

    [Fact]
    public void FormatSql_LowercaseKeywordCasing_LowercasesKeywords()
    {
        var options = new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Lowercase,
            CommaPlacement = CommaPlacement.Trailing,
            IndentationSize = 4,
        };

        string result = ConverterService.FormatSql("SELECT a.Id, a.Name FROM TableA a", options);
        Assert.Contains("select", result);
        Assert.Contains("from", result);
        Assert.DoesNotContain("SELECT", result);
    }

    [Fact]
    public void FormatSql_LeadingCommaPlacement_PutsCommaBeforeColumn()
    {
        var options = new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase,
            CommaPlacement = CommaPlacement.Leading,
            IndentationSize = 4,
            MultilineSelectElementsList = true,
        };

        string result = ConverterService.FormatSql("SELECT a.Id, a.Name, a.Value FROM TableA a", options);
        Assert.Contains(",a.Name", result.Replace(" ", string.Empty));
    }

    [Fact]
    public void FormatSql_AlignClauseBodiesTrue_PadsKeywordsToAlignBodies()
    {
        string sql = "SELECT a.Id FROM TableA a WHERE a.Id = 1";
        var aligned = new SqlScriptGeneratorOptions { KeywordCasing = KeywordCasing.Uppercase, AlignClauseBodies = true };
        var unaligned = new SqlScriptGeneratorOptions { KeywordCasing = KeywordCasing.Uppercase, AlignClauseBodies = false };

        string alignedResult = ConverterService.FormatSql(sql, aligned);
        string unalignedResult = ConverterService.FormatSql(sql, unaligned);

        Assert.Contains("FROM   TableA", alignedResult);
        Assert.Contains("FROM TableA", unalignedResult);
        Assert.DoesNotContain("FROM   TableA", unalignedResult);
    }

    [Fact]
    public void UnixTimestampToDateTime_ConvertsEpochZeroToUtcStart()
    {
        string result = ConverterService.UnixTimestampToDateTime("0");
        string expected = DateTimeOffset.FromUnixTimeSeconds(0).ToLocalTime().DateTime.ToString(ConverterService.TimestampDateFormat);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TimestampAndDateTime_RoundTrip()
    {
        string timestamp = "1700000000";
        string date = ConverterService.UnixTimestampToDateTime(timestamp);
        string roundTripped = ConverterService.DateTimeToUnixTimestamp(date);
        Assert.Equal(timestamp, roundTripped);
    }

    [Fact]
    public void FormatUuid_DefaultFormat_HasHyphensAndLowercase()
    {
        var uuid = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
        Assert.Equal("12345678-1234-1234-1234-1234567890ab", ConverterService.FormatUuid(uuid, noHyphens: false, uppercase: false));
    }

    [Fact]
    public void FormatUuid_NoHyphensUppercase()
    {
        var uuid = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
        Assert.Equal("123456781234123412341234567890AB", ConverterService.FormatUuid(uuid, noHyphens: true, uppercase: true));
    }

    [Fact]
    public void UnixTimestampToIso8601Local_ProducesOffsetFormat()
    {
        string iso = ConverterService.UnixTimestampToIso8601Local("0");
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$", iso);
    }

    [Fact]
    public void UnixTimestampToIso8601Utc_ProducesZSuffix()
        => Assert.Equal("1970-01-01T00:00:00Z", ConverterService.UnixTimestampToIso8601Utc("0"));

    [Fact]
    public void UnixTimestampToIso8601Local_RoundTripsThroughDateTimeToUnixTimestamp()
    {
        string timestamp = "1700000000";
        string iso = ConverterService.UnixTimestampToIso8601Local(timestamp);
        Assert.Equal(timestamp, ConverterService.DateTimeToUnixTimestamp(iso));
    }

    [Fact]
    public void UnixTimestampToIso8601Utc_RoundTripsThroughDateTimeToUnixTimestamp()
    {
        string timestamp = "1700000000";
        string iso = ConverterService.UnixTimestampToIso8601Utc(timestamp);
        Assert.Equal(timestamp, ConverterService.DateTimeToUnixTimestamp(iso));
    }

    [Fact]
    public void DateTimeToUnixTimestamp_AcceptsUtcIso8601WithZSuffix()
        => Assert.Equal("0", ConverterService.DateTimeToUnixTimestamp("1970-01-01T00:00:00Z"));

    [Fact]
    public void CreateUuidV5_MatchesKnownVector()
    {
        // Python: uuid.uuid5(uuid.NAMESPACE_DNS, 'python.org') == 886313e1-3b8a-5372-9b90-0c9aee199e5d
        Guid result = ConverterService.CreateUuidV5(ConverterService.NamespaceDns, "python.org");
        Assert.Equal(Guid.Parse("886313e1-3b8a-5372-9b90-0c9aee199e5d"), result);
    }

    [Fact]
    public void CreateUuidV5_IsDeterministicForSameInput()
    {
        Guid first = ConverterService.CreateUuidV5(ConverterService.NamespaceUrl, "example");
        Guid second = ConverterService.CreateUuidV5(ConverterService.NamespaceUrl, "example");
        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateUuidV7_HasVersion7Marker()
    {
        Guid uuid = ConverterService.CreateUuidV7();
        byte[] bytes = uuid.ToByteArray();
        Assert.Equal(0x70, bytes[7] & 0xF0);
        Assert.Equal(0x80, bytes[8] & 0xC0);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(64)]
    public void GeneratePassword_ProducesRequestedLength(int length)
    {
        string password = ConverterService.GeneratePassword(length, true, true, true, true, false);
        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void GeneratePassword_OnlyDigits_ContainsOnlyDigits()
    {
        string password = ConverterService.GeneratePassword(50, false, false, true, false, false);
        Assert.All(password, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void GeneratePassword_ExcludeAmbiguous_NeverContainsAmbiguousChars()
    {
        string password = ConverterService.GeneratePassword(200, true, true, true, false, true);
        Assert.DoesNotContain(password, c => "0O1lI".Contains(c));
    }

    [Fact]
    public void GeneratePassword_NoCharsetSelected_Throws()
        => Assert.Throws<InvalidOperationException>(() => ConverterService.GeneratePassword(16, false, false, false, false, false));

    [Fact]
    public void GeneratePassword_ZeroLength_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ConverterService.GeneratePassword(0, true, true, true, true, false));

    [Fact]
    public void FindRegexMatches_ReturnsAllNonOverlappingMatches()
    {
        var matches = ConverterService.FindRegexMatches(@"\d+", "a12 b345 c6", RegexOptions.None);
        Assert.Equal(new[] { "12", "345", "6" }, matches.Select(m => m.Value));
    }

    [Fact]
    public void FindRegexMatches_CapturesNamedAndNumberedGroups()
    {
        var matches = ConverterService.FindRegexMatches(@"(?<year>\d{4})-(\d{2})-(\d{2})", "2026-08-31", RegexOptions.None);
        var match = Assert.Single(matches);
        Assert.Equal("2026", match.Groups["year"].Value);
        Assert.Equal("08", match.Groups[1].Value);
        Assert.Equal("31", match.Groups[2].Value);
    }

    [Fact]
    public void FindRegexMatches_IgnoreCaseOption_MatchesRegardlessOfCase()
    {
        var matches = ConverterService.FindRegexMatches("hello", "HELLO world", RegexOptions.None);
        Assert.Empty(matches);

        matches = ConverterService.FindRegexMatches("hello", "HELLO world", RegexOptions.IgnoreCase);
        Assert.Single(matches);
    }

    [Fact]
    public void FindRegexMatches_InvalidPattern_Throws()
        => Assert.ThrowsAny<Exception>(() => ConverterService.FindRegexMatches("[unclosed", "text", RegexOptions.None));

    [Fact]
    public void FindRegexMatches_MoreThanMaxMatches_IsTruncatedToLimit()
    {
        var matches = ConverterService.FindRegexMatches("a", new string('a', ConverterService.MaxRegexMatches + 500), RegexOptions.None);
        Assert.Equal(ConverterService.MaxRegexMatches, matches.Count);
    }

    [Fact]
    public void FindRegexMatches_CatastrophicBacktracking_ThrowsInsteadOfHanging()
        => Assert.ThrowsAny<Exception>(() => ConverterService.FindRegexMatches(@"(a+)+$", new string('a', 40) + "!", RegexOptions.None));

    [Theory]
    [InlineData(ConverterService.RegexSampleEmailPattern, ConverterService.RegexSampleEmailInput, 2)]
    [InlineData(ConverterService.RegexSampleDatePattern, ConverterService.RegexSampleDateInput, 2)]
    [InlineData(ConverterService.RegexSamplePhonePattern, ConverterService.RegexSamplePhoneInput, 2)]
    public void RegexSamples_PatternMatchesSampleInput(string pattern, string input, int expectedMatchCount)
    {
        var matches = ConverterService.FindRegexMatches(pattern, input, RegexOptions.None);
        Assert.Equal(expectedMatchCount, matches.Count);
    }

    [Fact]
    public void ExtractRegexPattern_PlainPattern_ReturnsAsIs()
        => Assert.Equal(@"\d+", ConverterService.ExtractRegexPattern(@"\d+"));

    [Fact]
    public void ExtractRegexPattern_SlashDelimited_StripsSurroundingSlashes()
        => Assert.Equal(@"\d+", ConverterService.ExtractRegexPattern(@"/\d+/"));

    [Fact]
    public void ExtractRegexPattern_OnlyOpeningSlash_ReturnsRawText()
        => Assert.Equal(@"/\d+", ConverterService.ExtractRegexPattern(@"/\d+"));

    [Fact]
    public void ExtractRegexPattern_EmptySlashes_ReturnsEmptyPattern()
        => Assert.Equal(string.Empty, ConverterService.ExtractRegexPattern("//"));

    [Fact]
    public void ExtractRegexPattern_EmptyInput_ReturnsEmptyPattern()
        => Assert.Equal(string.Empty, ConverterService.ExtractRegexPattern(string.Empty));

    [Fact]
    public void ExtractRegexPattern_SingleSlash_ReturnsAsIs()
        => Assert.Equal("/", ConverterService.ExtractRegexPattern("/"));

    // jwt.io の既定サンプルトークン: header={"alg":"HS256","typ":"JWT"}, payload={"sub":"1234567890","name":"John Doe","iat":1516239022}
    private const string SampleJwt =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    [Fact]
    public void DecodeJwt_ValidToken_ReturnsFormattedHeaderPayloadAndSignature()
    {
        var (headerJson, payloadJson, signature) = ConverterService.DecodeJwt(SampleJwt);
        Assert.Contains("HS256", headerJson);
        Assert.Contains("JWT", headerJson);
        Assert.Contains("1234567890", payloadJson);
        Assert.Contains("John Doe", payloadJson);
        Assert.Equal("SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", signature);
    }

    [Theory]
    [InlineData("only.two")]
    [InlineData("a..c")]
    [InlineData("")]
    public void DecodeJwt_MalformedStructure_Throws(string token)
        => Assert.ThrowsAny<Exception>(() => ConverterService.DecodeJwt(token));

    [Fact]
    public void DecodeJwt_InvalidBase64InHeader_Throws()
        => Assert.ThrowsAny<Exception>(() => ConverterService.DecodeJwt("not-valid-base64!!.eyJhIjoxfQ.sig"));

    [Fact]
    public void JwtSampleTokenBasic_DecodesToExpectedClaims()
    {
        var (headerJson, payloadJson, _) = ConverterService.DecodeJwt(ConverterService.JwtSampleTokenBasic);
        Assert.Contains("HS256", headerJson);
        Assert.Contains("John Doe", payloadJson);
    }

    [Fact]
    public void JwtSampleTokenExpired_DecodesWithExpiredExpClaim()
    {
        var (_, payloadJson, _) = ConverterService.DecodeJwt(ConverterService.JwtSampleTokenExpired);
        var claim = Assert.Single(ConverterService.ExtractJwtTimestampClaims(payloadJson), c => c.ClaimName == "exp");
        Assert.True(DateTimeOffset.FromUnixTimeSeconds(claim.UnixSeconds) < DateTimeOffset.UtcNow);
    }

    [Fact]
    public void JwtSampleTokenWithRoles_DecodesWithRolesArrayClaim()
    {
        var (_, payloadJson, _) = ConverterService.DecodeJwt(ConverterService.JwtSampleTokenWithRoles);
        Assert.Contains("\"roles\"", payloadJson);
        Assert.Contains("admin", payloadJson);
    }

    [Fact]
    public void ExtractJwtTimestampClaims_FindsIatClaim()
    {
        var (_, payloadJson, _) = ConverterService.DecodeJwt(SampleJwt);
        var claims = ConverterService.ExtractJwtTimestampClaims(payloadJson);
        var iat = Assert.Single(claims);
        Assert.Equal("iat", iat.ClaimName);
        Assert.Equal(1516239022L, iat.UnixSeconds);
    }

    [Fact]
    public void ExtractJwtTimestampClaims_NoTimestampClaims_ReturnsEmpty()
        => Assert.Empty(ConverterService.ExtractJwtTimestampClaims("{\"sub\":\"abc\"}"));

    [Theory]
    [InlineData("my_variable_name", "myVariableName")]
    [InlineData("my-variable-name", "myVariableName")]
    [InlineData("MyVariableName", "myVariableName")]
    [InlineData("MY_CONSTANT_VALUE", "myConstantValue")]
    [InlineData("Hello World", "helloWorld")]
    [InlineData("", "")]
    public void ToCamelCase_ConvertsFromAnyNotation(string input, string expected)
        => Assert.Equal(expected, ConverterService.ToCamelCase(input));

    [Theory]
    [InlineData("my_variable_name", "MyVariableName")]
    [InlineData("myVariableName", "MyVariableName")]
    [InlineData("kebab-case-value", "KebabCaseValue")]
    [InlineData("name", "Name")]
    public void ToPascalCase_ConvertsFromAnyNotation(string input, string expected)
        => Assert.Equal(expected, ConverterService.ToPascalCase(input));

    [Theory]
    [InlineData("myVariableName", "my_variable_name")]
    [InlineData("MyVariableName", "my_variable_name")]
    [InlineData("kebab-case-value", "kebab_case_value")]
    [InlineData("HTTPServerName", "http_server_name")]
    public void ToSnakeCase_ConvertsFromAnyNotation(string input, string expected)
        => Assert.Equal(expected, ConverterService.ToSnakeCase(input));

    [Theory]
    [InlineData("myVariableName", "my-variable-name")]
    [InlineData("MY_CONSTANT_VALUE", "my-constant-value")]
    public void ToKebabCase_ConvertsFromAnyNotation(string input, string expected)
        => Assert.Equal(expected, ConverterService.ToKebabCase(input));

    [Theory]
    [InlineData("myVariableName", "MY_VARIABLE_NAME")]
    [InlineData("kebab-case-value", "KEBAB_CASE_VALUE")]
    public void ToConstantCase_ConvertsFromAnyNotation(string input, string expected)
        => Assert.Equal(expected, ConverterService.ToConstantCase(input));

    [Theory]
    [InlineData("255", 10, 255UL)]
    [InlineData("FF", 16, 255UL)]
    [InlineData("0xFF", 16, 255UL)]
    [InlineData("ff", 16, 255UL)]
    [InlineData("1010", 2, 10UL)]
    [InlineData("0b1010", 2, 10UL)]
    [InlineData("17", 8, 15UL)]
    [InlineData("0o17", 8, 15UL)]
    [InlineData("0O17", 8, 15UL)]
    [InlineData("0B1010", 2, 10UL)]
    [InlineData("1_000", 10, 1000UL)]
    [InlineData("0xFF_FF", 16, 65535UL)]
    public void TryParseNumberBase_ValidInput_ReturnsExpectedValue(string text, int fromBase, ulong expected)
    {
        bool success = ConverterService.TryParseNumberBase(text, fromBase, out ulong value);
        Assert.True(success);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("GG", 16)]
    [InlineData("2", 2)]
    [InlineData("9", 8)]
    [InlineData("0x", 16)]
    public void TryParseNumberBase_InvalidInput_ReturnsFalse(string text, int fromBase)
        => Assert.False(ConverterService.TryParseNumberBase(text, fromBase, out _));

    [Theory]
    [InlineData(255UL, 10, "255")]
    [InlineData(255UL, 16, "FF")]
    [InlineData(10UL, 2, "1010")]
    [InlineData(15UL, 8, "17")]
    [InlineData(0UL, 2, "0")]
    public void FormatNumberBase_ProducesExpectedRepresentation(ulong value, int toBase, string expected)
        => Assert.Equal(expected, ConverterService.FormatNumberBase(value, toBase));

    [Fact]
    public void FormatNumberBase_MaxValue_ProducesFullWidthHexAndDecimal()
    {
        Assert.Equal("FFFFFFFFFFFFFFFF", ConverterService.FormatNumberBase(ulong.MaxValue, 16));
        Assert.Equal("18446744073709551615", ConverterService.FormatNumberBase(ulong.MaxValue, 10));
    }

    [Theory]
    [InlineData(255UL, 16, "0xFF")]
    [InlineData(15UL, 8, "0o17")]
    [InlineData(10UL, 2, "0B1010")]
    [InlineData(255UL, 10, "255")]
    public void FormatNumberBase_WithPrefix_AddsExpectedPrefix(ulong value, int toBase, string expected)
        => Assert.Equal(expected, ConverterService.FormatNumberBase(value, toBase, includePrefix: true));

    [Fact]
    public void FormatNumberBase_WithoutPrefix_OmitsPrefixByDefault()
    {
        Assert.Equal("FF", ConverterService.FormatNumberBase(255UL, 16));
        Assert.Equal("17", ConverterService.FormatNumberBase(15UL, 8));
        Assert.Equal("1010", ConverterService.FormatNumberBase(10UL, 2));
    }

    [Theory]
    [InlineData("0xFF", 16, 255UL)]
    [InlineData("0o17", 8, 15UL)]
    [InlineData("0B1010", 2, 10UL)]
    public void TryParseNumberBase_PrefixedOutputRoundTrips(string prefixed, int fromBase, ulong expected)
    {
        bool success = ConverterService.TryParseNumberBase(prefixed, fromBase, out ulong value);
        Assert.True(success);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(16)]
    public void NumberBase_RoundTripsThroughAllBases(int toBase)
    {
        ConverterService.TryParseNumberBase("12345", 10, out ulong original);
        string formatted = ConverterService.FormatNumberBase(original, toBase);
        ConverterService.TryParseNumberBase(formatted, toBase, out ulong roundTripped);
        Assert.Equal(original, roundTripped);
    }

    [Theory]
    [InlineData("https://example.com/hasamiboard", QRCoder.QRCodeGenerator.ECCLevel.M)]
    [InlineData("日本語のテキストもエンコードできる", QRCoder.QRCodeGenerator.ECCLevel.H)]
    public void QrCode_GenerateThenDecode_RoundTrips(string text, QRCoder.QRCodeGenerator.ECCLevel eccLevel)
    {
        byte[] pngBytes = ConverterService.GenerateQrCodePng(text, eccLevel, pixelsPerModule: 8);
        var image = ConverterService.PngBytesToBitmapImage(pngBytes);

        string? decoded = ConverterService.DecodeQrCode(image);

        Assert.Equal(text, decoded);
    }

    [Fact]
    public void DecodeQrCode_NoQrCodeInImage_ReturnsNull()
    {
        var blank = new System.Windows.Media.Imaging.WriteableBitmap(64, 64, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        Assert.Null(ConverterService.DecodeQrCode(blank));
    }

    [Fact]
    public void GetNextCronOccurrences_EveryFifteenMinutes_ReturnsEvenlySpacedOccurrences()
    {
        string fromText = new DateTime(2026, 1, 1, 0, 0, 0).ToString(ConverterService.TimestampDateFormat);

        var results = ConverterService.GetNextCronOccurrences("*/15 * * * *", CronFormat.Standard, fromText, 3);

        var expected = new[]
        {
            new DateTime(2026, 1, 1, 0, 15, 0),
            new DateTime(2026, 1, 1, 0, 30, 0),
            new DateTime(2026, 1, 1, 0, 45, 0),
        }.Select(d => d.ToString(ConverterService.TimestampDateFormat));

        Assert.Equal(expected, results);
    }

    [Fact]
    public void GetNextCronOccurrences_WeekdayOnlyExpression_SkipsWeekend()
    {
        var saturday = new DateTime(2026, 1, 3);
        Assert.Equal(DayOfWeek.Saturday, saturday.DayOfWeek);
        string fromText = saturday.ToString(ConverterService.TimestampDateFormat);

        var results = ConverterService.GetNextCronOccurrences("0 9 * * 1-5", CronFormat.Standard, fromText, 1);

        var expectedMonday = new DateTime(2026, 1, 5, 9, 0, 0);
        Assert.Equal(new[] { expectedMonday.ToString(ConverterService.TimestampDateFormat) }, results);
    }

    [Fact]
    public void GetNextCronOccurrences_IncludeSecondsFormat_ParsesSixFields()
    {
        string fromText = new DateTime(2026, 1, 1, 9, 0, 0).ToString(ConverterService.TimestampDateFormat);

        var results = ConverterService.GetNextCronOccurrences("30 0 9 * * *", CronFormat.IncludeSeconds, fromText, 1);

        var expected = new DateTime(2026, 1, 1, 9, 0, 30);
        Assert.Equal(new[] { expected.ToString(ConverterService.TimestampDateFormat) }, results);
    }

    [Fact]
    public void GetNextCronOccurrences_InvalidExpression_Throws()
        => Assert.Throws<CronFormatException>(
            () => ConverterService.GetNextCronOccurrences("not a cron", CronFormat.Standard, string.Empty, 1));

    [Fact]
    public void GetNextCronOccurrences_SixFieldExpressionParsedAsStandard_Throws()
        => Assert.Throws<CronFormatException>(
            () => ConverterService.GetNextCronOccurrences("30 0 9 * * *", CronFormat.Standard, string.Empty, 1));

    [Fact]
    public void DescribeCron_ValidExpression_ReturnsNonEmptyDescription()
        => Assert.False(string.IsNullOrWhiteSpace(ConverterService.DescribeCron("0 9 * * 1-5")));

    [Fact]
    public void DescribeCron_InvalidExpression_ReturnsEmptyStringInsteadOfThrowing()
        => Assert.Equal(string.Empty, ConverterService.DescribeCron("not a cron"));

    [Fact]
    public void TryBuildCronDayOfWeekField_NoDaysSelected_ReturnsFalse()
    {
        bool success = ConverterService.TryBuildCronDayOfWeekField(Array.Empty<int>(), out string field);

        Assert.False(success);
        Assert.Equal(string.Empty, field);
    }

    [Fact]
    public void TryBuildCronDayOfWeekField_SomeDaysSelected_ReturnsSortedCommaList()
    {
        bool success = ConverterService.TryBuildCronDayOfWeekField(new[] { 5, 1, 3 }, out string field);

        Assert.True(success);
        Assert.Equal("1,3,5", field);
    }

    [Fact]
    public void TryBuildCronDayOfWeekField_AllSevenDaysSelected_ReturnsWildcard()
    {
        bool success = ConverterService.TryBuildCronDayOfWeekField(new[] { 0, 1, 2, 3, 4, 5, 6 }, out string field);

        Assert.True(success);
        Assert.Equal("*", field);
    }

    [Fact]
    public void GetNextCronOccurrences_WeeklyBuilderField_ProducesExpectedSchedule()
    {
        ConverterService.TryBuildCronDayOfWeekField(new[] { 1, 3, 5 }, out string dayField);
        string expression = $"0 9 * * {dayField}";

        var monday = new DateTime(2026, 1, 5);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        string fromText = monday.ToString(ConverterService.TimestampDateFormat);

        var results = ConverterService.GetNextCronOccurrences(expression, CronFormat.Standard, fromText, 3);

        var expected = new[]
        {
            new DateTime(2026, 1, 5, 9, 0, 0),
            new DateTime(2026, 1, 7, 9, 0, 0),
            new DateTime(2026, 1, 9, 9, 0, 0),
        }.Select(d => d.ToString(ConverterService.TimestampDateFormat));

        Assert.Equal(expected, results);
    }

    [Theory]
    [InlineData("alice", "password123")]
    [InlineData("", "")]
    [InlineData("ユーザー", "パスワード")]
    public void BuildBasicAuthHeader_ReturnsExpectedBase64Header(string username, string password)
    {
        string expectedBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        Assert.Equal($"Authorization: Basic {expectedBase64}", ConverterService.BuildBasicAuthHeader(username, password));
    }

    [Theory]
    [InlineData("alice", "password123", "http://alice:password123@example.com")]
    [InlineData("", "", "http://:@example.com")]
    [InlineData("user@corp", "p@ss:w/ord", "http://user%40corp:p%40ss%3Aw%2Ford@example.com")]
    public void BuildBasicAuthUserInfoUrl_ReturnsPercentEncodedUserInfoUrl(string username, string password, string expected)
    {
        Assert.Equal(expected, ConverterService.BuildBasicAuthUserInfoUrl(username, password));
    }

    [Fact]
    public void EvaluateJq_AppliesFieldAccessFilter()
    {
        var results = ConverterService.EvaluateJq("""{"name":"Alice","age":30}""", ".name", rawOutput: false, compactOutput: false, slurpInput: false, nullInput: false);

        Assert.Equal(new[] { "\"Alice\"" }, results);
    }

    [Fact]
    public void EvaluateJq_DoesNotEscapeNonAsciiCharacters()
    {
        var results = ConverterService.EvaluateJq("""{"name":"こんにちは"}""", ".name", rawOutput: false, compactOutput: true, slurpInput: false, nullInput: false);

        Assert.Equal(new[] { "\"こんにちは\"" }, results);
    }

    [Fact]
    public void EvaluateJq_RawOutput_ReturnsUnquotedString()
    {
        var results = ConverterService.EvaluateJq("""{"name":"Alice"}""", ".name", rawOutput: true, compactOutput: false, slurpInput: false, nullInput: false);

        Assert.Equal(new[] { "Alice" }, results);
    }

    [Fact]
    public void EvaluateJq_CompactOutput_ReturnsSingleLineJson()
    {
        string json = """{"a":1,"b":2}""";

        var compact = ConverterService.EvaluateJq(json, ".", rawOutput: false, compactOutput: true, slurpInput: false, nullInput: false);
        var pretty = ConverterService.EvaluateJq(json, ".", rawOutput: false, compactOutput: false, slurpInput: false, nullInput: false);

        Assert.Equal(new[] { """{"a":1,"b":2}""" }, compact);
        Assert.NotEqual(compact.Single(), pretty.Single());
        Assert.Contains('\n', pretty.Single());
    }

    [Fact]
    public void EvaluateJq_NullInput_IgnoresProvidedJson()
    {
        var results = ConverterService.EvaluateJq("not valid json", "1 + 1", rawOutput: false, compactOutput: false, slurpInput: false, nullInput: true);

        Assert.Equal(new[] { "2" }, results);
    }

    [Fact]
    public void EvaluateJq_SlurpInput_CombinesMultipleJsonDocuments()
    {
        var results = ConverterService.EvaluateJq("1\n2\n3", ".", rawOutput: false, compactOutput: true, slurpInput: true, nullInput: false);

        Assert.Equal(new[] { "[1,2,3]" }, results);
    }

    [Fact]
    public void EvaluateJq_InvalidFilter_ThrowsJqException()
    {
        Assert.Throws<JqException>(() =>
            ConverterService.EvaluateJq("{}", "this is not a valid jq filter (((", rawOutput: false, compactOutput: false, slurpInput: false, nullInput: false));
    }

    [Fact]
    public void BuildJqPathTree_ReturnsExpectedPathsForNestedObjectAndArray()
    {
        var nodes = ConverterService.BuildJqPathTree("""{"user":{"name":"Alice"},"items":[10,20]}""");

        var user = Assert.Single(nodes, n => n.JqPath == ".user");
        var name = Assert.Single(user.Children, n => n.JqPath == ".user.name");
        Assert.Equal("name: \"Alice\"", name.Label);

        var items = Assert.Single(nodes, n => n.JqPath == ".items");
        Assert.Equal(2, items.Children.Count);
        Assert.Equal(".items[0]", items.Children[0].JqPath);
        Assert.Equal(".items[1]", items.Children[1].JqPath);
    }

    [Fact]
    public void BuildJqPathTree_EscapesNonIdentifierKeysWithBracketSyntax()
    {
        var nodes = ConverterService.BuildJqPathTree("""{"user-name":"Alice"}""");

        var node = Assert.Single(nodes);
        Assert.Equal(".[\"user-name\"]", node.JqPath);
    }

    [Fact]
    public void BuildHtpasswdLineBcrypt_ProducesVerifiableHash()
    {
        string line = ConverterService.BuildHtpasswdLineBcrypt("alice", "s3cret!", 4);

        int separatorIndex = line.IndexOf(':');
        string username = line[..separatorIndex];
        string hash = line[(separatorIndex + 1)..];

        Assert.Equal("alice", username);
        Assert.StartsWith("$2a$04$", hash);
        Assert.True(BCrypt.Net.BCrypt.Verify("s3cret!", hash));
        Assert.False(BCrypt.Net.BCrypt.Verify("wrong", hash));
    }

    [Theory]
    [InlineData("testpass123", "abcdefgh", "$apr1$abcdefgh$S09C0VI9zqJQqiRkdro8J1")]
    [InlineData("", "aaaaaaaa", "$apr1$aaaaaaaa$tc1onWtdqiymTE.0yS4Pp0")]
    [InlineData("password", "12345678", "$apr1$12345678$9pHAGSBYtlmFtid2xxNog0")]
    public void BuildHtpasswdLineApr1_MatchesOpenSslReferenceOutput(string password, string salt, string expectedHash)
    {
        // 期待値は `openssl passwd -apr1 -salt <salt> <password>` の出力と一致することを確認済み
        string line = ConverterService.BuildHtpasswdLineApr1("bob", password, salt);
        Assert.Equal($"bob:{expectedHash}", line);
    }

    [Fact]
    public void BuildHtpasswdLineSha1_ProducesShaPrefixedBase64Digest()
    {
        string line = ConverterService.BuildHtpasswdLineSha1("carol", "hunter2");
        string expectedDigest = Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes("hunter2")));

        Assert.Equal($"carol:{{SHA}}{expectedDigest}", line);
    }
}
