using System.Globalization;
using System.Windows.Data;

namespace HasamiBoard.Converters;

/// <summary>ConverterParameter に列挙値名を指定し、一致すれば true を返す (RadioButtonのIsChecked等に使用)。</summary>
public class EnumToBoolConverter : IValueConverter
{
    /// <summary>列挙値がパラメータ文字列と一致するか判定する。</summary>
    /// <param name="value">判定対象の列挙値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">比較対象の列挙値名</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>一致すればtrue、否則false</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string parameterText)
        {
            return false;
        }

        return string.Equals(value.ToString(), parameterText, StringComparison.OrdinalIgnoreCase);
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
