using System.Windows.Media;

namespace HasamiBoard.Services;

/// <summary>設定パネル等のフォント選択コンボボックスに表示する、システムにインストール済みのフォント一覧。</summary>
public static class FontCatalog
{
    public static IReadOnlyList<string> FontFamilies { get; } = Fonts.SystemFontFamilies
        .Select(f => f.Source)
        .Distinct()
        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
        .ToList();
}
