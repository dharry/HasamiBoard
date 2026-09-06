using System.Runtime.InteropServices;

namespace HasamiBoard.Interop;

/// <summary>Win32 API の P/Invoke宣言とラッパーメソッドをまとめたクラス。</summary>
internal static class NativeMethods
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_HOTKEY = 0x0312;

    /// <summary>グローバルホットキーを登録する。</summary>
    /// <param name="hWnd">ウィンドウハンドル</param>
    /// <param name="id">ホットキーID</param>
    /// <param name="fsModifiers">修飾キー (Ctrl/Shift/Alt 等)</param>
    /// <param name="vk">仮想キーコード</param>
    /// <returns>成功時は true</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>登録済みのグローバルホットキーを解除する。</summary>
    /// <param name="hWnd">ウィンドウハンドル</param>
    /// <param name="id">ホットキーID</param>
    /// <returns>成功時は true</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>ウィンドウの拡張属性 (タイトルバーのダークモード等) を設定する。</summary>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    /// <summary>ウィンドウのタイトルバーをダーク/ライトに切り替える (Windows 10 1809+)。</summary>
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <param name="isDarkMode">true でダークモード、false でライトモード</param>
    public static void SetTitleBarTheme(IntPtr hwnd, bool isDarkMode)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int useDark = isDarkMode ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    /// <summary>クリップボード内容変更の通知先としてウィンドウを登録する。</summary>
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <returns>成功時は true</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    /// <summary>クリップボード内容変更の通知登録を解除する。</summary>
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <returns>成功時は true</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
