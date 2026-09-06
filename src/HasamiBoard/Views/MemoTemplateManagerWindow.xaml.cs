using System.Collections.ObjectModel;
using System.Windows;
using HasamiBoard.Models;
using HasamiBoard.Services;

namespace HasamiBoard.Views;

/// <summary>登録済みメモテンプレートの一覧管理（新規作成・編集・削除）ウィンドウ</summary>
public partial class MemoTemplateManagerWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly MemoTemplateRepository _repository;

    public ObservableCollection<MemoTemplate> Templates { get; } = new();

    /// <summary>ウィンドウを閉じた時点でテンプレート一覧に変更があったかどうか。</summary>
    public bool HasChanges { get; private set; }

    /// <summary>依存サービスを受け取りテンプレート一覧を読み込む</summary>
    /// <param name="settingsService">設定サービス</param>
    /// <param name="repository">テンプレートリポジトリ</param>
    public MemoTemplateManagerWindow(SettingsService settingsService, MemoTemplateRepository repository)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _repository = repository;
        DataContext = this;

        LoadTemplates();
    }

    /// <summary>リポジトリからテンプレート一覧を再読み込みして表示を更新する</summary>
    private void LoadTemplates()
    {
        Templates.Clear();
        foreach (var template in _repository.GetAll())
        {
            Templates.Add(template);
        }

        bool isEmpty = Templates.Count == 0;
        EmptyText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        TemplateListBox.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>新規テンプレート作成用の編集画面を開く</summary>
    private void NewTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        OpenEditor(null);
    }

    /// <summary>選択したテンプレートの編集画面を開く</summary>
    private void EditTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MemoTemplate template })
        {
            OpenEditor(template);
        }
    }

    /// <summary>テンプレート編集ウィンドウをダイアログ表示し、変更があれば一覧を更新する</summary>
    private void OpenEditor(MemoTemplate? seed)
    {
        var editWindow = new MemoTemplateEditWindow(_settingsService, _repository, seed)
        {
            Owner = this,
        };

        if (editWindow.ShowDialog() == true)
        {
            HasChanges = true;
            LoadTemplates();
        }
    }

    /// <summary>確認ダイアログの上でテンプレートを削除する</summary>
    private void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MemoTemplate template })
        {
            return;
        }

        var result = MessageBox.Show(
            string.Format(LocalizationManager.Instance["Template_DeleteConfirmMessage"], template.Name),
            LocalizationManager.Instance["Template_DeleteConfirmTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _repository.Delete(template.Id);
        HasChanges = true;
        LoadTemplates();
    }
}
