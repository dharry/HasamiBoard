using System.IO;
using System.Reflection;
using System.Text;

namespace HasamiBoard.Services;

/// <summary>アセンブリに埋め込まれたテキストリソース (CSS等) を読み込む共通処理。</summary>
internal static class EmbeddedResourceLoader
{
    /// <summary>指定したファイル名末尾に一致する埋め込みリソースをテキストとして読み込む。</summary>
    public static string LoadText(string fileNameSuffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(fileNameSuffix, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
