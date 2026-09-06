using System.Windows;
using HasamiBoard.Interop;

namespace HasamiBoard.Services;

/// <summary>ダーク/ライトテーマのリソース辞書切り替えとタイトルバー連動を行う。</summary>
public class ThemeService
{
    private const string ThemeDictionaryTag = "ThemeDictionary";

    /// <summary>指定テーマのリソース辞書を適用し、各ウィンドウのタイトルバー色も切り替える。</summary>
    /// <param name="isDarkMode">ダークモード利用フラグ</param>
    public void ApplyTheme(bool isDarkMode)
    {
        var app = Application.Current;
        var uri = isDarkMode
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var newDictionary = new ResourceDictionary { Source = uri };

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => Equals(d["__ThemeMarker"], ThemeDictionaryTag));

        newDictionary["__ThemeMarker"] = ThemeDictionaryTag;

        if (existing is not null)
        {
            int index = app.Resources.MergedDictionaries.IndexOf(existing);
            app.Resources.MergedDictionaries[index] = newDictionary;
        }
        else
        {
            app.Resources.MergedDictionaries.Add(newDictionary);
        }

        foreach (Window window in app.Windows)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            NativeMethods.SetTitleBarTheme(hwnd, isDarkMode);
        }
    }
}
