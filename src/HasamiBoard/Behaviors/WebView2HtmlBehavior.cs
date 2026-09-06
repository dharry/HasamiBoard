using System.IO;
using System.Windows;
using HasamiBoard.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HasamiBoard.Behaviors;

/// <summary>
/// WebView2 に対して完成済みのHTML文字列を直接バインドできるようにする添付プロパティ。
/// メモ一覧の仮想化された ItemsControl 内で再利用される WebView2 (Markdownプレビュー) を
/// 想定し、複数インスタンス間で CoreWebView2Environment を共有して初期化コストを抑える。
/// </summary>
public static class WebView2HtmlBehavior
{
    public static readonly DependencyProperty HtmlProperty =
        DependencyProperty.RegisterAttached(
            "Html",
            typeof(string),
            typeof(WebView2HtmlBehavior),
            new PropertyMetadata(null, OnHtmlChanged));

    /// <summary>添付プロパティ Html の値を取得する。</summary>
    /// <param name="obj">対象 DependencyObject</param>
    /// <returns>Html プロパティの値</returns>
    public static string? GetHtml(DependencyObject obj) => (string?)obj.GetValue(HtmlProperty);

    /// <summary>添付プロパティ Html の値を設定する。</summary>
    /// <param name="obj">対象 DependencyObject</param>
    /// <param name="value">設定する Html テキスト</param>
    public static void SetHtml(DependencyObject obj, string? value) => obj.SetValue(HtmlProperty, value);

    private static Task<CoreWebView2Environment>? _sharedEnvironmentTask;

    /// <summary>Html プロパティ変更時に WebView2 を初期化しHTMLをナビゲートする。</summary>
    private static async void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WebView2 view)
        {
            return;
        }

        string html = e.NewValue as string ?? string.Empty;

        try
        {
            if (view.CoreWebView2 is null)
            {
                _sharedEnvironmentTask ??= CoreWebView2Environment.CreateAsync(
                    userDataFolder: Path.Combine(AppPaths.DataFolder, "WebView2"));
                var environment = await _sharedEnvironmentTask;
                await view.EnsureCoreWebView2Async(environment);
            }

            // 初期化待ちの間に (仮想化の再利用等で) さらに新しい値へ変更されていた場合、
            // 古い内容でナビゲートしてしまわないようにする。
            if (GetHtml(view) != html)
            {
                return;
            }

            view.CoreWebView2?.NavigateToString(string.IsNullOrEmpty(html) ? "<html><body></body></html>" : html);
        }
        catch
        {
            // WebView2 ランタイム未導入等の環境では、プレビューのみ利用不可として続行する
        }
    }
}
