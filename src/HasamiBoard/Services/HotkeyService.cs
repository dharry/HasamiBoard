using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HasamiBoard.Interop;

namespace HasamiBoard.Services;

/// <summary>
/// システム全体のグローバルホットキーを RegisterHotKey で管理する。
/// 設定文字列 (例: "Ctrl+Shift", "S") からホットキーを登録し、押下時にコールバックを呼び出す。
/// </summary>
public class HotkeyService : IDisposable
{
    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_SHIFT = 0x4;
    private const uint MOD_WIN = 0x8;

    private HwndSource? _hwndSource;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 0xB100;

    /// <summary>指定ウィンドウにフックしてホットキー受信を開始する。</summary>
    /// <param name="window">フック対象のウィンドウ</param>
    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);
    }

    /// <summary>登録済みホットキーをすべて解除しフックを外す。</summary>
    public void Detach()
    {
        if (_hwndSource is null)
        {
            return;
        }

        foreach (int id in _handlers.Keys.ToList())
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, id);
        }

        _handlers.Clear();
        _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;
    }

    /// <summary>修飾キーとキー、押下時コールバックを指定してグローバルホットキーを登録する。</summary>
    /// <param name="modifiers">修飾キー文字列（例："Ctrl+Shift"）</param>
    /// <param name="key">キー名</param>
    /// <param name="callback">ホットキー押下時のコールバック</param>
    /// <returns>登録ID (失敗時は -1)。同じ用途で再登録する場合は先に Unregister すること。</returns>
    public int Register(string modifiers, string key, Action callback)
    {
        if (_hwndSource is null)
        {
            return -1;
        }

        uint modFlags = ParseModifiers(modifiers);
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var wpfKey))
        {
            return -1;
        }

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
        int id = _nextId++;

        if (!NativeMethods.RegisterHotKey(_hwndSource.Handle, id, modFlags, vk))
        {
            return -1;
        }

        _handlers[id] = callback;
        return id;
    }

    /// <summary>指定IDのホットキー登録を解除する。</summary>
    /// <param name="id">解除対象のホットキー登録ID</param>
    public void Unregister(int id)
    {
        if (_hwndSource is null || !_handlers.ContainsKey(id))
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_hwndSource.Handle, id);
        _handlers.Remove(id);
    }

    /// <summary>ウィンドウメッセージを監視し、ホットキー押下を検知してコールバックを呼び出す。</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var callback))
            {
                callback();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>"Ctrl+Shift" 等の修飾キー文字列をWin32の修飾キーフラグへ変換する。</summary>
    private static uint ParseModifiers(string modifiers)
    {
        uint flags = 0;
        foreach (string part in modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            flags |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => MOD_CONTROL,
                "alt" => MOD_ALT,
                "shift" => MOD_SHIFT,
                "win" or "windows" => MOD_WIN,
                _ => 0u,
            };
        }

        return flags;
    }

    /// <summary>登録済みホットキーを解除しリソースを解放する。</summary>
    public void Dispose()
    {
        Detach();
    }
}
