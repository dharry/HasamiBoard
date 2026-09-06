using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HasamiBoard.Converters;

/// <summary>整数のインデックス値がパラメータと一致すれば表示にする。</summary>
public class IndexToVisibilityConverter : IValueConverter
{
    /// <summary>インデックス値がパラメータと一致すれば Visible を返す。</summary>
    /// <param name="value">判定対象のインデックス値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">比較対象のインデックス値</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>一致すればVisible、否則Collapsed</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int current && parameter is not null && int.TryParse(parameter.ToString(), out int target))
        {
            return current == target ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
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
