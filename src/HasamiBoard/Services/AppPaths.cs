using System.IO;

namespace HasamiBoard.Services;

/// <summary>
/// アプリのデータ保存先フォルダを解決する。
/// 通常は %LOCALAPPDATA%\HasamiBoard だが、開発・テスト時に実データを汚さないよう
/// 環境変数 HASAMIBOARD_DATA_DIR が設定されていればそちらを優先する。
/// </summary>
internal static class AppPaths
{
    public static string DataFolder { get; } = ResolveDataFolder();

    /// <summary>データ保存先フォルダを決定し、存在しなければ作成する。</summary>
    private static string ResolveDataFolder()
    {
        string? overridePath = Environment.GetEnvironmentVariable("HASAMIBOARD_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            Directory.CreateDirectory(overridePath);
            return overridePath;
        }

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HasamiBoard");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
