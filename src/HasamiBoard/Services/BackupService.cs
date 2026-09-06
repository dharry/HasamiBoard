using System.IO;
using System.IO.Compression;

namespace HasamiBoard.Services;

/// <summary>アプリのデータ (設定・データベース・画像) をZIPファイルへバックアップ/復元する。</summary>
public class BackupService
{
    private static readonly string[] DatabaseFileNames =
    {
        "Settings.json",
        "ClipboardHistory.db",
        "ScreenshotHistory.db",
        "Memos.db",
        "MemoTemplates.db",
    };

    private readonly string _dataFolder;

    /// <summary>バックアップ対象のデータフォルダを指定して初期化する。</summary>
    /// <param name="dataFolder">データフォルダパス（nullの場合は既定値を使用）</param>
    public BackupService(string? dataFolder = null)
    {
        _dataFolder = dataFolder ?? AppPaths.DataFolder;
    }

    /// <summary>設定・データベース・画像フォルダをZIPファイルへ出力する。</summary>
    /// <param name="destinationZipPath">出力先ZIPファイルパス</param>
    public void CreateBackup(string destinationZipPath)
    {
        if (File.Exists(destinationZipPath))
        {
            File.Delete(destinationZipPath);
        }

        using var zip = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);

        foreach (string fileName in DatabaseFileNames)
        {
            string path = Path.Combine(_dataFolder, fileName);
            if (File.Exists(path))
            {
                zip.CreateEntryFromFile(path, fileName, CompressionLevel.Optimal);
            }
        }

        string imagesFolder = Path.Combine(_dataFolder, "images");
        if (Directory.Exists(imagesFolder))
        {
            foreach (string file in Directory.EnumerateFiles(imagesFolder, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(_dataFolder, file).Replace('\\', '/');
                zip.CreateEntryFromFile(file, relative, CompressionLevel.Optimal);
            }
        }
    }

    /// <summary>
    /// バックアップZIPをデータフォルダへ展開する。呼び出し前に、対象ファイルを
    /// 使用中のLiteDB接続等はすべてDisposeしておくこと。
    /// </summary>
    /// <param name="sourceZipPath">復元元ZIPファイルパス</param>
    public void RestoreBackup(string sourceZipPath)
    {
        using var zip = ZipFile.OpenRead(sourceZipPath);

        string dataFolderFullPath = Path.GetFullPath(_dataFolder);
        string dataFolderPrefix = dataFolderFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? dataFolderFullPath
            : dataFolderFullPath + Path.DirectorySeparatorChar;

        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue; // ディレクトリエントリはスキップ
            }

            string destinationPath = Path.GetFullPath(
                Path.Combine(dataFolderFullPath, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

            if (!destinationPath.StartsWith(dataFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"バックアップZIPに不正なエントリが含まれています: {entry.FullName}");
            }

            string? destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }
}
