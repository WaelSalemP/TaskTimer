using TaskTimer.Models;
using TaskTimer.Services;

namespace TaskTimer.Forms;

/// <summary>
/// Asks where call/meeting time should go. Shown when a call starts (the user may defer the
/// decision) and again when it ends (the user may discard the time).
/// </summary>
public sealed class AssignTimeForm : Form
{
    private readonly RadioButton _call = new() { AutoSize = true };
    private readonly RadioButton _meeting = new() { AutoSize = true };
    private readonly RadioButton _toCurrent = new() { AutoSize = true };
    private readonly RadioButton _toOther = new() { AutoSize = true };
    private readonly RadioButton _noTarget = new() { AutoSize = true };
    private readonly ComboBox _other = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 460 };
    private readonly WorkItem? _current;

    public EntryKind Kind => _meeting.Checked ? EntryKind.Meeting : EntryKind.Call;

    /// <summary>Chosen task, or null for "ask me later" (start) / "do not record" (end).</summary>
    public WorkItem? Target =>
        _toCurrent.Checked ? _current : _toOther.Checked ? _other.SelectedItem as WorkItem : null;

    public AssignTimeForm(bool ended, TimeSpan duration, IReadOnlyList<WorkItem> items, WorkItem? current,
        EntryKind kind, WorkItem? preselected)
    {
        _current = current;

        Text = Loc.T(ended ? "CallEndedTitle" : "CallStartedTitle");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false; // an explicit choice is required so time is never dropped by accident
        ShowInTaskbar = false;
        TopMost = true; // must surface above the Teams call window
        StartPosition = FormStartPosition.CenterScreen;
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
            AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(3, 3, 3, 12),
            Text = ended ? Loc.T("CallEndedMsg", duration.ToString(@"hh\:mm\:ss")) : Loc.T("CallStartedMsg")
        };

        _call.Text = Loc.T("Call");
        _meeting.Text = Loc.T("Meeting");
        var kindPanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
        kindPanel.Controls.Add(new Label { Text = Loc.T("Kind"), AutoSize = true, Margin = new Padding(3, 6, 8, 0) });
        kindPanel.Controls.Add(_call);
        kindPanel.Controls.Add(_meeting);
        (kind == EntryKind.Meeting ? _meeting : _call).Checked = true;

        _toCurrent.Text = Loc.T("AddToCurrent", current?.ToString() ?? Loc.T("NoTask"));
        _toCurrent.Enabled = current != null;
        _toCurrent.MaximumSize = new Size(480, 0);
        _toOther.Text = Loc.T("AddToOther");
        _noTarget.Text = Loc.T(ended ? "Discard" : "DecideLater");

        _other.Margin = new Padding(22, 0, 3, 8);
        _other.Items.AddRange(items.Where(i => i.Key != current?.Key).Cast<object>().ToArray());
        _other.Enabled = false;
        _toOther.Enabled = _other.Items.Count > 0;
        _toOther.CheckedChanged += (_, _) => _other.Enabled = _toOther.Checked;

        var ok = new Button { Text = Loc.T("OK"), AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 12, 3, 3) };
        ok.Padding = new Padding(14, 3, 14, 3);
        Theme.StyleButton(ok, Theme.Primary);
        ok.Click += (_, _) => Confirm();
        AcceptButton = ok;

        layout.Controls.AddRange(new Control[] { message, kindPanel, _toCurrent, _toOther, _other, _noTarget, ok });
        Controls.Add(layout);

        if (preselected != null && preselected.Key == current?.Key)
            _toCurrent.Checked = true;
        else if (preselected != null && _other.Items.Cast<WorkItem>().FirstOrDefault(i => i.Key == preselected.Key) is { } match)
        {
            _toOther.Checked = true;
            _other.SelectedItem = match;
        }
        else if (current != null)
            _toCurrent.Checked = true;
        else
            _noTarget.Checked = true;
    }

    private void Confirm()
    {
        if (_toOther.Checked && _other.SelectedItem == null)
        {
            MessageBox.Show(this, Loc.T("PickOther"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, Loc.MsgOptions);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
