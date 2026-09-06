using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ColorCode.Styling;
using Markdig;
using Markdown.ColorCode;

namespace HasamiBoard.Services;

/// <summary>
/// Markdig + Markdown.ColorCode で Markdown を HTML へ変換し、完全ローカル同梱の
/// Bootstrap スタイル (CDN通信なし) でラップしたプレビュー用HTMLドキュメントを生成する。
/// </summary>
public class MarkdownRenderer
{
    private static readonly string BootstrapCss = EmbeddedResourceLoader.LoadText("bootstrap.min.css");

    // NavigateToString で表示するプレビューはネットワークアクセスやファイルパス解決の
    // 制約があるため、ローカルファイルを指す <img src="..."> は Base64 データURIへ
    // 埋め込み変換してから描画する。
    private static readonly Regex ImgSrcRegex = new("<img[^>]*\\ssrc=\"([^\"]+)\"", RegexOptions.Compiled);

    // ライト/ダークそれぞれの背景色に対して視認できる配色のシンタックスハイライトを使う。
    // (既定の StyleDictionary.DefaultDark を常用すると、ライトモードでは淡い色が
    //  白背景に埋もれてハイライトされていないように見えてしまう)
    private static readonly MarkdownPipeline LightPipeline = BuildPipeline(StyleDictionary.DefaultLight);
    private static readonly MarkdownPipeline DarkPipeline = BuildPipeline(StyleDictionary.DefaultDark);

    /// <summary>指定シンタックスハイライト配色でMarkdig変換パイプラインを構築する。</summary>
    private static MarkdownPipeline BuildPipeline(StyleDictionary styleDictionary) => new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseColorCode(HtmlFormatterType.Style, styleDictionary)
        .Build();

    /// <summary>Markdown文字列をプレビュー表示用のHTMLドキュメント文字列へ変換する。</summary>
    /// <param name="markdown">変換対象のMarkdown文字列</param>
    /// <param name="isDarkMode">ダークモード利用フラグ</param>
    /// <param name="bodyFontFamily">本文フォント名</param>
    /// <param name="bodyFontSize">本文フォントサイズ</param>
    /// <param name="codeFontFamily">コードフォント名</param>
    /// <param name="codeFontSize">コードフォントサイズ</param>
    /// <param name="isPreview">プレビュー表示モード</param>
    /// <returns>生成されたHTMLドキュメント</returns>
    public string RenderHtmlDocument(string markdown, bool isDarkMode, string bodyFontFamily, double bodyFontSize, string codeFontFamily, double codeFontSize, bool isPreview = false)
    {
        var pipeline = isDarkMode ? DarkPipeline : LightPipeline;
        string bodyHtml = Markdig.Markdown.ToHtml(markdown ?? string.Empty, pipeline);
        bodyHtml = EmbedLocalImages(bodyHtml);
        string theme = isDarkMode ? "dark" : "light";

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"ja\" data-bs-theme=\"").Append(theme).Append("\"><head><meta charset=\"utf-8\" />");
        sb.Append("<style>").Append(BootstrapCss).Append("</style>");
        sb.Append("<style>");
        // カード一覧のプレビュー表示 (isPreview) では、内部スクロールバーを出さず
        // カードの高さで内容を切り詰めて表示する。
        sb.Append("html,body{height:100%;").Append(isPreview ? "overflow:hidden;" : string.Empty).Append("} body{padding:16px 20px;font-family:").Append(EscapeCss(bodyFontFamily)).Append(";font-size:").Append(bodyFontSize.ToString("0.##")).Append("px;} ");
        sb.Append("pre,code{font-family:").Append(EscapeCss(codeFontFamily)).Append(";font-size:").Append(codeFontSize.ToString("0.##")).Append("px;} ");
        // コードタグの背景色。Bootstrapのテーマ対応変数 (--bs-secondary-bg) を使うことで
        // ライトモードは薄いグレー、ダークモードは少し濃いグレーに自動で切り替わる。
        sb.Append("pre{padding:12px;border-radius:6px;overflow-x:auto;background-color:var(--bs-secondary-bg);} ");
        sb.Append("code{background-color:var(--bs-secondary-bg);padding:2px 4px;border-radius:4px;} ");
        sb.Append("pre code{background-color:transparent;padding:0;border-radius:0;} ");
        sb.Append("table{width:auto;} img{max-width:100%;} ");
        sb.Append("blockquote{border-left:4px solid var(--bs-secondary-border-subtle);padding-left:12px;color:var(--bs-secondary-color);} ");
        sb.Append("</style></head><body>");
        sb.Append(bodyHtml);
        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>HTML内のローカル画像参照をBase64データURIへ埋め込み変換する。</summary>
    private static string EmbedLocalImages(string html)
    {
        return ImgSrcRegex.Replace(html, match =>
        {
            string src = match.Groups[1].Value;
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            string localPath = src.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(localPath))
            {
                return match.Value;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(localPath);
                string mimeType = Path.GetExtension(localPath).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    _ => "image/png",
                };
                string dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
                return match.Value.Replace($"src=\"{src}\"", $"src=\"{dataUri}\"");
            }
            catch (IOException)
            {
                return match.Value;
            }
        });
    }

    /// <summary>CSSのfont-family値として安全な文字列へ変換する。</summary>
    private static string EscapeCss(string fontFamily)
        => string.IsNullOrWhiteSpace(fontFamily) ? "sans-serif" : fontFamily.Replace("\"", "'");
}
