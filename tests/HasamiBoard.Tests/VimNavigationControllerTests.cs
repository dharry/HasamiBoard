using System.IO;
using System.Windows.Input;
using HasamiBoard.Services;
using Xunit;

namespace HasamiBoard.Tests;

public class VimNavigationControllerTests
{
    private static VimNavigationController CreateController(out Dictionary<string, int> callCounts)
    {
        var counts = new Dictionary<string, int>
        {
            ["down"] = 0,
            ["up"] = 0,
            ["top"] = 0,
            ["bottom"] = 0,
            ["delete"] = 0,
            ["yank"] = 0,
            ["edit"] = 0,
        };

        var controller = new VimNavigationController
        {
            MoveDown = () => counts["down"]++,
            MoveUp = () => counts["up"]++,
            MoveToTop = () => counts["top"]++,
            MoveToBottom = () => counts["bottom"]++,
            DeleteSelected = () => counts["delete"]++,
            YankSelected = () => counts["yank"]++,
            EnterEdit = () => counts["edit"]++,
        };

        callCounts = counts;
        return controller;
    }

    [Fact]
    public void J_MovesDownImmediately()
    {
        var controller = CreateController(out var counts);
        Assert.True(controller.HandleKey(Key.J, ModifierKeys.None));
        Assert.Equal(1, counts["down"]);
    }

    [Fact]
    public void K_MovesUpImmediately()
    {
        var controller = CreateController(out var counts);
        Assert.True(controller.HandleKey(Key.K, ModifierKeys.None));
        Assert.Equal(1, counts["up"]);
    }

    [Fact]
    public void I_And_A_EnterEditImmediately()
    {
        var controller = CreateController(out var counts);
        controller.HandleKey(Key.I, ModifierKeys.None);
        controller.HandleKey(Key.A, ModifierKeys.None);
        Assert.Equal(2, counts["edit"]);
    }

    [Fact]
    public void ShiftG_MovesToBottomImmediately_NoDoublePressNeeded()
    {
        var controller = CreateController(out var counts);
        controller.HandleKey(Key.G, ModifierKeys.Shift);
        Assert.Equal(1, counts["bottom"]);
        Assert.Equal(0, counts["top"]);
    }

    [Fact]
    public void LowercaseG_RequiresDoublePress_ToMoveToTop()
    {
        var controller = CreateController(out var counts);

        controller.HandleKey(Key.G, ModifierKeys.None);
        Assert.Equal(0, counts["top"]);

        controller.HandleKey(Key.G, ModifierKeys.None);
        Assert.Equal(1, counts["top"]);
    }

    [Fact]
    public void SingleLowercaseG_DoesNotTriggerTopAlone()
    {
        var controller = CreateController(out var counts);
        controller.HandleKey(Key.G, ModifierKeys.None);
        controller.HandleKey(Key.J, ModifierKeys.None);
        Assert.Equal(0, counts["top"]);
        Assert.Equal(1, counts["down"]);
    }

    [Fact]
    public void ShiftD_DeletesImmediately()
    {
        var controller = CreateController(out var counts);
        controller.HandleKey(Key.D, ModifierKeys.Shift);
        Assert.Equal(1, counts["delete"]);
    }

    [Fact]
    public void LowercaseD_RequiresDoublePress_ToDelete()
    {
        var controller = CreateController(out var counts);
        controller.HandleKey(Key.D, ModifierKeys.None);
        controller.HandleKey(Key.D, ModifierKeys.None);
        Assert.Equal(1, counts["delete"]);
    }

    [Fact]
    public void ShiftY_And_LowercaseYY_AndCtrlC_AllYank()
    {
        var controller = CreateController(out var counts);

        controller.HandleKey(Key.Y, ModifierKeys.Shift);
        Assert.Equal(1, counts["yank"]);

        controller.HandleKey(Key.Y, ModifierKeys.None);
        controller.HandleKey(Key.Y, ModifierKeys.None);
        Assert.Equal(2, counts["yank"]);

        controller.HandleKey(Key.C, ModifierKeys.Control);
        Assert.Equal(3, counts["yank"]);
    }

    [Fact]
    public void PendingDoubleKey_ExpiresAfterTimeout()
    {
        var controller = CreateController(out var counts);

        controller.HandleKey(Key.D, ModifierKeys.None);
        Thread.Sleep(700);
        controller.HandleKey(Key.D, ModifierKeys.None);

        Assert.Equal(0, counts["delete"]);
    }

    [Fact]
    public void UnhandledKey_ReturnsFalse()
    {
        var controller = CreateController(out _);
        Assert.False(controller.HandleKey(Key.Z, ModifierKeys.None));
    }
}
