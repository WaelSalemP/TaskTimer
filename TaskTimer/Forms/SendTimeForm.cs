using TaskTimer.Models;
using TaskTimer.Services;

namespace TaskTimer.Forms;

/// <summary>Confirms sending time to the server and lets the user set the task's status in the same update.</summary>
public sealed class SendTimeForm : Form
{
    private readonly WorkItem _item;
    private readonly ComboBox _state = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly CheckBox _reduceRemaining = new() { AutoSize = true, Margin = new Padding(3, 0, 3, 12) };

    /// <summary>Whether to lower the remaining work by the hours being sent, or leave it untouched.</summary>
    public bool ReduceRemaining => _reduceRemaining.Checked;

    /// <summary>The status to set, or null to leave it as it is.</summary>
    public string? NewState =>
        _state.SelectedItem is string state && !string.Equals(state, _item.State, StringComparison.OrdinalIgnoreCase)
            ? state
            : null;

    /// <param name="reduceRemaining">Initial state of the remaining-work box (the default from the settings).</param>
    /// <param name="hasRemainingWork">False for providers without a remaining-work field; hides the box.</param>
    public SendTimeForm(WorkItem item, double hours, IReadOnlyList<string> states, bool reduceRemaining, bool hasRemainingWork)
    {
        _item = item;

        Icon = Program.AppIcon;
        Text = Loc.T("Push");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14), Dock = DockStyle.Fill
        };

        var message = new Label
        {
            AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(3, 3, 3, 14),
            Text = Loc.T("ConfirmPush", hours, item.Title)
        };

        // The current status is always offered, even if the server no longer lists it for this type.
        var options = states.ToList();
        if (item.State.Length > 0 && !options.Contains(item.State, StringComparer.OrdinalIgnoreCase))
            options.Insert(0, item.State);
        _state.Items.AddRange(options.Cast<object>().ToArray());
        _state.SelectedItem = options.FirstOrDefault(s => string.Equals(s, item.State, StringComparison.OrdinalIgnoreCase));

        var statePanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
        statePanel.Controls.Add(new Label { Text = Loc.T("State"), AutoSize = true, Margin = new Padding(3, 7, 8, 0) });
        statePanel.Controls.Add(_state);

        var send = new Button { Text = Loc.T("Send"), AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(12, 3, 12, 3) };
        var cancel = new Button { Text = Loc.T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
        Theme.StyleButton(send, Theme.Primary);
        Theme.StyleButton(cancel, Theme.Neutral);
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right };
        buttons.Controls.Add(send);
        buttons.Controls.Add(cancel);

        _reduceRemaining.Text = Loc.T("ReduceRemainingNow", hours);
        _reduceRemaining.Checked = reduceRemaining && hasRemainingWork;
        _reduceRemaining.Visible = hasRemainingWork;

        layout.Controls.AddRange(new Control[] { message, statePanel, _reduceRemaining, buttons });
        Controls.Add(layout);
        AcceptButton = send;
        CancelButton = cancel;
    }
}
