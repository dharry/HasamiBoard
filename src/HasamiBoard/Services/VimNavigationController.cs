using System.Windows.Input;

namespace HasamiBoard.Services;

/// <summary>
/// クリップボード履歴/スクリーンショット/メモの各タブで共通利用するVim風キーバインド処理。
/// j/k/gg/G/dd/D/yy/Y/Ctrl+C/i/a を解釈し、対応するコールバックを呼び出す。
/// </summary>
public class VimNavigationController
{
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromMilliseconds(600);

    private Key? _pendingKey;
    private DateTime _pendingKeyAt;

    public Action? MoveDown { get; set; }
    public Action? MoveUp { get; set; }
    public Action? MoveToTop { get; set; }
    public Action? MoveToBottom { get; set; }
    public Action? DeleteSelected { get; set; }
    public Action? YankSelected { get; set; }
    public Action? EnterEdit { get; set; }

    /// <summary>Vim風キー入力を解釈し、対応する移動/削除/コピー等の操作を実行する。</summary>
    /// <param name="key">入力されたキー</param>
    /// <param name="modifiers">入力されたキーの修飾キー</param>
    /// <returns>キーを処理した場合 true (呼び出し側は e.Handled = true とすること)</returns>
    public bool HandleKey(Key key, ModifierKeys modifiers)
    {
        bool ctrl = modifiers.HasFlag(ModifierKeys.Control);
        bool shift = modifiers.HasFlag(ModifierKeys.Shift);

        if (ctrl && !shift && key == Key.C)
        {
            YankSelected?.Invoke();
            return true;
        }

        // 修飾キー付きはVim風単発コマンド以外は対象外
        if (ctrl)
        {
            return false;
        }

        switch (key)
        {
            case Key.J:
                ClearPending();
                MoveDown?.Invoke();
                return true;

            case Key.K:
                ClearPending();
                MoveUp?.Invoke();
                return true;

            case Key.I:
            case Key.A:
                ClearPending();
                EnterEdit?.Invoke();
                return true;

            case Key.G:
                if (shift)
                {
                    ClearPending();
                    MoveToBottom?.Invoke();
                    return true;
                }

                return HandleTwoKeySequence(Key.G, MoveToTop);

            case Key.D:
                if (shift)
                {
                    ClearPending();
                    DeleteSelected?.Invoke();
                    return true;
                }

                return HandleTwoKeySequence(Key.D, DeleteSelected);

            case Key.Y:
                if (shift)
                {
                    ClearPending();
                    YankSelected?.Invoke();
                    return true;
                }

                return HandleTwoKeySequence(Key.Y, YankSelected);

            default:
                ClearPending();
                return false;
        }
    }

    /// <summary>gg/dd/yy のような2連続キー入力を判定し、成立時にアクションを実行する。</summary>
    private bool HandleTwoKeySequence(Key key, Action? action)
    {
        var now = DateTime.UtcNow;
        if (_pendingKey == key && now - _pendingKeyAt <= PendingTimeout)
        {
            ClearPending();
            action?.Invoke();
            return true;
        }

        _pendingKey = key;
        _pendingKeyAt = now;
        return true;
    }

    /// <summary>2連続キー入力の保留状態をクリアする。</summary>
    private void ClearPending()
    {
        _pendingKey = null;
    }
}
