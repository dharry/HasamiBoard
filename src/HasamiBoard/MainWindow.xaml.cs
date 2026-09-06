using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HasamiBoard.Services;
using HasamiBoard.ViewModels;
using HasamiBoard.Views;

namespace HasamiBoard;

/// <summary>アプリのメインウィンドウ。タブ・ホットキー・トレイ常駐等を統括する</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly ClipboardHistoryRepository _clipboardHistoryRepository;
    private readonly ScreenshotHistoryRepository _screenshotHistoryRepository;
    private readonly MemoRepository _memoRepository;
    private readonly MemoTemplateRepository _memoTemplateRepository;
    private readonly ClipboardMonitorService _clipboardMonitorService;
    private readonly ImageStorageService _imageStorageService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly StartupRegistryService _startupRegistryService;

    private int _screenshotHotkeyId = -1;
    private int _colorPickerHotkeyId = -1;
    private int _converterHotkeyId = -1;
    private int _errorListHotkeyId = -1;
    private bool _isExiting;
    private bool _dataConnectionsDisposed;
    private ConverterWindow? _converterWindow;
    private ErrorListWindow? _errorListWindow;

    /// <summary>各サービス・ViewModelを初期化し、ウィンドウ表示状態を復元する</summary>
    public MainWindow()
    {
        InitializeComponent();

        _settingsService = App.SettingsService;
        _clipboardHistoryRepository = new ClipboardHistoryRepository();
        _screenshotHistoryRepository = new ScreenshotHistoryRepository();
        _memoRepository = new MemoRepository();
        _memoTemplateRepository = new MemoTemplateRepository();
        _clipboardMonitorService = new ClipboardMonitorService();
        _imageStorageService = new ImageStorageService(_settingsService);
        _hotkeyService = new HotkeyService();
        _startupRegistryService = new StartupRegistryService();
        var ocrService = new OcrService();

        var clipboardHistoryViewModel = new ClipboardHistoryViewModel(
            _settingsService,
            _clipboardHistoryRepository,
            _clipboardMonitorService);

        var screenshotViewModel = new ScreenshotViewModel(
            _settingsService,
            _screenshotHistoryRepository,
            _imageStorageService,
            _clipboardMonitorService,
            ocrService);

        var memoViewModel = new MemoViewModel(_settingsService, _memoRepository, _memoTemplateRepository, _clipboardMonitorService);

        var settingsViewModel = new SettingsViewModel(
            _settingsService,
            _startupRegistryService,
            clipboardHistoryViewModel,
            screenshotViewModel,
            memoViewModel,
            reloadMainViewModel: () => _viewModel!.ReloadFromSettings(),
            refreshHotkeys: RefreshHotkeys,
            prepareForRestore: DisposeDataConnections,
            exitApplication: ExitApplication);

        _viewModel = new MainViewModel(_settingsService, App.ThemeService, clipboardHistoryViewModel, screenshotViewModel, memoViewModel, settingsViewModel);
        clipboardHistoryViewModel.TagBadgeClicked += tag => _viewModel.ToggleQuickFilterToken(tag);
        screenshotViewModel.TagBadgeClicked += tag => _viewModel.ToggleQuickFilterToken(tag);
        memoViewModel.TagBadgeClicked += tag => _viewModel.ToggleQuickFilterToken(tag);
        screenshotViewModel.CreateMemoRequested += memoViewModel.RequestNewMemo;
        screenshotViewModel.AnalyzeQrCodeRequested += image => OpenConverterWindow(image);
        clipboardHistoryViewModel.CreateMemoRequested += memoViewModel.RequestNewMemo;
        DataContext = _viewModel;

        MemoViewControl.Initialize(_settingsService, _imageStorageService, _memoTemplateRepository);

        RestoreWindowPlacement();

        _trayIconService = new TrayIconService();
        _trayIconService.ToggleRequested += ToggleWindowVisibility;
        _trayIconService.ShowRequested += ShowAndActivate;
        _trayIconService.ConverterRequested += () => OpenConverterWindow();
        _trayIconService.ErrorListRequested += OpenErrorListWindow;
        _trayIconService.ExitRequested += () =>
        {
            _isExiting = true;
            Close();
        };
        _trayIconService.SetConverterMenuItemEnabled(_settingsService.Current.IsConverterEnabled);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Language))
            {
                _trayIconService.RefreshLocalization();
            }

            if (e.PropertyName == nameof(MainViewModel.IsConverterEnabled))
            {
                RefreshHotkeys();
                _trayIconService.SetConverterMenuItemEnabled(_viewModel.IsConverterEnabled);
            }
        };

        _startupRegistryService.SetEnabled(_settingsService.Current.IsAutoStart);

        SourceInitialized += (_, _) =>
        {
            _clipboardMonitorService.Attach(this);
            _hotkeyService.Attach(this);
            _hotkeyService.Register("Ctrl", "Q", ToggleWindowVisibility);
            RefreshHotkeys();
        };
    }

    /// <summary>設定に基づいてグローバルホットキーを再登録する</summary>
    private void RefreshHotkeys()
    {
        if (_screenshotHotkeyId >= 0)
        {
            _hotkeyService.Unregister(_screenshotHotkeyId);
            _screenshotHotkeyId = -1;
        }

        if (_colorPickerHotkeyId >= 0)
        {
            _hotkeyService.Unregister(_colorPickerHotkeyId);
            _colorPickerHotkeyId = -1;
        }

        if (_converterHotkeyId >= 0)
        {
            _hotkeyService.Unregister(_converterHotkeyId);
            _converterHotkeyId = -1;
        }

        if (_errorListHotkeyId >= 0)
        {
            _hotkeyService.Unregister(_errorListHotkeyId);
            _errorListHotkeyId = -1;
        }

        var settings = _settingsService.Current;

        if (settings.IsScreenshotHotkeyEnabled)
        {
            _screenshotHotkeyId = _hotkeyService.Register(settings.ScreenshotHotkeyModifiers, settings.ScreenshotHotkeyKey, ScreenCaptureLauncher.Launch);
        }

        if (settings.IsColorPickerHotkeyEnabled)
        {
            _colorPickerHotkeyId = _hotkeyService.Register(settings.ColorPickerHotkeyModifiers, settings.ColorPickerHotkeyKey, OpenColorPicker);
        }

        if (settings.IsConverterHotkeyEnabled && settings.IsConverterEnabled)
        {
            _converterHotkeyId = _hotkeyService.Register(settings.ConverterHotkeyModifiers, settings.ConverterHotkeyKey, () => OpenConverterWindow());
        }

        if (settings.IsErrorListHotkeyEnabled)
        {
            _errorListHotkeyId = _hotkeyService.Register(settings.ErrorListHotkeyModifiers, settings.ErrorListHotkeyKey, OpenErrorListWindow);
        }
    }

    /// <summary>カラーピッカーオーバーレイを開き、確定色を履歴へ追加する</summary>
    private void OpenColorPicker()
    {
        var overlay = new ColorPickerOverlayWindow();
        overlay.Completed += hex =>
        {
            if (hex is null)
            {
                return;
            }

            _clipboardMonitorService.NotifySelfWrite(hex);
            Clipboard.SetText(hex);
            _viewModel.ClipboardHistory.AddColorPickerEntry(hex);
        };
        overlay.Show();
    }

    /// <summary>カラーピッカーボタン押下時にオーバーレイを開く</summary>
    private void ColorPickerButton_Click(object sender, RoutedEventArgs e) => OpenColorPicker();

    /// <summary>変換ツールボタン押下時にウィンドウを開く</summary>
    private void ConverterButton_Click(object sender, RoutedEventArgs e) => OpenConverterWindow();

    /// <summary>エラー一覧ボタン押下時にウィンドウを開く</summary>
    private void ErrorListButton_Click(object sender, RoutedEventArgs e) => OpenErrorListWindow();

    /// <summary>変換ツールウィンドウを開く（既に開いていればアクティブ化）。
    /// <paramref name="qrDecodeImage"/> を指定した場合、QRコードモードへ切り替えてその画像を即座にデコードする</summary>
    private void OpenConverterWindow(BitmapSource? qrDecodeImage = null)
    {
        if (_converterWindow is null)
        {
            _converterWindow = new ConverterWindow { Owner = this };
            _converterWindow.Closed += (_, _) =>
            {
                _converterWindow = null;
                GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            };
            _converterWindow.Show();
        }
        else
        {
            _converterWindow.Activate();
        }

        if (qrDecodeImage is not null)
        {
            _converterWindow.ShowQrDecodeImage(qrDecodeImage);
        }
    }

    /// <summary>エラーメッセージ一覧ウィンドウを開く（既に開いていればアクティブ化）</summary>
    private void OpenErrorListWindow()
    {
        if (_errorListWindow is null)
        {
            _errorListWindow = new ErrorListWindow { Owner = this };
            _errorListWindow.Closed += (_, _) =>
            {
                _errorListWindow = null;
                GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            };
            _errorListWindow.Show();
        }
        else
        {
            _errorListWindow.Activate();
        }
    }

    /// <summary>ウィンドウを表示し前面にアクティブ化する</summary>
    private void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>ウィンドウの表示・非表示（タスクトレイ格納）を切り替える</summary>
    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
        }
        else
        {
            ShowAndActivate();
        }
    }

    /// <summary>保存された位置・サイズでウィンドウを復元する（画面外逸脱防止付き）</summary>
    private void RestoreWindowPlacement()
    {
        var settings = _settingsService.Current;

        double width = settings.WindowWidth > 0 ? settings.WindowWidth : Width;
        double height = settings.WindowHeight > 0 ? settings.WindowHeight : Height;
        double left = settings.WindowLeft;
        double top = settings.WindowTop;

        double virtualLeft = SystemParameters.VirtualScreenLeft;
        double virtualTop = SystemParameters.VirtualScreenTop;
        double virtualWidth = SystemParameters.VirtualScreenWidth;
        double virtualHeight = SystemParameters.VirtualScreenHeight;

        bool isOnScreen =
            left + width > virtualLeft &&
            left < virtualLeft + virtualWidth &&
            top + height > virtualTop &&
            top < virtualTop + virtualHeight;

        Width = width;
        Height = height;

        if (isOnScreen)
        {
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    /// <summary>ウィンドウを閉じる際に終了確認と設定保存・後始末を行う</summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            var result = MessageBox.Show(
                LocalizationManager.Instance["Confirm_ExitMessage"],
                LocalizationManager.Instance["Confirm_ExitTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        if (WindowState == WindowState.Normal)
        {
            _settingsService.Current.WindowLeft = Left;
            _settingsService.Current.WindowTop = Top;
            _settingsService.Current.WindowWidth = Width;
            _settingsService.Current.WindowHeight = Height;
        }

        _settingsService.Save();

        _trayIconService.Dispose();
        _hotkeyService.Dispose();
        _clipboardMonitorService.Dispose();
        DisposeDataConnections();
    }

    /// <summary>データベース接続を解放する。バックアップ復元でファイルを上書きする前や、終了時に呼び出す。</summary>
    private void DisposeDataConnections()
    {
        if (_dataConnectionsDisposed)
        {
            return;
        }

        _dataConnectionsDisposed = true;
        _clipboardHistoryRepository.Dispose();
        _screenshotHistoryRepository.Dispose();
        _memoRepository.Dispose();
        _memoTemplateRepository.Dispose();
    }

    /// <summary>確認なしでアプリを終了する。バックアップ復元後、変更を反映するための再起動を促す際に使用する。</summary>
    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    /// <summary>
    /// 開いているタグ選択/一括タグ選択/新規メモ選択のポップアップがあれば閉じる。
    /// Escキー押下時に、ポップアップより先にウィンドウ全体をタスクトレイへ格納してしまわないようにするため。
    /// </summary>
    private bool CloseAnyOpenPopup()
    {
        if (_viewModel.IsSettingsPanelOpen)
        {
            _viewModel.IsSettingsPanelOpen = false;
            return true;
        }

        if (_viewModel.ClipboardHistory.SelectedItem is { IsTagPickerOpen: true } clipboardItem)
        {
            clipboardItem.IsTagPickerOpen = false;
            return true;
        }

        if (_viewModel.ClipboardHistory.IsBulkTagPickerOpen)
        {
            _viewModel.ClipboardHistory.IsBulkTagPickerOpen = false;
            return true;
        }

        if (_viewModel.Screenshot.SelectedItem is { IsTagPickerOpen: true } screenshotItem)
        {
            screenshotItem.IsTagPickerOpen = false;
            return true;
        }

        if (_viewModel.Screenshot.IsBulkTagPickerOpen)
        {
            _viewModel.Screenshot.IsBulkTagPickerOpen = false;
            return true;
        }

        if (_viewModel.Memo.SelectedItem is { IsTagPickerOpen: true } memoItem)
        {
            memoItem.IsTagPickerOpen = false;
            return true;
        }

        if (_viewModel.Memo.IsBulkTagPickerOpen)
        {
            _viewModel.Memo.IsBulkTagPickerOpen = false;
            return true;
        }

        if (_viewModel.Memo.IsNewMemoPickerOpen)
        {
            _viewModel.Memo.IsNewMemoPickerOpen = false;
            return true;
        }

        return false;
    }

    /// <summary>ウィンドウ全体のキー操作（タブ切替・検索フォーカス・Escape）を処理する</summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (ctrl && e.Key == Key.Tab)
        {
            _viewModel.QuickFilterText = string.Empty;
            int count = 3;
            int next = shift
                ? (_viewModel.SelectedTabIndex - 1 + count) % count
                : (_viewModel.SelectedTabIndex + 1) % count;
            _viewModel.SelectedTabIndex = next;
            e.Handled = true;
            return;
        }

        bool isTextInputFocused = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;

        if ((ctrl && e.Key == Key.F) || (!isTextInputFocused && e.Key == Key.OemQuestion))
        {
            QuickFilterTextBox.Focus();
            QuickFilterTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (CloseAnyOpenPopup())
            {
                e.Handled = true;
                return;
            }

            Hide();
            e.Handled = true;
        }
    }

    /// <summary>設定パネル背景クリックで設定パネルを閉じる</summary>
    private void SettingsBackdrop_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _viewModel.IsSettingsPanelOpen = false;
    }

    /// <summary>タブ切替時にクイックフィルターをクリアする</summary>
    private void NavTabs_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // タブ切替時は、テキスト入力によるものかタグバッジクリックによるものかに関わらず
        // クイックフィルターを常にクリアする
        _viewModel.QuickFilterText = string.Empty;
    }
}
