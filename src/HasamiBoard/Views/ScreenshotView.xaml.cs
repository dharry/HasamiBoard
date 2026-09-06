using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HasamiBoard.ViewModels;

namespace HasamiBoard.Views;

/// <summary>スクリーンショットタブの一覧表示・操作を扱うユーザーコントロール</summary>
public partial class ScreenshotView : UserControl
{
    /// <summary>コンポーネントを初期化する</summary>
    public ScreenshotView()
    {
        InitializeComponent();
    }

    /// <summary>サムネイルのダブルクリックで画像ビューアーを開く</summary>
    private void Thumbnail_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: ScreenshotItemViewModel item })
        {
            item.OpenImageCommand.Execute(null);
        }
    }

    /// <summary>メモ部分のダブルクリックでインライン編集を開始する</summary>
    private void NoteText_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: ScreenshotItemViewModel item })
        {
            item.BeginEditNoteCommand.Execute(null);
        }
    }

    /// <summary>メモ編集中のCtrl+Enter/Escapeキーで確定・キャンセルする</summary>
    private void NoteEditTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ScreenshotItemViewModel item })
        {
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            item.CommitEditNoteCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            item.CancelEditNoteCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>スクリーンショット一覧上でのキー操作（全選択・削除・Vim操作）を処理する</summary>
    private void ScreenshotListBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ScreenshotViewModel viewModel)
        {
            return;
        }

        bool isTextInputFocused = Keyboard.FocusedElement is TextBoxBase;
        if (isTextInputFocused)
        {
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ScreenshotListBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (viewModel.SelectedItems.Count > 1)
            {
                viewModel.DeleteSelectedCommand.Execute(null);
            }
            else if (viewModel.SelectedItem is not null)
            {
                viewModel.DeleteItem(viewModel.SelectedItem);
            }

            e.Handled = true;
            return;
        }

        if (viewModel.Vim.HandleKey(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>一覧の選択項目変更をViewModelへ反映する</summary>
    private void ScreenshotListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ScreenshotViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateSelection(ScreenshotListBox.SelectedItems.Cast<ScreenshotItemViewModel>());
    }
}
