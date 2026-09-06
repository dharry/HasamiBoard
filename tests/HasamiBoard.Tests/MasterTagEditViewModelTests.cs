using HasamiBoard.ViewModels;
using Xunit;

namespace HasamiBoard.Tests;

public class MasterTagEditViewModelTests
{
    private static MasterTagEditViewModel CreateViewModel(
        string name,
        out List<(string Old, string New)> renameCalls,
        out List<string> deleteCalls)
    {
        var renames = new List<(string, string)>();
        var deletes = new List<string>();
        renameCalls = renames;
        deleteCalls = deletes;

        return new MasterTagEditViewModel(
            name,
            "#E53935",
            onColorChanged: (_, _) => { },
            onDelete: n => deletes.Add(n),
            onRename: (oldName, newName) => renames.Add((oldName, newName)));
    }

    [Fact]
    public void CommitRename_ReenteredViaLostFocus_InvokesOnRenameExactlyOnce()
    {
        var tag = CreateViewModel("重要", out var renameCalls, out _);

        tag.BeginRenameCommand.Execute(null);
        tag.EditingName = "重要度高";

        // WPF collapses the edit TextBox when IsRenaming flips to false, which
        // synchronously fires LostFocus and re-invokes CommitRenameCommand
        // before the outer call returns. Simulate that reentrancy here.
        tag.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MasterTagEditViewModel.IsRenaming) && !tag.IsRenaming)
            {
                tag.CommitRenameCommand.Execute(null);
            }
        };

        tag.CommitRenameCommand.Execute(null);

        Assert.Single(renameCalls);
        Assert.Equal(("重要", "重要度高"), renameCalls[0]);
        Assert.False(tag.IsRenaming);
    }

    [Fact]
    public void CancelRename_ReenteredViaLostFocus_DoesNotCommitEditedName()
    {
        var tag = CreateViewModel("重要", out var renameCalls, out _);

        tag.BeginRenameCommand.Execute(null);
        tag.EditingName = "誤入力";

        tag.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MasterTagEditViewModel.IsRenaming) && !tag.IsRenaming)
            {
                tag.CommitRenameCommand.Execute(null);
            }
        };

        tag.CancelRenameCommand.Execute(null);

        Assert.Empty(renameCalls);
        Assert.False(tag.IsRenaming);
        Assert.Equal("重要", tag.Name);
    }

    [Fact]
    public void CommitRename_NoActualChange_DoesNotInvokeOnRename()
    {
        var tag = CreateViewModel("重要", out var renameCalls, out _);

        tag.BeginRenameCommand.Execute(null);
        tag.EditingName = "重要";
        tag.CommitRenameCommand.Execute(null);

        Assert.Empty(renameCalls);
    }
}
