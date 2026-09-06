using System.Windows;

namespace HasamiBoard.Services;

/// <summary>変換ツール/エラーメッセージ一覧など、子ウィンドウの位置・サイズの復元/保存を行う共通処理。</summary>
public static class WindowPlacementHelper
{
    /// <summary>
    /// 保存されていた位置・サイズを復元する。保存値が無い(初回起動)場合や画面外に
    /// 出てしまっている場合は、XAML側の既定の起動位置 (CenterOwner 等) をそのまま使う。
    /// </summary>
    /// <param name="window">復元対象のウィンドウ</param>
    /// <param name="savedLeft">保存されていた左端座標</param>
    /// <param name="savedTop">保存されていた上端座標</param>
    /// <param name="savedWidth">保存されていた幅</param>
    /// <param name="savedHeight">保存されていた高さ</param>
    public static void Restore(Window window, double? savedLeft, double? savedTop, double savedWidth, double savedHeight)
    {
        if (savedWidth > 0)
        {
            window.Width = savedWidth;
        }

        if (savedHeight > 0)
        {
            window.Height = savedHeight;
        }

        if (savedLeft is not double left || savedTop is not double top)
        {
            return;
        }

        double virtualLeft = SystemParameters.VirtualScreenLeft;
        double virtualTop = SystemParameters.VirtualScreenTop;
        double virtualWidth = SystemParameters.VirtualScreenWidth;
        double virtualHeight = SystemParameters.VirtualScreenHeight;

        bool isOnScreen =
            left + window.Width > virtualLeft &&
            left < virtualLeft + virtualWidth &&
            top + window.Height > virtualTop &&
            top < virtualTop + virtualHeight;

        if (isOnScreen)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
        }
    }

    /// <summary>ウィンドウが通常状態 (最大化/最小化されていない) の場合のみ、現在の位置・サイズを保存する。</summary>
    /// <param name="window">保存対象のウィンドウ</param>
    /// <param name="save">位置・サイズを保存するコールバック</param>
    public static void SaveIfNormal(Window window, Action<double, double, double, double> save)
    {
        if (window.WindowState == WindowState.Normal)
        {
            save(window.Left, window.Top, window.Width, window.Height);
        }
    }
}
