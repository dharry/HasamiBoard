using System.Globalization;
using System.Windows.Data;

namespace HasamiBoard.Converters;

/// <summary>整数のインデックス値がパラメータと一致するか判定する。</summary>
public class IndexToBoolConverter : IValueConverter
{
    /// <summary>インデックス値がパラメータと一致すれば true を返す。</summary>
    /// <param name="value">判定対象のインデックス値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">比較対象のインデックス値</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>一致すればtrue、否則false</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int current && parameter is not null && int.TryParse(parameter.ToString(), out int target))
        {
            return current == target;
        }

        return false;
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
