using System.Text.Json;
using HasamiBoard.Models;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class ErrorCodeServiceTests
{
    [Fact]
    public void HttpStatusCodes_Contains404NotFound()
    {
        var entry = Assert.Single(ErrorCodeService.HttpStatusCodes, e => e.Code == "404");
        Assert.Equal("Not Found", entry.DescriptionEn);
        Assert.Null(entry.MacroName);
    }

    [Fact]
    public void ErrnoCodes_ContainsKnownCode_EACCES()
    {
        var entry = Assert.Single(ErrorCodeService.ErrnoCodes, e => e.MacroName == "EACCES");
        Assert.Equal("13", entry.Code);
        Assert.Equal("Permission denied", entry.DescriptionEn);
    }

    [Fact]
    public void WindowsErrorCodes_ContainsKnownCode_ErrorAccessDenied()
    {
        var entry = Assert.Single(ErrorCodeService.WindowsErrorCodes, e => e.MacroName == "ERROR_ACCESS_DENIED");
        Assert.Equal("5", entry.Code);
    }

    [Fact]
    public void SmtpCodes_Contains550MailboxUnavailable()
    {
        var entry = Assert.Single(ErrorCodeService.SmtpCodes, e => e.Code == "550");
        Assert.Contains("mailbox unavailable", entry.DescriptionEn);
    }

    [Theory]
    [InlineData("HttpStatusCodes")]
    [InlineData("ErrnoCodes")]
    [InlineData("WindowsErrorCodes")]
    [InlineData("SmtpCodes")]
    public void AllEntries_HaveNonEmptyCodeAndDescriptions(string propertyName)
    {
        var entries = propertyName switch
        {
            "HttpStatusCodes" => ErrorCodeService.HttpStatusCodes,
            "ErrnoCodes" => ErrorCodeService.ErrnoCodes,
            "WindowsErrorCodes" => ErrorCodeService.WindowsErrorCodes,
            _ => ErrorCodeService.SmtpCodes,
        };

        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Code));
            Assert.False(string.IsNullOrWhiteSpace(e.DescriptionEn));
            Assert.False(string.IsNullOrWhiteSpace(e.DescriptionJa));
        });
    }

    [Fact]
    public void Search_ByCode_ReturnsMatchingEntry()
    {
        var result = ErrorCodeService.Search(ErrorCodeService.HttpStatusCodes, "404");
        Assert.Single(result);
        Assert.Equal("404", result[0].Code);
    }

    [Fact]
    public void Search_ByMacroName_IsCaseInsensitive()
    {
        var result = ErrorCodeService.Search(ErrorCodeService.ErrnoCodes, "eacces");
        Assert.Contains(result, e => e.MacroName == "EACCES");
    }

    [Fact]
    public void Search_ByDescriptionSubstring_MatchesEnglishAndJapanese()
    {
        var enResult = ErrorCodeService.Search(ErrorCodeService.HttpStatusCodes, "Not Found");
        Assert.Contains(enResult, e => e.Code == "404");

        var jaResult = ErrorCodeService.Search(ErrorCodeService.HttpStatusCodes, "見つかりません");
        Assert.Contains(jaResult, e => e.Code == "404");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_EmptyOrWhitespaceQuery_ReturnsAllEntries(string? query)
    {
        var result = ErrorCodeService.Search(ErrorCodeService.SmtpCodes, query);
        Assert.Equal(ErrorCodeService.SmtpCodes.Count, result.Count);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var result = ErrorCodeService.Search(ErrorCodeService.WindowsErrorCodes, "no-such-error-code-xyz");
        Assert.Empty(result);
    }

    [Fact]
    public void ToJson_WithMacro_IncludesCodeMacroAndEnglishDescription()
    {
        var entry = new ErrorCodeEntry("5", "ERROR_ACCESS_DENIED", "Access is denied.", "アクセスが拒否されました。");

        using var doc = JsonDocument.Parse(ErrorCodeService.ToJson(entry, useJapaneseDescription: false));

        Assert.Equal("5", doc.RootElement.GetProperty("Code").GetString());
        Assert.Equal("ERROR_ACCESS_DENIED", doc.RootElement.GetProperty("Macro").GetString());
        Assert.Equal("Access is denied.", doc.RootElement.GetProperty("Description").GetString());
    }

    [Fact]
    public void ToJson_JapaneseSelected_UsesJapaneseDescription()
    {
        var entry = new ErrorCodeEntry("5", "ERROR_ACCESS_DENIED", "Access is denied.", "アクセスが拒否されました。");

        using var doc = JsonDocument.Parse(ErrorCodeService.ToJson(entry, useJapaneseDescription: true));

        Assert.Equal("アクセスが拒否されました。", doc.RootElement.GetProperty("Description").GetString());
    }

    [Fact]
    public void ToJson_NoMacro_MacroIsNull()
    {
        var entry = new ErrorCodeEntry("404", null, "Not Found", "見つかりません");

        using var doc = JsonDocument.Parse(ErrorCodeService.ToJson(entry, useJapaneseDescription: false));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("Macro").ValueKind);
    }
}
