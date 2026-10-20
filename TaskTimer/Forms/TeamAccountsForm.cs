using TaskTimer.Models;
using TaskTimer.Services;

namespace TaskTimer.Forms;

/// <summary>Edits the list of team members' e-mail accounts whose tasks are loaded next to my own.</summary>
public sealed class TeamAccountsForm : Form
{
    private readonly AppSettings _settings;
    private readonly TextBox _email = new() { Dock = DockStyle.Fill, PlaceholderText = "name@company.com" };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
    private readonly Button _add = new() { AutoSize = true };
    private readonly Button _remove = new() { AutoSize = true };
    private readonly Button _save = new() { AutoSize = true };
    private readonly Button _cancel = new() { AutoSize = true, DialogResult = DialogResult.Cancel };

    public TeamAccountsForm(AppSettings settings)
    {
        _settings = settings;

        Icon = Program.AppIcon;
        Text = Loc.T("TeamAccounts");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 400);
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);

        _add.Text = Loc.T("Add");
        _remove.Text = Loc.T("Remove");
        _save.Text = Loc.T("Save");
        _cancel.Text = Loc.T("Cancel");
        Theme.StyleButton(_add, Theme.Green);
        Theme.StyleButton(_remove, Theme.Neutral);
        Theme.StyleButton(_save, Theme.Primary);
        Theme.StyleButton(_cancel, Theme.Neutral);
        foreach (var button in new[] { _add, _remove, _save, _cancel }) button.Padding = new Padding(8, 3, 8, 3);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hint = new Label { Text = Loc.T("TeamAccountsHint"), AutoSize = true, MaximumSize = new Size(490, 0), Margin = new Padding(3, 3, 3, 10) };
        table.Controls.Add(hint, 0, 0);
        table.SetColumnSpan(hint, 2);
        _email.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        table.Controls.Add(_email, 0, 1);
        table.Controls.Add(_add, 1, 1);
        table.Controls.Add(_list, 0, 2);
        table.Controls.Add(_remove, 1, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8)
        };
        buttons.Controls.AddRange(new Control[] { _cancel, _save });

        Controls.Add(table);
        Controls.Add(buttons);
        CancelButton = _cancel;

        _list.Items.AddRange(settings.TeamEmails.Cast<object>().ToArray());

        _add.Click += (_, _) => AddEmail();
        _email.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true; // Enter adds the address instead of closing the dialog
            AddEmail();
        };
        _remove.Click += (_, _) =>
        {
            foreach (var selected in _list.SelectedItems.Cast<object>().ToList()) _list.Items.Remove(selected);
        };
        _save.Click += (_, _) => SaveAndClose();
    }

    private void AddEmail()
    {
        // Several addresses can be pasted at once.
        var entries = _email.Text.Split(new[] { ';', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length == 0) return;

        var invalid = entries.FirstOrDefault(e => !IsEmail(e));
        if (invalid != null)
        {
            MessageBox.Show(this, Loc.T("InvalidEmail", invalid), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, Loc.MsgOptions);
            return;
        }

        foreach (var entry in entries)
            if (!_list.Items.Cast<string>().Contains(entry, StringComparer.OrdinalIgnoreCase))
                _list.Items.Add(entry);
        _email.Clear();
        _email.Focus();
    }

    private static bool IsEmail(string text)
    {
        var at = text.IndexOf('@');
        return at > 0 && at == text.LastIndexOf('@') && text.IndexOf('.', at) > at + 1 && !text.EndsWith('.');
    }

    private void SaveAndClose()
    {
        AddEmail(); // include an address typed but not yet added
        if (_email.Text.Trim().Length > 0) return; // it was invalid; the user has been told
        _settings.TeamEmails = _list.Items.Cast<string>().ToList();
        SettingsStore.Save(_settings);
        DialogResult = DialogResult.OK;
        Close();
    }
}
