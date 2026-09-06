using System.IO;

namespace HasamiBoard.Services;

/// <summary>未処理例外を %LOCALAPPDATA%\HasamiBoard\crash.log (または開発用フォルダ) へ追記する。</summary>
public static class CrashLogger
{
    /// <summary>例外内容をクラッシュログファイルへ追記する。</summary>
    /// <param name="ex">記録する例外オブジェクト</param>
    public static void Log(Exception ex)
    {
        try
        {
            string path = Path.Combine(AppPaths.DataFolder, "crash.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(path, entry);
        }
        catch
        {
            // ログ出力自体の失敗は無視する (これ以上ユーザーに通知する手段がないため)
        }
    }
}
