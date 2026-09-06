using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HasamiBoard.Services;

namespace HasamiBoard.Views;

/// <summary>画面全体を覆い、マウス位置のピクセル色を拡大表示するカラーピッカーオーバーレイ</summary>
public partial class ColorPickerOverlayWindow : Window
{
    private string? _lastHex;

    /// <summary>確定時はHEXコード、キャンセル時は null を受け取る。</summary>
    public event Action<string?>? Completed;

    /// <summary>オーバーレイを仮想画面全体のサイズで初期化する</summary>
    public ColorPickerOverlayWindow()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    /// <summary>
    /// 読み込み時にウィンドウをアクティブ化しフォーカスする。マウスを1度も動かさずに
    /// Enter/Space で確定された場合でも色を取得できるよう、現在のカーソル位置で
    /// 初回のピクセル読み取りも行っておく。
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        UpdateColorAtPosition(Mouse.GetPosition(this));
    }

    /// <summary>マウス移動に追従してピクセル色とルーペ表示を更新する</summary>
    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        UpdateColorAtPosition(e.GetPosition(this));
    }

    /// <summary>指定した論理座標のピクセル色とルーペ表示を取得・更新する</summary>
    private void UpdateColorAtPosition(Point logicalPos)
    {
        var screenPoint = PointToScreen(logicalPos);
        int sx = (int)screenPoint.X;
        int sy = (int)screenPoint.Y;

        try
        {
            string hex = ScreenPixelReader.GetPixelColorHex(sx, sy);
            var magnified = ScreenPixelReader.CaptureRegionAroundPoint(sx, sy, 15);
            MagnifierImage.Source = magnified;
            HexText.Text = hex;
            RgbText.Text = HexToRgbLabel(hex);
            ColorSwatch.Background = (Brush)new BrushConverter().ConvertFromString(hex)!;
            _lastHex = hex;
        }
        catch
        {
            // 画面端など取得失敗時は前回値を維持
        }

        PositionLoupe(logicalPos);
    }

    /// <summary>画面端にはみ出さないようルーペパネルの表示位置を計算する</summary>
    private void PositionLoupe(Point cursorLogicalPos)
    {
        const double panelWidth = 170;
        const double panelHeight = 190;
        const double offset = 24;

        double left = cursorLogicalPos.X + offset;
        double top = cursorLogicalPos.Y + offset;

        if (left + panelWidth > ActualWidth)
        {
            left = cursorLogicalPos.X - panelWidth - offset;
        }

        if (top + panelHeight > ActualHeight)
        {
            top = cursorLogicalPos.Y - panelHeight - offset;
        }

        Canvas.SetLeft(LoupePanel, Math.Max(0, left));
        Canvas.SetTop(LoupePanel, Math.Max(0, top));
    }

    /// <summary>左クリックで色を確定する</summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Confirm();

    /// <summary>右クリックで選択をキャンセルする</summary>
    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e) => Cancel();

    /// <summary>Enter/Spaceで確定、Escapeでキャンセルする</summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Confirm();
        }
        else if (e.Key == Key.Escape)
        {
            Cancel();
        }
    }

    /// <summary>選択中の色で確定しウィンドウを閉じる</summary>
    private void Confirm()
    {
        Completed?.Invoke(_lastHex);
        Close();
    }

    /// <summary>選択をキャンセルしてウィンドウを閉じる</summary>
    private void Cancel()
    {
        Completed?.Invoke(null);
        Close();
    }

    /// <summary>HEXカラーコードをRGB表記の文字列に変換する</summary>
    private static string HexToRgbLabel(string hex)
    {
        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return $"RGB({r}, {g}, {b})";
    }
}
