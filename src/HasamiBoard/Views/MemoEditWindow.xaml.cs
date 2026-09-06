using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HasamiBoard.Models;
using HasamiBoard.Services;
using HasamiBoard.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace HasamiBoard.Views;

/// <summary>メモの新規作成・編集を行う子ウィンドウ（テキスト/Markdownモード対応）</summary>
public partial class MemoEditWindow : Window
{
    private const double MinFontSize = 8;
    private const double MaxFontSize = 40;

    private readonly SettingsService _settingsService;
    private readonly ImageStorageService _imageStorageService;
    private readonly MemoViewModel _ownerViewModel;
    private readonly MemoItemViewModel? _existingItem;
    private readonly MemoEditViewModel _viewModel;
    private readonly MarkdownRenderer _markdownRenderer = new();

    private bool _isWebViewReady;
    private double _markdownEditFontSize;
    private double _markdownPreviewZoom = 1.0;

    /// <summary>編集対象またはテンプレート下書きからViewModelを構築し初期化する</summary>
    /// <param name="settingsService">設定サービス</param>
    /// <param name="imageStorageService">画像保存サービス</param>
    /// <param name="ownerViewModel">親メモViewModel</param>
    /// <param name="existing">既存メモ（新規時は null）</param>
    /// <param name="draft">テンプレート下書き</param>
    public MemoEditWindow(SettingsService settingsService, ImageStorageService imageStorageService, MemoViewModel ownerViewModel, MemoItemViewModel? existing, MemoDraft? draft = null)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _imageStorageService = imageStorageService;
        _ownerViewModel = ownerViewModel;
        _existingItem = existing;
        _viewModel = new MemoEditViewModel(settingsService, existing, draft);
        DataContext = _viewModel;

        _markdownEditFontSize = _viewModel.MarkdownEditFontSize;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        Loaded += async (_, _) => await InitializeWebViewAsync();
    }

    /// <summary>Markdownプレビュー用WebView2を初期化する</summary>
    private async System.Threading.Tasks.Task InitializeWebViewAsync()
    {
        try
        {
            string userDataFolder = Path.Combine(AppPaths.DataFolder, "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await MarkdownPreview.EnsureCoreWebView2Async(env);
            _isWebViewReady = true;

            if (_viewModel.IsMarkdownMode)
            {
                RenderPreview();
            }
        }
        catch
        {
            // WebView2 ランタイム未導入等の環境では、プレビューのみ利用不可として続行する
        }
    }

    /// <summary>現在の本文からMarkdownプレビューHTMLを生成し描画する</summary>
    private void RenderPreview()
    {
        if (!_isWebViewReady)
        {
            return;
        }

        string html = _markdownRenderer.RenderHtmlDocument(
            _viewModel.Body,
            _viewModel.IsDarkMode,
            _viewModel.MarkdownViewFontFamily,
            _viewModel.MarkdownViewFontSize,
            _viewModel.MarkdownCodeFontFamily,
            _viewModel.MarkdownCodeFontSize);

        MarkdownPreview.CoreWebView2?.NavigateToString(html);
        if (MarkdownPreview.CoreWebView2 is not null)
        {
            MarkdownPreview.ZoomFactor = _markdownPreviewZoom;
        }
    }

    /// <summary>テキストモードへの切り替えを反映する</summary>
    private void TextModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.IsMarkdownMode = false;
    }

    /// <summary>Markdownモードへの切り替えを反映しプレビューを更新する</summary>
    private void MarkdownModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.IsMarkdownMode = true;
        RenderPreview();
    }

    /// <summary>Markdown編集内容の変更に応じてプレビューを再描画する</summary>
    private void MarkdownEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        RenderPreview();
    }

    /// <summary>Ctrl+V押下時にクリップボード画像を検出してMarkdown記法で挿入する</summary>
    private void MarkdownEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 通常の TextBox は「クリップボードに画像しか無い（テキストが無い）」場合、
        // 既定の Paste コマンドが CanExecute=false のため Ctrl+V が完全に無効化されてしまう。
        // そのため画像がある場合のみここで独自にハンドリングし、それ以外は既定のテキスト貼り付けに委ねる。
        bool isPasteGesture = e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!isPasteGesture)
        {
            return;
        }

        BitmapSource? image = ClipboardImageHelper.GetImage();
        if (image is null)
        {
            return;
        }

        InsertAtSelection(BuildImageMarkdown(image));
        e.Handled = true;
    }

    /// <summary>画像を保存し、対応するMarkdown画像記法の文字列を生成する</summary>
    private string BuildImageMarkdown(BitmapSource image)
    {
        try
        {
            string absolutePath = _imageStorageService.SaveMemoImage(image, DateTime.Now);
            return $"![screenshot]({absolutePath.Replace('\\', '/')})";
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>選択範囲を置き換える形でエディタにテキストを挿入する</summary>
    private void InsertAtSelection(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        int start = MarkdownEditor.SelectionStart;
        int length = MarkdownEditor.SelectionLength;
        MarkdownEditor.Text = MarkdownEditor.Text.Remove(start, length).Insert(start, text);
        MarkdownEditor.CaretIndex = start + text.Length;
    }

    /// <summary>タグ選択ポップアップを開く</summary>
    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        TagPickerPopup.IsOpen = true;
    }

    /// <summary>フォント設定ポップアップを開く</summary>
    private void FontSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        FontSettingsPopup.IsOpen = true;
    }

    /// <summary>フォント設定変更をエディタ・プレビューへ反映する</summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MemoEditViewModel.TextFontFamily):
                PlainTextEditor.FontFamily = new FontFamily(_viewModel.TextFontFamily);
                break;
            case nameof(MemoEditViewModel.TextFontSize):
                PlainTextEditor.FontSize = _viewModel.TextFontSize;
                break;
            case nameof(MemoEditViewModel.MarkdownEditFontFamily):
                MarkdownEditor.FontFamily = new FontFamily(_viewModel.MarkdownEditFontFamily);
                break;
            case nameof(MemoEditViewModel.MarkdownEditFontSize):
                _markdownEditFontSize = _viewModel.MarkdownEditFontSize;
                MarkdownEditor.FontSize = _markdownEditFontSize;
                break;
            case nameof(MemoEditViewModel.MarkdownViewFontFamily):
            case nameof(MemoEditViewModel.MarkdownViewFontSize):
            case nameof(MemoEditViewModel.MarkdownCodeFontFamily):
            case nameof(MemoEditViewModel.MarkdownCodeFontSize):
                RenderPreview();
                break;
        }
    }

    /// <summary>選択したタグをメモに追加する</summary>
    private void TagOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tagName })
        {
            _viewModel.AddTag(tagName);
        }

        TagPickerPopup.IsOpen = false;
    }

    /// <summary>Ctrl+ホイールでテキストエディタのフォントサイズを拡大縮小する</summary>
    private void PlainTextEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        double delta = e.Delta > 0 ? 1 : -1;
        PlainTextEditor.FontSize = Clamp(PlainTextEditor.FontSize + delta, MinFontSize, MaxFontSize);
        e.Handled = true;
    }

    /// <summary>Ctrl+ホイールでMarkdownエディタのフォントサイズとプレビューを拡大縮小する</summary>
    private void MarkdownEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        double delta = e.Delta > 0 ? 1 : -1;
        _markdownEditFontSize = Clamp(_markdownEditFontSize + delta, MinFontSize, MaxFontSize);
        MarkdownEditor.FontSize = _markdownEditFontSize;

        AdjustPreviewZoom(delta);
        e.Handled = true;
    }

    /// <summary>Ctrl+ホイールでMarkdownプレビューのズーム倍率を変更する</summary>
    private void MarkdownPreview_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        double delta = e.Delta > 0 ? 1 : -1;
        AdjustPreviewZoom(delta);
        e.Handled = true;
    }

    /// <summary>プレビューのズーム倍率を差分値で調整する</summary>
    private void AdjustPreviewZoom(double delta)
    {
        _markdownPreviewZoom = Clamp(_markdownPreviewZoom + delta * 0.1, 0.5, 3.0);
        if (_isWebViewReady && MarkdownPreview.CoreWebView2 is not null)
        {
            MarkdownPreview.ZoomFactor = _markdownPreviewZoom;
        }
    }

    /// <summary>値を指定範囲内に収める</summary>
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    /// <summary>入力内容でメモを保存しウィンドウを閉じる</summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string title = string.IsNullOrWhiteSpace(_viewModel.Title)
            ? FirstLineOf(_viewModel.Body)
            : _viewModel.Title.Trim();

        var mode = _viewModel.IsMarkdownMode ? MemoMode.Markdown : MemoMode.Text;
        var names = _viewModel.TagNames.ToList();
        _ownerViewModel.SaveFromEditor(_existingItem, title, mode, _viewModel.Body, names);
        DialogResult = true;
        Close();
    }

    /// <summary>本文の先頭行からタイトル候補文字列を生成する</summary>
    private static string FirstLineOf(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return LocalizationManager.Instance["Memo_NewTitle"];
        }

        string firstLine = body.Split('\n')[0].Trim();
        return firstLine.Length > 60 ? firstLine[..60] : firstLine;
    }

    /// <summary>編集をキャンセルしてウィンドウを閉じる</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
