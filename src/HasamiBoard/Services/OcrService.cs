using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace HasamiBoard.Services;

/// <summary>Windows標準の Windows.Media.Ocr API を用いた画像内テキスト認識。</summary>
public class OcrService
{
    /// <summary>指定画像ファイルに対してOCRを実行し、認識結果テキストを返す。</summary>
    /// <param name="imagePath">OCR処理対象の画像ファイルパス</param>
    /// <returns>認識されたテキスト</returns>
    public async Task<string> RecognizeTextAsync(string imagePath)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            throw new OcrEngineUnavailableException();
        }

        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        SoftwareBitmap ocrBitmap = softwareBitmap;
        bool isConverted = false;
        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            ocrBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            isConverted = true;
        }

        try
        {
            var result = await engine.RecognizeAsync(ocrBitmap);
            var lines = result.Lines.Select(line => OcrTextFormatter.NormalizeFilePaths(OcrTextFormatter.JoinWords(
                line.Words.Select(w => w.Text).ToList())));
            return OcrTextFormatter.JoinLines(lines);
        }
        finally
        {
            if (isConverted)
            {
                ocrBitmap.Dispose();
            }
        }
    }
}
