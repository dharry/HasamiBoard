using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorCode;
using HasamiBoard.Services;
using HasamiBoard.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HasamiBoard.Views;

/// <summary>各種テキスト変換機能をまとめた変換ツールウィンドウ</summary>
public partial class ConverterWindow : Window
{
    private readonly ConverterViewModel _viewModel;
    private readonly CodeHighlightRenderer _codeHighlightRenderer = new();
    private readonly Dictionary<ConverterMode, WebView2> _highlightViews;
    private readonly Dictionary<ConverterMode, ILanguage> _highlightLanguages = new()
    {
        [ConverterMode.Json] = Languages.FindById("json"),
        [ConverterMode.Xml] = Languages.Xml,
        [ConverterMode.HtmlFormat] = Languages.Html,
        [ConverterMode.Css] = Languages.Css,
        [ConverterMode.JavaScript] = Languages.JavaScript,
        [ConverterMode.Sql] = Languages.Sql,
    };
    private readonly HashSet<ConverterMode> _initializedHighlightViews = [];
    private CoreWebView2Environment? _sharedWebViewEnvironment;
    private readonly Expander[] _categoryExpanders = [];

    /// <summary>ViewModelとウィンドウ位置を初期化し、状態保存を設定する</summary>
    public ConverterWindow()
    {
        InitializeComponent();
        _viewModel = new ConverterViewModel();
        DataContext = _viewModel;

        _highlightViews = new Dictionary<ConverterMode, WebView2>
        {
            [ConverterMode.Json] = JsonOutputView,
            [ConverterMode.Xml] = XmlOutputView,
            [ConverterMode.HtmlFormat] = HtmlFormatOutputView,
            [ConverterMode.Css] = CssOutputView,
            [ConverterMode.JavaScript] = JavaScriptOutputView,
            [ConverterMode.Sql] = SqlOutputView,
        };

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.RegexMatchesUpdated += (_, _) => RenderRegexHighlight();

        _categoryExpanders =
        [
            CategoryEncodeDecodeExpander, CategoryFormatterExpander, CategoryDateTimeExpander,
            CategoryGeneratorExpander, CategoryTextAnalysisExpander,
        ];
        RestoreCategoryExpansion();

        RefreshModeOrder();
        RefreshModeVisibility();
        ExpandCategoryContaining(_viewModel.SelectedMode);
        Activated += (_, _) =>
        {
            RefreshModeOrder();
            RefreshModeVisibility();
        };

        var settings = App.SettingsService.Current;
        WindowPlacementHelper.Restore(this, settings.ConverterWindowLeft, settings.ConverterWindowTop, settings.ConverterWindowWidth, settings.ConverterWindowHeight);

        Closing += (_, _) =>
        {
            WindowPlacementHelper.SaveIfNormal(this, (left, top, width, height) =>
            {
                settings.ConverterWindowLeft = left;
                settings.ConverterWindowTop = top;
                settings.ConverterWindowWidth = width;
                settings.ConverterWindowHeight = height;
            });

            _viewModel.PersistState();
            App.SettingsService.Save();
        };

        Loaded += async (_, _) => await RenderCurrentModeHighlightAsync();
    }

    /// <summary>外部 (スクリーンショット機能等) から渡された画像を、QRコードモードに切り替えてデコード対象として表示する</summary>
    public void ShowQrDecodeImage(BitmapSource image) => _viewModel.LoadQrDecodeImageFromExternalSource(image);

    /// <summary>出力やモード変更時にシンタックスハイライト表示を再描画する</summary>
    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConverterViewModel.SelectedMode))
        {
            ExpandCategoryContaining(_viewModel.SelectedMode);
        }

        if (e.PropertyName is nameof(ConverterViewModel.OutputText)
            or nameof(ConverterViewModel.SelectedMode)
            or nameof(ConverterViewModel.EditFontFamily)
            or nameof(ConverterViewModel.EditFontSize))
        {
            await RenderCurrentModeHighlightAsync();
        }
    }

    /// <summary>設定に保存された折りたたみ状態を各カテゴリへ適用する</summary>
    private void RestoreCategoryExpansion()
    {
        var collapsed = App.SettingsService.Current.ConverterCollapsedCategories;
        foreach (var expander in _categoryExpanders)
        {
            if (expander.Tag is string categoryName)
            {
                expander.IsExpanded = !collapsed.Contains(categoryName);
            }
        }
    }

    /// <summary>カテゴリの折りたたみ/展開状態を設定へ即時反映する</summary>
    private void CategoryExpander_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander { Tag: string categoryName } expander)
        {
            return;
        }

        var collapsed = App.SettingsService.Current.ConverterCollapsedCategories;
        if (expander.IsExpanded)
        {
            collapsed.Remove(categoryName);
        }
        else if (!collapsed.Contains(categoryName))
        {
            collapsed.Add(categoryName);
        }

        App.SettingsService.Save();
    }

    /// <summary>指定モードを含むカテゴリが折りたたまれていれば展開する (選択中モードを常に視認可能にする)</summary>
    private void ExpandCategoryContaining(ConverterMode mode)
    {
        string modeName = mode.ToString();
        foreach (var expander in _categoryExpanders)
        {
            if (!expander.IsExpanded
                && expander.Content is StackPanel panel
                && panel.Children.OfType<RadioButton>().Any(b => b.CommandParameter as string == modeName))
            {
                expander.IsExpanded = true;
            }
        }
    }

    /// <summary>フォント設定ポップアップを開く</summary>
    private void FontSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        FontSettingsPopup.IsOpen = true;
    }

    /// <summary>設定に保存された並び順に従って、各カテゴリ内のサブ開発ツール項目を並べ替える (並び替えはカテゴリ内に限る)</summary>
    private void RefreshModeOrder()
    {
        var order = App.SettingsService.Current.ConverterModeOrder;
        if (order.Count == 0)
        {
            return;
        }

        foreach (var panel in GetCategoryItemPanels())
        {
            var remaining = panel.Children.OfType<RadioButton>().ToList();
            panel.Children.Clear();

            foreach (var modeName in order)
            {
                int index = remaining.FindIndex(b => b.CommandParameter is string buttonModeName && buttonModeName == modeName);
                if (index < 0)
                {
                    continue;
                }

                panel.Children.Add(remaining[index]);
                remaining.RemoveAt(index);
            }

            foreach (var button in remaining)
            {
                panel.Children.Add(button);
            }
        }
    }

    /// <summary>設定で無効化されたモードのサイドバー項目を非表示にし、選択中モードが無効化されていれば有効な別モードへ切り替える。
    /// 配下の全モードが無効化されたカテゴリは見出しごと非表示にする。</summary>
    private void RefreshModeVisibility()
    {
        var disabledModes = App.SettingsService.Current.ConverterDisabledModes;

        foreach (var expander in _categoryExpanders)
        {
            if (expander.Content is not StackPanel panel)
            {
                continue;
            }

            bool anyVisible = false;
            foreach (var radioButton in panel.Children.OfType<RadioButton>())
            {
                if (radioButton.CommandParameter is string modeName)
                {
                    bool visible = !disabledModes.Contains(modeName);
                    radioButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    anyVisible |= visible;
                }
            }

            expander.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        if (disabledModes.Contains(_viewModel.SelectedMode.ToString()))
        {
            _viewModel.SelectedMode = Enum.GetValues<ConverterMode>()
                .FirstOrDefault(mode => !disabledModes.Contains(mode.ToString()));
        }
    }

    /// <summary>各カテゴリのサブ開発ツール項目を格納するStackPanelを列挙する</summary>
    private IEnumerable<StackPanel> GetCategoryItemPanels()
        => _categoryExpanders.Select(expander => expander.Content).OfType<StackPanel>();

    /// <summary>QRコードモード表示中にCtrl+Vが押され、クリップボードに画像がある場合はQRコードデコード対象として読み込む。
    /// ウィンドウ全体で受け付けることで、入力欄等にフォーカスがあっても貼り付け可能にする
    /// (クリップボードにテキストしか無い場合は素通りさせ、通常のテキスト貼り付けを妨げない)。</summary>
    private void ConverterWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            && _viewModel.SelectedMode == ConverterMode.QrCode && Clipboard.ContainsImage())
        {
            _viewModel.PasteQrDecodeImageCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>正規表現の入力補助トークンを、パターン入力欄のカーソル位置へ挿入する</summary>
    private void RegexTokenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: string token })
        {
            return;
        }

        int caret = RegexPatternTextBox.CaretIndex;
        string text = _viewModel.RegexPatternText;
        _viewModel.RegexPatternText = text.Insert(caret, token);
        RegexPatternTextBox.CaretIndex = caret + token.Length;
        RegexPatternTextBox.Focus();
    }

    /// <summary>jqテストのフィルタ入力欄のカーソル位置へ、指定のトークンを挿入する</summary>
    private void InsertJqToken(string token)
    {
        int caret = JqFilterTextBox.CaretIndex;
        string text = _viewModel.JqFilterText;
        _viewModel.JqFilterText = text.Insert(caret, token);
        JqFilterTextBox.CaretIndex = caret + token.Length;
        JqFilterTextBox.Focus();
    }

    /// <summary>よく使うフィルタのチップボタンから、対応するトークンをフィルタ入力欄のカーソル位置へ挿入する</summary>
    private void JqTokenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: string token })
        {
            return;
        }

        InsertJqToken(token);
    }

    /// <summary>キーパス・ナビゲーターで選択したノードのjqパスを、フィルタ入力欄のカーソル位置へ挿入する</summary>
    private void JqTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is JqPathNode node)
        {
            InsertJqToken(node.JqPath);
        }
    }

    /// <summary>現在の変換モードに応じたシンタックスハイライトをWebViewへ描画する</summary>
    private async Task RenderCurrentModeHighlightAsync()
    {
        if (!_highlightViews.TryGetValue(_viewModel.SelectedMode, out var view))
        {
            return;
        }

        if (!_initializedHighlightViews.Contains(_viewModel.SelectedMode))
        {
            await EnsureHighlightViewInitializedAsync(_viewModel.SelectedMode, view);
        }

        if (view.CoreWebView2 is null)
        {
            return;
        }

        string html = _codeHighlightRenderer.RenderHtmlDocument(
            _viewModel.OutputText,
            _highlightLanguages[_viewModel.SelectedMode],
            App.SettingsService.Current.IsDarkMode,
            _viewModel.EditFontFamily,
            _viewModel.EditFontSize);

        view.CoreWebView2.NavigateToString(html);
    }

    /// <summary>正規表現テスターのテスト文字列にマッチ箇所のハイライトを反映する</summary>
    private void RenderRegexHighlight()
    {
        var textBlock = RegexHighlightTextBlock;
        textBlock.Inlines.Clear();

        string text = _viewModel.RegexTestInputText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var highlightBrush = (Brush)FindResource("FlashBrush");
        int cursor = 0;
        foreach (var match in _viewModel.RegexMatches.OrderBy(m => m.Index))
        {
            if (match.Index > text.Length)
            {
                continue;
            }

            if (match.Index > cursor)
            {
                textBlock.Inlines.Add(new Run(text[cursor..match.Index]));
            }

            int matchEnd = Math.Min(match.Index + match.Value.Length, text.Length);
            if (matchEnd > match.Index)
            {
                textBlock.Inlines.Add(new Run(text[match.Index..matchEnd]) { Background = highlightBrush });
            }

            cursor = Math.Max(cursor, matchEnd);
        }

        if (cursor < text.Length)
        {
            textBlock.Inlines.Add(new Run(text[cursor..]));
        }
    }

    /// <summary>指定モードのWebView2を初回のみ初期化する</summary>
    private async Task EnsureHighlightViewInitializedAsync(ConverterMode mode, WebView2 view)
    {
        try
        {
            _sharedWebViewEnvironment ??= await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(AppPaths.DataFolder, "WebView2"));
            await view.EnsureCoreWebView2Async(_sharedWebViewEnvironment);
            _initializedHighlightViews.Add(mode);
        }
        catch
        {
            // WebView2 ランタイム未導入等の環境では、シンタックスハイライトのみ利用不可として続行する
        }
    }
}
