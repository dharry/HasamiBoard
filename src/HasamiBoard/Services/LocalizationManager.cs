using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace HasamiBoard.Services;

/// <summary>
/// リソース文字列を実行時に切替可能にするための共有インデクサー。
/// XAMLからは {Binding Source={x:Static services:LocalizationManager.Instance}, Path=[Key]} で参照する。
/// 言語切替時に Item[] の PropertyChanged を発火させることで全バインディングを再評価させる。
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private readonly ResourceManager _resourceManager =
        new("HasamiBoard.Properties.Resources", typeof(LocalizationManager).Assembly);

    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>シングルトンとしてのみ生成させるための非公開コンストラクタ。</summary>
    private LocalizationManager()
    {
    }

    public string this[string key] => _resourceManager.GetString(key, _currentCulture) ?? key;

    /// <summary>言語設定を切り替え、バインディング全体の再評価を通知する。</summary>
    /// <param name="languageSetting">"auto" / "ja" / "en"</param>
    public void ApplyLanguage(string languageSetting)
    {
        _currentCulture = languageSetting switch
        {
            "ja" => new CultureInfo("ja-JP"),
            "en" => new CultureInfo("en-US"),
            _ => CultureInfo.InstalledUICulture,
        };

        CultureInfo.CurrentUICulture = _currentCulture;
        CultureInfo.CurrentCulture = _currentCulture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
