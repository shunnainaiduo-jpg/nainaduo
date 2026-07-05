using System.Windows.Forms;

namespace CadLibraryManager;

internal static class PromptText
{
    public static string? ShowDialog(string prompt, string title, string defaultValue)
    {
        using var form = new Form
        {
            Text = title,
            Width = 420,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };
        var label = new Label { Text = prompt, Left = 12, Top = 12, Width = 380 };
        var input = new TextBox { Text = defaultValue, Left = 12, Top = 38, Width = 380 };
        var okButton = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 220, Width = 80, Top = 72 };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 306, Width = 80, Top = 72 };

        form.Controls.Add(label);
        form.Controls.Add(input);
        form.Controls.Add(okButton);
        form.Controls.Add(cancelButton);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        return form.ShowDialog() == DialogResult.OK ? input.Text : null;
    }
}
