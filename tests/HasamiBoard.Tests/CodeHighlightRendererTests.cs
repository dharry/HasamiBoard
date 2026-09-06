using ColorCode;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class CodeHighlightRendererTests
{
    [Fact]
    public void RenderHtmlDocument_HighlightsSqlKeywords()
    {
        var renderer = new CodeHighlightRenderer();
        string html = renderer.RenderHtmlDocument("SELECT * FROM Foo WHERE Bar = 1;", Languages.Sql, false, "Consolas", 12);

        Assert.Contains("<span style=", html);
        Assert.Contains("SELECT", html);
        Assert.Contains("Foo", html);
    }

    [Fact]
    public void RenderHtmlDocument_HighlightsJson()
    {
        var renderer = new CodeHighlightRenderer();
        string html = renderer.RenderHtmlDocument("{\"key\":\"value\"}", Languages.FindById("json"), false, "Consolas", 12);

        Assert.Contains("<span style=", html);
        Assert.Contains("key", html);
    }

    [Fact]
    public void RenderHtmlDocument_SetsBootstrapThemeAttribute()
    {
        var renderer = new CodeHighlightRenderer();

        string lightHtml = renderer.RenderHtmlDocument("a{}", Languages.Css, false, "Consolas", 12);
        string darkHtml = renderer.RenderHtmlDocument("a{}", Languages.Css, true, "Consolas", 12);

        Assert.Contains("data-bs-theme=\"light\"", lightHtml);
        Assert.Contains("data-bs-theme=\"dark\"", darkHtml);
    }

    [Fact]
    public void RenderHtmlDocument_ColorsDifferBetweenLightAndDarkMode()
    {
        var renderer = new CodeHighlightRenderer();
        const string code = "SELECT * FROM Foo;";

        string lightHtml = renderer.RenderHtmlDocument(code, Languages.Sql, false, "Consolas", 12);
        string darkHtml = renderer.RenderHtmlDocument(code, Languages.Sql, true, "Consolas", 12);

        Assert.NotEqual(lightHtml, darkHtml);
    }

    [Fact]
    public void RenderHtmlDocument_EmptyCode_DoesNotThrow()
    {
        var renderer = new CodeHighlightRenderer();
        string html = renderer.RenderHtmlDocument(string.Empty, Languages.Xml, false, "Consolas", 12);

        Assert.Contains("<html", html);
    }

    [Fact]
    public void RenderHtmlDocument_EmbedsBootstrapCssInline_NoExternalRequests()
    {
        var renderer = new CodeHighlightRenderer();
        string html = renderer.RenderHtmlDocument("body{}", Languages.Css, false, "Consolas", 12);

        Assert.Contains("Bootstrap", html);
        Assert.DoesNotContain("<link", html);
        Assert.DoesNotContain("cdn.", html, StringComparison.OrdinalIgnoreCase);
    }
}
