using System.Windows;
using System.Windows.Controls;
using HasamiBoard.Services;

namespace HasamiBoard;

/// <summary>アプリケーションのエントリーポイントおよび起動・終了処理を担うクラス</summary>
public partial class App : Application
{
    public static SettingsService SettingsService { get; private set; } = null!;
    public static ThemeService ThemeService { get; private set; } = null!;

    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    /// <summary>アプリ起動時の初期化処理（例外ハンドラ登録、DB初期化、多重起動防止、メイン画面表示）を行う</summary>
    /// <param name="e">アプリ起動イベント引数</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ツールチップの表示遅延をなくし、ホバーで即座に表示されるようにする
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(0));

        DispatcherUnhandledException += (_, args) =>
        {
            CrashLogger.Log(args.Exception);
            MessageBox.Show(
                LocalizationManager.Instance["App_UnhandledException_Message"] + "\n\n" + args.Exception.Message,
                LocalizationManager.Instance["AppTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                CrashLogger.Log(ex);
            }
        };

        LiteDbBootstrap.Configure();

        // 開発用データフォルダ (HASAMIBOARD_DATA_DIR) 使用時は本番インスタンスと衝突しないよう
        // ミューテックス名を分離する。本番ビルドでは未設定のため仕様通りの名前になる。
        string? devSuffix = Environment.GetEnvironmentVariable("HASAMIBOARD_DATA_DIR");
        string mutexName = string.IsNullOrWhiteSpace(devSuffix)
            ? $@"Local\HasamiBoard_SingleInstance_{Environment.UserName}"
            : $@"Local\HasamiBoard_SingleInstance_{Environment.UserName}_Dev_{devSuffix.GetHashCode()}";
        _singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
        _ownsSingleInstanceMutex = createdNew;

        if (!createdNew)
        {
            MessageBox.Show(
                LocalizationManager.Instance["App_AlreadyRunning_Message"],
                LocalizationManager.Instance["AppTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            SettingsService = new SettingsService();
            ThemeService = new ThemeService();

            LocalizationManager.Instance.ApplyLanguage(SettingsService.Current.Language);
            ThemeService.ApplyTheme(SettingsService.Current.IsDarkMode);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            // /hide: GUIを表示せずタスクトレイに常駐した状態で起動する（自動起動Runキーにはこのオプション付きで登録される）
            bool startHidden = e.Args.Any(arg => string.Equals(arg, "/hide", StringComparison.OrdinalIgnoreCase));
            if (startHidden)
            {
                // ディスパッチャーのメッセージループ開始前にShow直後Hideすることで、
                // 画面表示なしでSourceInitializedを発火させ、ホットキー登録・クリップボード監視を初期化する
                mainWindow.Show();
                mainWindow.Hide();
            }
            else
            {
                mainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            MessageBox.Show(
                LocalizationManager.Instance["App_StartupFailed_Message"] + "\n\n" + ex.Message,
                LocalizationManager.Instance["AppTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>アプリ終了時にミューテックスを解放する</summary>
    /// <param name="e">アプリ終了イベント引数</param>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
