using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HasamiBoard.ViewModels;

namespace HasamiBoard.Views;

/// <summary>各種設定項目を表示・変更する設定パネル</summary>
public partial class SettingsPanel : UserControl
{
    /// <summary>コンポーネントを初期化する</summary>
    public SettingsPanel()
    {
        InitializeComponent();
    }

    /// <summary>スクリーンショット保存形式をPNGに設定する</summary>
    private void PngRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Settings.ScreenshotImageFormat = "png";
    }

    /// <summary>スクリーンショット保存形式をJPGに設定する</summary>
    private void JpgRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Settings.ScreenshotImageFormat = "jpg";
    }

    /// <summary>スクリーンショット保存形式をWebPに設定する</summary>
    private void WebpRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Settings.ScreenshotImageFormat = "webp";
    }

    /// <summary>マスタータグ名のダブルクリックでリネーム編集を開始する</summary>
    private void MasterTagName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: MasterTagEditViewModel tag })
        {
            tag.BeginRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>タグ名編集中のEnter/Escapeキーで確定・キャンセルする</summary>
    private void MasterTagNameEditTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MasterTagEditViewModel tag })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            tag.CommitRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tag.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>フォーカス喪失時にタグ名編集を確定する</summary>
    private void MasterTagNameEditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MasterTagEditViewModel tag })
        {
            tag.CommitRenameCommand.Execute(null);
        }
    }
}
