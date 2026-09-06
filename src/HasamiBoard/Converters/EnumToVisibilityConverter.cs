using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HasamiBoard.Converters;

/// <summary>ConverterParameter にカンマ区切りで列挙値名を指定し、いずれかに一致すれば Visible にする。</summary>
public class EnumToVisibilityConverter : IValueConverter
{
    /// <summary>列挙値がパラメータ候補群のいずれかに一致するか判定する。</summary>
    /// <param name="value">判定対象の列挙値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">カンマ区切りの候補値一覧</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>一致すればVisible、否則Collapsed</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string parameterText)
        {
            return Visibility.Collapsed;
        }

        string current = value.ToString() ?? string.Empty;
        var candidates = parameterText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return candidates.Any(c => string.Equals(c, current, StringComparison.OrdinalIgnoreCase))
            ? Visibility.Visible
            : Visibility.Collapsed;
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
