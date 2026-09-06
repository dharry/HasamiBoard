using System.Windows;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class WindowPlacementHelperTests
{
    // System.Windows.Window はSTAスレッドでのみ生成できるため、xUnitの既定(MTA)スレッドを
    // 避けて専用のSTAスレッド上でテスト本体を実行する。
    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }

    [Fact]
    public void Restore_WithNoSavedPosition_LeavesStartupLocationUntouched() => RunOnSta(() =>
    {
        var window = new Window { Left = 11, Top = 22, Width = 300, Height = 200 };

        WindowPlacementHelper.Restore(window, savedLeft: null, savedTop: null, savedWidth: 0, savedHeight: 0);

        Assert.Equal(11, window.Left);
        Assert.Equal(22, window.Top);
        Assert.Equal(300, window.Width);
        Assert.Equal(200, window.Height);
    });

    [Fact]
    public void Restore_WithSavedSizeOnly_AppliesWidthAndHeight() => RunOnSta(() =>
    {
        var window = new Window { Width = 300, Height = 200 };

        WindowPlacementHelper.Restore(window, savedLeft: null, savedTop: null, savedWidth: 640, savedHeight: 480);

        Assert.Equal(640, window.Width);
        Assert.Equal(480, window.Height);
    });

    [Fact]
    public void Restore_WithOnScreenPosition_AppliesLeftAndTop() => RunOnSta(() =>
    {
        var window = new Window { Width = 300, Height = 200 };

        double left = SystemParameters.VirtualScreenLeft + 10;
        double top = SystemParameters.VirtualScreenTop + 10;

        WindowPlacementHelper.Restore(window, left, top, savedWidth: 0, savedHeight: 0);

        Assert.Equal(left, window.Left);
        Assert.Equal(top, window.Top);
        Assert.Equal(WindowStartupLocation.Manual, window.WindowStartupLocation);
    });

    [Fact]
    public void Restore_WithOffScreenPosition_DoesNotMoveWindow() => RunOnSta(() =>
    {
        var window = new Window { Left = 5, Top = 5, Width = 300, Height = 200 };

        WindowPlacementHelper.Restore(window, savedLeft: -999999, savedTop: -999999, savedWidth: 0, savedHeight: 0);

        Assert.Equal(5, window.Left);
        Assert.Equal(5, window.Top);
    });

    [Fact]
    public void SaveIfNormal_WhenNormal_InvokesCallbackWithCurrentGeometry() => RunOnSta(() =>
    {
        var window = new Window { Left = 1, Top = 2, Width = 3, Height = 4, WindowState = WindowState.Normal };
        (double, double, double, double)? captured = null;

        WindowPlacementHelper.SaveIfNormal(window, (l, t, w, h) => captured = (l, t, w, h));

        Assert.Equal((1d, 2d, 3d, 4d), captured);
    });

    [Fact]
    public void SaveIfNormal_WhenMaximized_DoesNotInvokeCallback() => RunOnSta(() =>
    {
        var window = new Window { WindowState = WindowState.Maximized };
        bool called = false;

        WindowPlacementHelper.SaveIfNormal(window, (_, _, _, _) => called = true);

        Assert.False(called);
    });
}
