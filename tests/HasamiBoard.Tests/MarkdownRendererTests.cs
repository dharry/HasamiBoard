using System.IO;
using System.Text.RegularExpressions;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void RenderHtmlDocument_ConvertsHeadingsAndEmphasis()
    {
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument("# Title\n\nSome **bold** text.", false, "Segoe UI", 14, "Consolas", 13);

        Assert.Contains("<h1", html);
        Assert.Contains("Title", html);
        Assert.Contains("<strong>bold</strong>", html);
    }

    [Fact]
    public void RenderHtmlDocument_SyntaxHighlightsFencedCodeBlocks()
    {
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument("```csharp\npublic class Foo { }\n```", false, "Segoe UI", 14, "Consolas", 13);

        // Markdown.ColorCode はトークンごとに <span style="color:..."> を出力する
        Assert.Contains("<span style=", html);
        Assert.Contains("Foo", html);
    }

    [Fact]
    public void RenderHtmlDocument_EmbedsBootstrapCssInline_NoExternalRequests()
    {
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument("test", false, "Segoe UI", 14, "Consolas", 13);

        // "http://www.w3.org/2000/svg" 等はBootstrap内蔵SVGのXML名前空間URIであり、
        // 実際のネットワークアクセスではないため許容する。CDN読み込みの兆候のみを検出する。
        Assert.Contains("Bootstrap", html);
        Assert.DoesNotContain("<link", html);
        Assert.DoesNotContain("cdn.", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"http", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, "dark")]
    [InlineData(false, "light")]
    public void RenderHtmlDocument_SetsBootstrapThemeAttribute(bool isDarkMode, string expectedTheme)
    {
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument("test", isDarkMode, "Segoe UI", 14, "Consolas", 13);

        Assert.Contains($"data-bs-theme=\"{expectedTheme}\"", html);
    }

    [Fact]
    public void RenderHtmlDocument_SyntaxHighlightColors_DifferBetweenLightAndDarkMode()
    {
        // StyleDictionary.DefaultDark を常用すると、ライトモードでも淡い色が使われ
        // 白背景上でハイライトされていないように見える不具合があった。
        // ライト/ダークで異なる配色 (StyleDictionary) が使われることを確認する。
        const string markdown = "```csharp\npublic class Foo { public void Bar() {} }\n```";
        var renderer = new MarkdownRenderer();

        string lightHtml = renderer.RenderHtmlDocument(markdown, false, "Segoe UI", 14, "Consolas", 13);
        string darkHtml = renderer.RenderHtmlDocument(markdown, true, "Segoe UI", 14, "Consolas", 13);

        var lightColors = ExtractSpanColors(lightHtml);
        var darkColors = ExtractSpanColors(darkHtml);

        Assert.NotEmpty(lightColors);
        Assert.NotEmpty(darkColors);
        Assert.NotEqual(lightColors, darkColors);
    }

    private static List<string> ExtractSpanColors(string html)
        => Regex.Matches(html, "style=\"color:\\s*(#[0-9A-Fa-f]{3,8})", RegexOptions.None)
            .Select(m => m.Groups[1].Value)
            .ToList();

    [Fact]
    public void RenderHtmlDocument_EmbedsLocalImageAsBase64DataUri()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"markdown-image-test-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tempFile, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        try
        {
            var renderer = new MarkdownRenderer();
            string markdown = $"![screenshot]({tempFile.Replace('\\', '/')})";

            string html = renderer.RenderHtmlDocument(markdown, false, "Segoe UI", 14, "Consolas", 13);

            Assert.Contains("data:image/png;base64,", html);
            Assert.DoesNotContain(tempFile.Replace('\\', '/'), html);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RenderHtmlDocument_MissingLocalImage_LeavesSrcUnchanged()
    {
        var renderer = new MarkdownRenderer();
        string markdown = "![screenshot](C:/does/not/exist.png)";

        string html = renderer.RenderHtmlDocument(markdown, false, "Segoe UI", 14, "Consolas", 13);

        Assert.Contains("src=\"C:/does/not/exist.png\"", html);
    }

    [Fact]
    public void RenderHtmlDocument_AppliesThemeAwareBackgroundToCodeTags()
    {
        // コードタグに背景色が無いと、コード部分だけ色がついていても本文と見分けにくい。
        // Bootstrapのテーマ対応変数 (--bs-secondary-bg) を使い、ライト/ダークで
        // 自動的に薄いグレー/少し濃いグレーへ切り替わるようにしている。
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument("```csharp\nvar x = 1;\n```\n\nInline `code` here.", false, "Segoe UI", 14, "Consolas", 13);

        Assert.Contains("pre{", html);
        Assert.Contains("background-color:var(--bs-secondary-bg)", html);
        // pre内のcodeタグは二重に背景がつかないよう透明にする
        Assert.Contains("pre code{background-color:transparent", html);
    }

    [Fact]
    public void RenderHtmlDocument_PreviewMode_HidesOverflowOnHtmlBody()
    {
        // メモ一覧カードのプレビューでは、スクロールバーを出さずカードの高さで
        // 内容を切り詰めたいため isPreview=true で html,body に overflow:hidden を付与する。
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument("test", false, "Segoe UI", 14, "Consolas", 13, isPreview: true);

        Assert.Contains("html,body{height:100%;overflow:hidden;}", html);
    }

    [Fact]
    public void RenderHtmlDocument_NonPreviewMode_DoesNotHideOverflowOnHtmlBody()
    {
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument("test", false, "Segoe UI", 14, "Consolas", 13);

        Assert.Contains("html,body{height:100%;}", html);
        Assert.DoesNotContain("html,body{height:100%;overflow:hidden;}", html);
    }

    [Fact]
    public void RenderHtmlDocument_EmptyMarkdown_DoesNotThrow()
    {
        var renderer = new MarkdownRenderer();
        string html = renderer.RenderHtmlDocument(string.Empty, false, "Segoe UI", 14, "Consolas", 13);

        Assert.Contains("<html", html);
    }
}
