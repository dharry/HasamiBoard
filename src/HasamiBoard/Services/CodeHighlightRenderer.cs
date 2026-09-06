using System.Text;
using ColorCode;
using ColorCode.Styling;

namespace HasamiBoard.Services;

/// <summary>
/// ColorCode を用いて任意のコード文字列 (JSON/XML/HTML/CSS/JavaScript/SQL 等) を
/// シンタックスハイライトし、Markdownプレビューと同じ完全ローカル同梱のBootstrapスタイル
/// (CDN通信なし) でラップしたHTMLドキュメントを生成する。
/// </summary>
public class CodeHighlightRenderer
{
    private static readonly string BootstrapCss = EmbeddedResourceLoader.LoadText("bootstrap.min.css");

    // ライト/ダークそれぞれの背景色に対して視認できる配色を使う (MarkdownRenderer と同じ理由)。
    private static readonly HtmlFormatter LightFormatter = new(StyleDictionary.DefaultLight);
    private static readonly HtmlFormatter DarkFormatter = new(StyleDictionary.DefaultDark);

    /// <summary>コードをシンタックスハイライトしたHTMLドキュメント文字列を生成する。</summary>
    /// <param name="code">ハイライト対象のコード文字列</param>
    /// <param name="language">言語定義</param>
    /// <param name="isDarkMode">ダークモード利用フラグ</param>
    /// <param name="fontFamily">フォントファミリ名</param>
    /// <param name="fontSize">フォントサイズ（ピクセル）</param>
    /// <returns>生成されたHTMLドキュメント</returns>
    public string RenderHtmlDocument(string code, ILanguage language, bool isDarkMode, string fontFamily, double fontSize)
    {
        var formatter = isDarkMode ? DarkFormatter : LightFormatter;
        string codeHtml = formatter.GetHtmlString(code ?? string.Empty, language);
        string theme = isDarkMode ? "dark" : "light";

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"ja\" data-bs-theme=\"").Append(theme).Append("\"><head><meta charset=\"utf-8\" />");
        sb.Append("<style>").Append(BootstrapCss).Append("</style>");
        sb.Append("<style>");
        sb.Append("html,body{height:100%;margin:0;} ");
        sb.Append("body,pre,code{font-family:").Append(EscapeCss(fontFamily)).Append(";font-size:").Append(fontSize.ToString("0.##")).Append("px;} ");
        sb.Append("body>div{margin:0;min-height:100%;} ");
        sb.Append("pre{margin:0;padding:12px;white-space:pre-wrap;word-break:break-word;} ");
        sb.Append("</style></head><body>");
        sb.Append(codeHtml);
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>CSSのfont-family値として安全な文字列へ変換する。</summary>
    private static string EscapeCss(string fontFamily)
        => string.IsNullOrWhiteSpace(fontFamily) ? "Consolas, monospace" : fontFamily.Replace("\"", "'");
}
