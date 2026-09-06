using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HasamiBoard.Converters;

/// <summary>文字列が空でない場合に Visible を返す。ConverterParameter="Invert" で反転。</summary>
public class StringToVisibilityConverter : IValueConverter
{
    /// <summary>文字列の有無を Visibility に変換する (Invert指定時は反転)。</summary>
    /// <param name="value">判定対象の文字列</param>
    /// <param name="targetType">変換先の型</param>
    /// <param name="parameter">変換パラメータ (Invert指定時は反転)</param>
    /// <param name="culture">カルチャ情報</param>
    /// <returns>文字列有無に応じてVisibleまたはCollapsed</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasText = value is string s && !string.IsNullOrEmpty(s);
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            hasText = !hasText;
        }

        return hasText ? Visibility.Visible : Visibility.Collapsed;
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
