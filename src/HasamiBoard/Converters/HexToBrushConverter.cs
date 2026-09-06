using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HasamiBoard.Converters;

/// <summary>HEXカラー文字列を SolidColorBrush に変換する。</summary>
public class HexToBrushConverter : IValueConverter
{
    /// <summary>HEXカラー文字列を SolidColorBrush に変換する。失敗時は灰色を返す。</summary>
    /// <param name="value">HEXカラー文字列</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">変換パラメータ</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>SolidColorBrush、解析失敗時は灰色ブラシ</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                // fall through
            }
        }

        return Brushes.Gray;
    }

    /// <summary>逆変換は未サポート。</summary>
    /// <param name="value">変換対象の値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">変換パラメータ</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>未サポート (NotSupportedExceptionをスロー)</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
