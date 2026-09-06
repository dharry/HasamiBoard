using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HasamiBoard.Interop;

namespace HasamiBoard.Services;

/// <summary>
/// WM_CLIPBOARDUPDATE を購読し、クリップボードのテキスト/画像変更を通知する。
/// 自アプリが Clipboard へ書き込んだ内容は NotifySelfWrite/NotifySelfImageWrite で
/// 事前登録しておくことで、履歴への二重追加を防ぐ。
/// </summary>
public class ClipboardMonitorService : IDisposable
{
    private const int MaxTextLength = 10_000;

    // Windows の画面キャプチャ (Win+Shift+S 等) は1回のキャプチャに対して
    // クリップボードへ複数回連続で書き込むことがあるため、短時間の
    // WM_CLIPBOARDUPDATE をデバウンスし最後の1枚のみを採用する。
    private static readonly TimeSpan ImageDebounceInterval = TimeSpan.FromMilliseconds(400);

    private HwndSource? _hwndSource;
    private string? _lastSelfWrittenText;
    private bool _suppressNextImage;
    private DispatcherTimer? _imageDebounceTimer;
    private BitmapSource? _pendingImage;

    public event Action<string>? TextCaptured;
    public event Action<BitmapSource>? ImageCaptured;

    /// <summary>指定ウィンドウにフックしてクリップボード変更通知の受信を開始する。</summary>
    /// <param name="window">フック対象のウィンドウ</param>
    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(helper.Handle);

        _imageDebounceTimer = new DispatcherTimer { Interval = ImageDebounceInterval };
        _imageDebounceTimer.Tick += (_, _) =>
        {
            _imageDebounceTimer!.Stop();
            if (_pendingImage is not null)
            {
                var image = _pendingImage;
                _pendingImage = null;
                ImageCaptured?.Invoke(image);
            }
        };
    }

    /// <summary>フックを解除し監視を停止する。</summary>
    public void Detach()
    {
        if (_imageDebounceTimer is not null)
        {
            _imageDebounceTimer.Stop();
            _imageDebounceTimer = null;
            _pendingImage = null;
        }

        if (_hwndSource is not null)
        {
            NativeMethods.RemoveClipboardFormatListener(_hwndSource.Handle);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    /// <summary>自アプリがこれからクリップボードへ書き込むテキストを事前登録する。</summary>
    /// <param name="text">事前登録するテキスト内容</param>
    public void NotifySelfWrite(string text)
    {
        _lastSelfWrittenText = text;
    }

    /// <summary>自アプリがこれからクリップボードへ画像を書き込むことを事前登録する。</summary>
    public void NotifySelfImageWrite()
    {
        _suppressNextImage = true;
    }

    /// <summary>ウィンドウメッセージを監視し、クリップボード更新を検知する。</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            if (!TryCaptureImage())
            {
                TryCaptureText();
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>クリップボードからテキストを取得し、変更イベントを発火する。</summary>
    private void TryCaptureText()
    {
        string? text = TryGetClipboardText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text == _lastSelfWrittenText)
        {
            _lastSelfWrittenText = null;
            return;
        }

        if (text.Length > MaxTextLength)
        {
            text = text[..MaxTextLength];
        }

        TextCaptured?.Invoke(text);
    }

    /// <summary>クリップボードから画像を取得し、デバウンス後にイベント発火する。</summary>
    private bool TryCaptureImage()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    return false;
                }

                if (_suppressNextImage)
                {
                    _suppressNextImage = false;
                    return true;
                }

                var image = Clipboard.GetImage();
                if (image is not null && _imageDebounceTimer is not null)
                {
                    _pendingImage = image;
                    _imageDebounceTimer.Stop();
                    _imageDebounceTimer.Start();
                }

                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                System.Threading.Thread.Sleep(30);
            }
        }

        return false;
    }

    /// <summary>クリップボードのテキストをリトライ付きで安全に取得する。</summary>
    private static string? TryGetClipboardText()
    {
        // クリップボードは他アプリに一時的にロックされていることがあるため軽くリトライする
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    return Clipboard.GetText();
                }

                return null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                System.Threading.Thread.Sleep(30);
            }
        }

        return null;
    }

    /// <summary>監視を停止しリソースを解放する。</summary>
    public void Dispose()
    {
        Detach();
    }
}
