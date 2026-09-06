using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace HasamiBoard.Services;

/// <summary>OSクリップボードから画像を取得する共通処理 (メモの画像貼り付け・開発ツールのQRコードデコード等で共有)。</summary>
public static class ClipboardImageHelper
{
    private static readonly string[] ImageFileExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

    /// <summary>クリップボードのビットマップ、またはファイルドロップリスト中の画像ファイルを取得する。</summary>
    /// <returns>取得できた画像。クリップボードに画像が存在しない場合は null。</returns>
    public static BitmapSource? GetImage()
    {
        if (Clipboard.ContainsImage())
        {
            return Clipboard.GetImage();
        }

        // エクスプローラー等で画像ファイルを Ctrl+C した場合、クリップボードには
        // ビットマップではなくファイルパス一覧 (CF_HDROP) が格納されるため、こちらも救済する。
        if (Clipboard.ContainsFileDropList())
        {
            string? imageFile = Clipboard.GetFileDropList()
                .Cast<string>()
                .FirstOrDefault(path => ImageFileExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()));

            if (imageFile is not null)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(imageFile, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch (Exception ex) when (ex is IOException or NotSupportedException)
                {
                    return null;
                }
            }
        }

        return null;
    }
}
