using System.IO;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace HasamiBoard.Services;

/// <summary>スクリーンショット/メモ画像のファイル保存・主要色抽出・不要ファイル整理を行う。</summary>
public class ImageStorageService
{
    private readonly SettingsService _settingsService;

    /// <summary>画像保存に使う設定サービスを受け取って初期化する。</summary>
    /// <param name="settingsService">画像保存設定サービス</param>
    public ImageStorageService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>クリップボードのビットマップを設定形式で保存し、絶対パスを返す。</summary>
    /// <param name="bitmapSource">保存するビットマップ</param>
    /// <param name="timestamp">保存日時</param>
    /// <returns>保存されたファイルの絶対パス</returns>
    public string SaveScreenshot(BitmapSource bitmapSource, DateTime timestamp)
        => SaveImage(bitmapSource, "screenshot", string.Empty, timestamp);

    /// <summary>メモに貼り付けられたビットマップを設定形式で保存し、絶対パスを返す。</summary>
    /// <param name="bitmapSource">保存するビットマップ</param>
    /// <param name="timestamp">保存日時</param>
    /// <returns>保存されたファイルの絶対パス</returns>
    public string SaveMemoImage(BitmapSource bitmapSource, DateTime timestamp)
        => SaveImage(bitmapSource, "memo", "memo_", timestamp);

    /// <summary>ビットマップを設定形式でエンコードし、日付別フォルダへファイル保存する。</summary>
    private string SaveImage(BitmapSource bitmapSource, string subFolder, string fileNamePrefix, DateTime timestamp)
    {
        string format = _settingsService.Current.ScreenshotImageFormat;
        string ext = format switch
        {
            "jpg" => "jpg",
            "webp" => "webp",
            _ => "png",
        };

        string relativeDir = Path.Combine(
            "images", subFolder,
            timestamp.ToString("yyyy"), timestamp.ToString("MM"), timestamp.ToString("dd"));
        string absoluteDir = Path.Combine(AppPaths.DataFolder, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        string fileName = $"{fileNamePrefix}{timestamp:HHmmss}_{Guid.NewGuid():N}.{ext}";
        string absolutePath = Path.Combine(absoluteDir, fileName);

        using var pngStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(pngStream);
        pngStream.Position = 0;

        using var bitmap = SKBitmap.Decode(pngStream);
        using SKData data = format switch
        {
            "jpg" => bitmap.Encode(SKEncodedImageFormat.Jpeg, _settingsService.Current.ScreenshotJpegQuality),
            "webp" => EncodeWebp(bitmap),
            _ => bitmap.Encode(SKEncodedImageFormat.Png, 100),
        };

        using (var fileStream = File.Create(absolutePath))
        {
            data.SaveTo(fileStream);
        }

        return absolutePath;
    }

    /// <summary>ビットマップを設定の品質/可逆設定でWebP形式にエンコードする。</summary>
    private SKData EncodeWebp(SKBitmap bitmap)
    {
        var settings = _settingsService.Current;
        var options = new SKWebpEncoderOptions(
            settings.ScreenshotWebpLossless ? SKWebpEncoderCompression.Lossless : SKWebpEncoderCompression.Lossy,
            settings.ScreenshotWebpQuality);

        using var pixmap = bitmap.PeekPixels();
        return pixmap.Encode(options) ?? bitmap.Encode(SKEncodedImageFormat.Webp, (int)settings.ScreenshotWebpQuality);
    }

    /// <summary>画像の主要色を最大 maxColors 色まで抽出する (簡易ヒストグラム量子化)。</summary>
    /// <param name="absolutePath">抽出対象画像の絶対パス</param>
    /// <param name="maxColors">抽出する最大色数</param>
    /// <returns>抽出されたカラーコード（HEX）リスト</returns>
    public List<string> ExtractDominantColors(string absolutePath, int maxColors = 5)
    {
        try
        {
            using var original = SKBitmap.Decode(absolutePath);
            if (original is null)
            {
                return new List<string>();
            }

            using var small = original.Resize(new SKImageInfo(48, 48), SKSamplingOptions.Default) ?? original;

            var counts = new Dictionary<int, int>();
            for (int y = 0; y < small.Height; y++)
            {
                for (int x = 0; x < small.Width; x++)
                {
                    var pixel = small.GetPixel(x, y);
                    if (pixel.Alpha < 16)
                    {
                        continue;
                    }

                    int r = pixel.Red & 0xF0;
                    int g = pixel.Green & 0xF0;
                    int b = pixel.Blue & 0xF0;
                    int key = (r << 16) | (g << 8) | b;
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
            }

            // 近似色の重複を避けつつ、頻度の高い順に代表色を選ぶ
            const int minDistance = 40;
            var selected = new List<(int R, int G, int B)>();

            foreach (var kv in counts.OrderByDescending(kv => kv.Value))
            {
                int r = (kv.Key >> 16) & 0xFF;
                int g = (kv.Key >> 8) & 0xFF;
                int b = kv.Key & 0xFF;

                bool tooClose = selected.Any(c =>
                    Math.Abs(c.R - r) + Math.Abs(c.G - g) + Math.Abs(c.B - b) < minDistance);

                if (tooClose)
                {
                    continue;
                }

                selected.Add((r, g, b));
                if (selected.Count >= maxColors)
                {
                    break;
                }
            }

            return selected.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>指定パスの画像ファイルが存在すれば削除する。</summary>
    /// <param name="absolutePath">削除対象ファイルの絶対パス</param>
    public void DeleteImageFile(string absolutePath)
    {
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    /// <summary>指定ルート配下の空になった年/月/日フォルダを再帰的に削除する。</summary>
    /// <param name="rootPath">対象ルートパス</param>
    public static void RemoveEmptyDirectories(string rootPath)
    {
        foreach (string dir in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(dir) &&
                !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try
                {
                    Directory.Delete(dir);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
