using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HasamiBoard.Converters;

/// <summary>ConverterParameter="Invert" で真偽を反転して評価する。</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>bool値を Visibility に変換する (Invert指定時は反転)。</summary>
    /// <param name="value">変換対象のbool値</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">変換パラメータ (Invert指定時は反転)</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>Visibility型に変換された値</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
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
