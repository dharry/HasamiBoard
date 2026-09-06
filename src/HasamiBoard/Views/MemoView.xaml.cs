using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HasamiBoard.Models;
using HasamiBoard.Services;
using HasamiBoard.ViewModels;

namespace HasamiBoard.Views;

/// <summary>メモタブの一覧表示・編集ウィンドウ起動を扱うユーザーコントロール</summary>
public partial class MemoView : UserControl
{
    private SettingsService? _settingsService;
    private ImageStorageService? _imageStorageService;
    private MemoTemplateRepository? _templateRepository;

    /// <summary>コンポーネントを初期化しDataContext変更イベントを購読する</summary>
    public MemoView()
    {
        InitializeComponent();
        DataContextChanged += MemoView_DataContextChanged;
    }

    /// <summary>子ウィンドウ生成に必要な依存サービスを設定する</summary>
    /// <param name="settingsService">設定サービス</param>
    /// <param name="imageStorageService">画像保存サービス</param>
    /// <param name="templateRepository">テンプレートリポジトリ</param>
    public void Initialize(SettingsService settingsService, ImageStorageService imageStorageService, MemoTemplateRepository templateRepository)
    {
        _settingsService = settingsService;
        _imageStorageService = imageStorageService;
        _templateRepository = templateRepository;
    }

    /// <summary>DataContext切替時にViewModelのイベント購読を張り替える</summary>
    private void MemoView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MemoViewModel oldVm)
        {
            oldVm.EditRequested -= OnEditRequested;
            oldVm.TemplateEditRequested -= OnTemplateEditRequested;
            oldVm.TemplateManagerRequested -= OnTemplateManagerRequested;
        }

        if (e.NewValue is MemoViewModel newVm)
        {
            newVm.EditRequested += OnEditRequested;
            newVm.TemplateEditRequested += OnTemplateEditRequested;
            newVm.TemplateManagerRequested += OnTemplateManagerRequested;
        }
    }

    /// <summary>メモ編集ウィンドウをダイアログ表示する</summary>
    private void OnEditRequested(MemoItemViewModel? existing, MemoDraft? draft)
    {
        if (DataContext is not MemoViewModel viewModel || _settingsService is null || _imageStorageService is null)
        {
            return;
        }

        var editWindow = new MemoEditWindow(_settingsService, _imageStorageService, viewModel, existing, draft)
        {
            Owner = Window.GetWindow(this),
        };
        editWindow.ShowDialog();
    }

    /// <summary>テンプレート編集ウィンドウをダイアログ表示し、保存されればテンプレート一覧を再読込する</summary>
    private void OnTemplateEditRequested(MemoTemplate? seed)
    {
        if (DataContext is not MemoViewModel viewModel || _settingsService is null || _templateRepository is null)
        {
            return;
        }

        var editWindow = new MemoTemplateEditWindow(_settingsService, _templateRepository, seed)
        {
            Owner = Window.GetWindow(this),
        };

        if (editWindow.ShowDialog() == true)
        {
            viewModel.ReloadTemplates();
        }
    }

    /// <summary>テンプレート管理ウィンドウをダイアログ表示し、変更があればテンプレート一覧を再読込する</summary>
    private void OnTemplateManagerRequested()
    {
        if (DataContext is not MemoViewModel viewModel || _settingsService is null || _templateRepository is null)
        {
            return;
        }

        var managerWindow = new MemoTemplateManagerWindow(_settingsService, _templateRepository)
        {
            Owner = Window.GetWindow(this),
        };
        managerWindow.ShowDialog();

        if (managerWindow.HasChanges)
        {
            viewModel.ReloadTemplates();
        }
    }

    /// <summary>項目のダブルクリックでメモ編集ウィンドウを開く</summary>
    private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MemoItemViewModel item })
        {
            item.OpenEditCommand.Execute(null);
        }
    }

    /// <summary>メモ一覧上でのキー操作（全選択・削除・Vim操作）を処理する</summary>
    private void MemoListBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MemoViewModel viewModel)
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
            MemoListBox.SelectAll();
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
    private void MemoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MemoViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateSelection(MemoListBox.SelectedItems.Cast<MemoItemViewModel>());
    }
}
