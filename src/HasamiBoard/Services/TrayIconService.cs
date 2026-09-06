using System.Reflection;
using WinForms = System.Windows.Forms;

namespace HasamiBoard.Services;

/// <summary>タスクトレイ常駐アイコンとその右クリックメニューを管理する。</summary>
public class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ToolStripMenuItem _showMenuItem;
    private readonly WinForms.ToolStripMenuItem _converterMenuItem;
    private readonly WinForms.ToolStripMenuItem _errorListMenuItem;
    private readonly WinForms.ToolStripMenuItem _exitMenuItem;

    public event Action? ToggleRequested;
    public event Action? ShowRequested;
    public event Action? ConverterRequested;
    public event Action? ErrorListRequested;
    public event Action? ExitRequested;

    /// <summary>通知アイコンとコンテキストメニューを構築し常駐を開始する。</summary>
    public TrayIconService()
    {
        System.Drawing.Icon? icon = null;
        try
        {
            icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
        }
        catch
        {
            // アイコン取得に失敗しても常駐自体は継続する
        }

        _showMenuItem = new WinForms.ToolStripMenuItem(LocalizationManager.Instance["Tray_Show"]);
        _showMenuItem.Click += (_, _) => ShowRequested?.Invoke();

        _converterMenuItem = new WinForms.ToolStripMenuItem(LocalizationManager.Instance["Header_Converter"]);
        _converterMenuItem.Click += (_, _) => ConverterRequested?.Invoke();

        _errorListMenuItem = new WinForms.ToolStripMenuItem(LocalizationManager.Instance["Header_ErrorList"]);
        _errorListMenuItem.Click += (_, _) => ErrorListRequested?.Invoke();

        _exitMenuItem = new WinForms.ToolStripMenuItem(LocalizationManager.Instance["Tray_Exit"]);
        _exitMenuItem.Click += (_, _) => ExitRequested?.Invoke();

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add(_showMenuItem);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add(_converterMenuItem);
        contextMenu.Items.Add(_errorListMenuItem);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add(_exitMenuItem);

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            Text = "HasamiBoard",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                ToggleRequested?.Invoke();
            }
        };
    }

    /// <summary>言語切替時にメニュー文言を更新する。</summary>
    public void RefreshLocalization()
    {
        _showMenuItem.Text = LocalizationManager.Instance["Tray_Show"];
        _converterMenuItem.Text = LocalizationManager.Instance["Header_Converter"];
        _errorListMenuItem.Text = LocalizationManager.Instance["Header_ErrorList"];
        _exitMenuItem.Text = LocalizationManager.Instance["Tray_Exit"];
    }

    /// <summary>開発ツール機能の有効/無効設定に応じて、メニュー項目の利用可否を切り替える。</summary>
    public void SetConverterMenuItemEnabled(bool isEnabled) => _converterMenuItem.Enabled = isEnabled;

    /// <summary>通知アイコンを非表示にして破棄する。</summary>
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
