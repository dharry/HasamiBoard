using System.IO;
using System.Text.Json;
using HasamiBoard.Models;

namespace HasamiBoard.Services;

/// <summary>アプリ設定 (Settings.json) の読み込み・保存・初期化を担う。</summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string AppDataFolder { get; }
    public string SettingsFilePath { get; }

    public AppSettings Current { get; private set; }

    /// <summary>データフォルダーを確保し、設定ファイルを読み込む。</summary>
    /// <param name="dataFolder">データフォルダパス（nullの場合は既定値を使用）</param>
    public SettingsService(string? dataFolder = null)
    {
        AppDataFolder = dataFolder ?? AppPaths.DataFolder;
        Directory.CreateDirectory(AppDataFolder);
        SettingsFilePath = Path.Combine(AppDataFolder, "Settings.json");
        Current = Load();
    }

    /// <summary>設定ファイルから設定を読み込む。存在しない/失敗時は既定値を返す。</summary>
    private AppSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            MigrateLegacyConverterModeNames(settings);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>開発ツールの列挙体リネーム (例: <c>ConverterMode.Guid</c> → <c>ConverterMode.Uuid</c>) に伴い、
    /// 旧バージョンで保存された設定ファイル内の旧モード名を現行名へ読み替える。
    /// これを行わないと、旧モード名を含む設定 (選択中モード/表示順/非表示設定) が新しい名前と一致せず、
    /// ユーザーの設定が黙って失われてしまう。</summary>
    private static void MigrateLegacyConverterModeNames(AppSettings settings)
    {
        const string LegacyGuidModeName = "Guid";
        const string CurrentUuidModeName = "Uuid";

        if (settings.ConverterSelectedMode == LegacyGuidModeName)
        {
            settings.ConverterSelectedMode = CurrentUuidModeName;
        }

        RenameLegacyModeEntry(settings.ConverterDisabledModes, LegacyGuidModeName, CurrentUuidModeName);
        RenameLegacyModeEntry(settings.ConverterModeOrder, LegacyGuidModeName, CurrentUuidModeName);
    }

    /// <summary>モード名一覧内の旧名を新名へ置き換える。新名が既に存在する場合は、重複を避けるため旧名の方を削除する。</summary>
    private static void RenameLegacyModeEntry(List<string> modeNames, string legacyName, string currentName)
    {
        int index = modeNames.IndexOf(legacyName);
        if (index < 0)
        {
            return;
        }

        if (modeNames.Contains(currentName))
        {
            modeNames.RemoveAt(index);
        }
        else
        {
            modeNames[index] = currentName;
        }
    }

    /// <summary>現在の設定をJSONファイルへ保存する。</summary>
    public void Save()
    {
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    /// <summary>設定を既定値にリセットして保存する。</summary>
    public void ResetToDefault()
    {
        Current = new AppSettings();
        Save();
    }
}
