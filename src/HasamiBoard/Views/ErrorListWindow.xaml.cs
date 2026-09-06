using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HasamiBoard.Services;
using HasamiBoard.ViewModels;

namespace HasamiBoard.Views;

/// <summary>各種エラーコード体系を参照表示するエラーメッセージ一覧ウィンドウ</summary>
public partial class ErrorListWindow : Window
{
    private readonly ErrorListViewModel _viewModel;

    /// <summary>ViewModelとウィンドウ位置を初期化し、状態保存を設定する</summary>
    public ErrorListWindow()
    {
        InitializeComponent();
        _viewModel = new ErrorListViewModel();
        DataContext = _viewModel;

        var settings = App.SettingsService.Current;
        WindowPlacementHelper.Restore(this, settings.ErrorListWindowLeft, settings.ErrorListWindowTop, settings.ErrorListWindowWidth, settings.ErrorListWindowHeight);

        Closing += (_, _) =>
        {
            WindowPlacementHelper.SaveIfNormal(this, (left, top, width, height) =>
            {
                settings.ErrorListWindowLeft = left;
                settings.ErrorListWindowTop = top;
                settings.ErrorListWindowWidth = width;
                settings.ErrorListWindowHeight = height;
            });

            _viewModel.PersistState();
            App.SettingsService.Save();
        };
    }

    /// <summary>Ctrl+F等で検索ボックスへフォーカスを移動する</summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool isTextInputFocused = Keyboard.FocusedElement is TextBoxBase;

        if ((ctrl && e.Key == Key.F) || (!isTextInputFocused && e.Key == Key.OemQuestion))
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
        }
    }

    /// <summary>フォント設定ポップアップを開く</summary>
    private void FontSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        FontSettingsPopup.IsOpen = true;
    }
}
