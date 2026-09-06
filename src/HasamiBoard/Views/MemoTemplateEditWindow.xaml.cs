using System.Windows;
using HasamiBoard.Models;
using HasamiBoard.Services;
using HasamiBoard.ViewModels;

namespace HasamiBoard.Views;

/// <summary>メモテンプレートの新規作成・編集を行う子ウィンドウ</summary>
public partial class MemoTemplateEditWindow : Window
{
    private readonly MemoTemplateRepository _repository;
    private readonly MemoTemplateEditViewModel _viewModel;

    /// <summary>編集元テンプレートからViewModelを構築し初期化する</summary>
    /// <param name="settingsService">設定サービス</param>
    /// <param name="repository">テンプレートリポジトリ</param>
    /// <param name="seed">編集対象テンプレート（新規時は null）</param>
    public MemoTemplateEditWindow(SettingsService settingsService, MemoTemplateRepository repository, MemoTemplate? seed)
    {
        InitializeComponent();

        _repository = repository;
        _viewModel = new MemoTemplateEditViewModel(settingsService, seed);
        DataContext = _viewModel;
    }

    /// <summary>テキストモードへの切り替えを反映する</summary>
    private void TextModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.IsMarkdownMode = false;
    }

    /// <summary>Markdownモードへの切り替えを反映する</summary>
    private void MarkdownModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.IsMarkdownMode = true;
    }

    /// <summary>タグ選択ポップアップを開く</summary>
    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        TagPickerPopup.IsOpen = true;
    }

    /// <summary>選択したタグをテンプレートに追加する</summary>
    private void TagOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tagName })
        {
            _viewModel.AddTag(tagName);
        }

        TagPickerPopup.IsOpen = false;
    }

    /// <summary>入力内容でテンプレートを保存しウィンドウを閉じる</summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string name = string.IsNullOrWhiteSpace(_viewModel.Name)
            ? FirstLineOf(_viewModel.Body)
            : _viewModel.Name.Trim();

        var template = _viewModel.Existing ?? new MemoTemplate();
        template.Name = name;
        template.Mode = _viewModel.IsMarkdownMode ? MemoMode.Markdown : MemoMode.Text;
        template.Body = _viewModel.Body;
        template.Tags = _viewModel.BuildTagList();

        if (string.IsNullOrEmpty(template.Id))
        {
            _repository.Insert(template);
        }
        else
        {
            _repository.Update(template);
        }

        DialogResult = true;
        Close();
    }

    /// <summary>編集をキャンセルしてウィンドウを閉じる</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>本文の先頭行からテンプレート名候補文字列を生成する</summary>
    private static string FirstLineOf(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return LocalizationManager.Instance["Template_NewTitle"];
        }

        string firstLine = body.Split('\n')[0].Trim();
        return firstLine.Length > 60 ? firstLine[..60] : firstLine;
    }
}
