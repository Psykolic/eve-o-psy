using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.View;
using Xunit;

namespace EveO.Tests.Integration;

[Collection("WindowsIntegration")]
public sealed class CycleGroupDialogTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Edit_ShowsModalDialogInOwnersTopMostBand(bool topMost)
    {
        using var owner = new Form { TopMost = topMost, ShowInTaskbar = false };
        owner.Show();
        var model = new CycleGroupEditModel(1, "", "", Array.Empty<CycleGroupClient>(),
            Array.Empty<string>(), null, null);
        bool? visibleTopMost = null;

        owner.BeginInvoke(new Action(() =>
        {
            var dialog = Application.OpenForms.OfType<CycleGroupDialog>().SingleOrDefault();
            if (dialog == null)
            {
                owner.Close();
                return;
            }

            if (dialog.Visible && dialog.Modal)
            {
                visibleTopMost = dialog.TopMost;
            }
            dialog.DialogResult = DialogResult.Cancel;
        }));

        Assert.Null(CycleGroupDialog.Edit(owner, model));
        Assert.Equal((bool?)topMost, visibleTopMost);
    }
}
