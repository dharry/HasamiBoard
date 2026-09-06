using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HasamiBoard.ViewModels;

namespace HasamiBoard.Views;

/// <summary>クリップボード履歴タブの一覧表示・操作を扱うユーザーコントロール</summary>
public partial class ClipboardHistoryView : UserControl
{
    /// <summary>コンポーネントを初期化する</summary>
    public ClipboardHistoryView()
    {
        InitializeComponent();
    }

    /// <summary>項目のダブルクリックでインライン編集を開始する</summary>
    private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardHistoryItemViewModel item })
        {
            item.BeginEditCommand.Execute(null);
        }
    }

    /// <summary>インライン編集中のEnter/Escapeキーで確定・キャンセルする</summary>
    private void EditTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ClipboardHistoryItemViewModel item })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            item.CommitEditCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            item.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>履歴一覧上でのキー操作（全選択・削除・Vim操作）を処理する</summary>
    private void HistoryListBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ClipboardHistoryViewModel viewModel)
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
            HistoryListBox.SelectAll();
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
    private void HistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ClipboardHistoryViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateSelection(HistoryListBox.SelectedItems.Cast<ClipboardHistoryItemViewModel>());
    }
}
