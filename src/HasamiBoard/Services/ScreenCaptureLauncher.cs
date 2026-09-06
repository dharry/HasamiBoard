using System.Diagnostics;

namespace HasamiBoard.Services;

/// <summary>Windows標準の画面キャプチャ (Snipping Tool 領域選択) を起動する。</summary>
public static class ScreenCaptureLauncher
{
    /// <summary>Windows標準の画面キャプチャ (領域選択) を起動する。</summary>
    public static void Launch()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-screenclip:",
                UseShellExecute = true,
            });
        }
        catch
        {
            // Snipping Tool が利用できない環境では静かに無視する
        }
    }
}
