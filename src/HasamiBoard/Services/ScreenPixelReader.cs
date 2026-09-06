using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingSize = System.Drawing.Size;

namespace HasamiBoard.Services;

/// <summary>GDI経由でスクリーンのピクセル/矩形領域をキャプチャする (カラーピッカー用)。</summary>
public static class ScreenPixelReader
{
    /// <summary>指定座標のピクセル色をHEX文字列で取得する。</summary>
    /// <param name="screenX">画面X座標</param>
    /// <param name="screenY">画面Y座標</param>
    /// <returns>カラーコード（HEX形式）</returns>
    public static string GetPixelColorHex(int screenX, int screenY)
    {
        using var bitmap = new DrawingBitmap(1, 1);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.CopyFromScreen(screenX, screenY, 0, 0, new DrawingSize(1, 1));
        var pixel = bitmap.GetPixel(0, 0);
        return $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";
    }

    /// <summary>指定中心座標を囲む正方形領域をキャプチャし、WPFのBitmapSourceとして返す (ルーペ用)。</summary>
    /// <param name="centerX">中心X座標</param>
    /// <param name="centerY">中心Y座標</param>
    /// <param name="size">キャプチャ領域のサイズ（ピクセル）</param>
    /// <returns>キャプチャされたBitmapSource</returns>
    public static BitmapSource CaptureRegionAroundPoint(int centerX, int centerY, int size)
    {
        int half = size / 2;
        int left = centerX - half;
        int top = centerY - half;

        using var bitmap = new DrawingBitmap(size, size);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.CopyFromScreen(left, top, 0, 0, new DrawingSize(size, size));

        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeDeleteObject(hBitmap);
        }
    }

    /// <summary>GDIオブジェクトハンドルを解放するWin32 API。</summary>
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>GDIビットマップハンドルを解放する。</summary>
    private static void NativeDeleteObject(IntPtr hObject) => DeleteObject(hObject);
}
