using Microsoft.Win32;

namespace HasamiBoard.Services;

/// <summary>Windows スタートアップ自動起動レジストリ (HKCU\...\Run) を管理する。</summary>
public class StartupRegistryService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HasamiBoard";

    /// <summary>スタートアップ自動起動が有効かどうかを確認する。</summary>
    /// <returns>自動起動が有効な場合true</returns>
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    /// <summary>スタートアップ自動起動レジストリ値を設定または削除する。</summary>
    /// <param name="enabled">有効化する場合true</param>
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                // /hide: 自動起動時はGUIを表示せずタスクトレイに常駐した状態で起動する
                key.SetValue(ValueName, $"\"{exePath}\" /hide");
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
