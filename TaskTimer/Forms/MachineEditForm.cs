using TaskTimer.Models;
using TaskTimer.Services;

namespace TaskTimer.Forms;

/// <summary>Adds or edits one dev machine and its sign-in details.</summary>
public sealed class MachineEditForm : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _location = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _address = new() { Dock = DockStyle.Fill, PlaceholderText = "devbox01.contoso.local  /  10.0.0.5:3389  /  https://..." };
    private readonly TextBox _user = new() { Dock = DockStyle.Fill, PlaceholderText = @"DOMAIN\user  /  user@contoso.com" };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly ComboBox _cloudSource = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly TextBox _subscription = new() { Dock = DockStyle.Fill };
    private readonly TextBox _resourceGroup = new() { Dock = DockStyle.Fill };
    private readonly TextBox _azureVm = new() { Dock = DockStyle.Fill };
    private readonly TextBox _lcsProject = new() { Dock = DockStyle.Fill, PlaceholderText = "1234567" };
    private readonly TextBox _lcsEnvironment = new() { Dock = DockStyle.Fill, PlaceholderText = "00000000-0000-0000-0000-000000000000" };
    private readonly TextBox _lcsApiUrl = new() { Dock = DockStyle.Fill, PlaceholderText = MachinePower.DefaultLcsApiUrl };
    private readonly TextBox _hyperVHost = new() { Dock = DockStyle.Fill };
    private readonly TextBox _hyperVVm = new() { Dock = DockStyle.Fill };
    private readonly List<Control> _sourceRow = new();
    private readonly List<Control> _azureRows = new();
    private readonly List<Control> _lcsRows = new();
    private readonly List<Control> _onPremisesRows = new();

    public DevMachine Machine { get; }

    /// <summary>The password to save, or null to keep the one already saved (or none).</summary>
    public string? Password => _password.Text.Length == 0 ? null : _password.Text;

    public MachineEditForm(DevMachine? existing)
    {
        Machine = existing == null ? new DevMachine() : existing.Clone();

        Icon = Program.AppIcon;
        Text = Loc.T("MachinesTitle");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(640, 540);
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);

        _location.Items.AddRange(new object[] { Loc.T("OnPremises"), Loc.T("Cloud") });
        _cloudSource.Items.AddRange(new object[] { Loc.T("SourceAzure"), Loc.T("SourceLcs") });

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Control[] Row(string label, Control field)
        {
            var caption = new Label { Text = Loc.T(label), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 8) };
            table.Controls.Add(caption);
            table.Controls.Add(field);
            return new[] { caption, field };
        }
        Row("MachineName", _name);
        Row("MachineLocation", _location);
        Row("MachineAddress", _address);
        Row("MachineUser", _user);
        Row("MachinePassword", _password);

        var powerHint = new Label { Text = Loc.T("PowerHint"), AutoSize = true, MaximumSize = new Size(600, 0), ForeColor = Theme.Muted, Margin = new Padding(3, 14, 3, 6) };
        table.Controls.Add(powerHint);
        table.SetColumnSpan(powerHint, 2);
        _sourceRow.AddRange(Row("CloudSource", _cloudSource));
        _azureRows.AddRange(Row("AzureSubscription", _subscription));
        _azureRows.AddRange(Row("AzureResourceGroup", _resourceGroup));
        _azureRows.AddRange(Row("MachineVm", _azureVm));
        _lcsRows.AddRange(Row("LcsProject", _lcsProject));
        _lcsRows.AddRange(Row("LcsEnvironment", _lcsEnvironment));
        _lcsRows.AddRange(Row("LcsApiUrl", _lcsApiUrl));
        _onPremisesRows.AddRange(Row("HyperVHost", _hyperVHost));
        _onPremisesRows.AddRange(Row("MachineVm", _hyperVVm));
        _location.SelectedIndexChanged += (_, _) => UpdateVmRows();
        _cloudSource.SelectedIndexChanged += (_, _) => UpdateVmRows();

        var hint = new Label { Text = Loc.T("AddressHint"), AutoSize = true, MaximumSize = new Size(600, 0), ForeColor = Theme.Muted, Margin = new Padding(3, 10, 3, 3) };
        table.Controls.Add(hint);
        table.SetColumnSpan(hint, 2);

        var save = new Button { Text = Loc.T("Save"), AutoSize = true, Padding = new Padding(12, 3, 12, 3) };
        var cancel = new Button { Text = Loc.T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
        Theme.StyleButton(save, Theme.Primary);
        Theme.StyleButton(cancel, Theme.Neutral);
        save.Click += (_, _) => SaveAndClose();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.AddRange(new Control[] { cancel, save });

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;

        _name.Text = Machine.Name;
        _location.SelectedIndex = (int)Machine.Location;
        _cloudSource.SelectedIndex = (int)Machine.CloudSource;
        _address.Text = Machine.Address;
        _user.Text = Machine.UserName;
        _subscription.Text = Machine.AzureSubscription;
        _resourceGroup.Text = Machine.AzureResourceGroup;
        _azureVm.Text = Machine.AzureVmName;
        _lcsProject.Text = Machine.LcsProjectId;
        _lcsEnvironment.Text = Machine.LcsEnvironmentId;
        _lcsApiUrl.Text = Machine.LcsApiUrl == MachinePower.DefaultLcsApiUrl ? "" : Machine.LcsApiUrl;
        _hyperVHost.Text = Machine.HyperVHost;
        _hyperVVm.Text = Machine.HyperVVmName;
        if (existing != null && RemoteDesktop.HasPassword(existing))
            _password.PlaceholderText = Loc.T("KeepPassword");
        UpdateVmRows();
    }

    private bool IsCloud => _location.SelectedIndex == (int)MachineLocation.Cloud;

    private bool IsLcs => IsCloud && _cloudSource.SelectedIndex == (int)CloudSource.Lcs;

    /// <summary>Shows only the fields for how this machine is managed; the other rows collapse.</summary>
    private void UpdateVmRows()
    {
        foreach (var control in _sourceRow) control.Visible = IsCloud;
        foreach (var control in _azureRows) control.Visible = IsCloud && !IsLcs;
        foreach (var control in _lcsRows) control.Visible = IsLcs;
        foreach (var control in _onPremisesRows) control.Visible = !IsCloud;
    }

    private void SaveAndClose()
    {
        string? problem = null;
        var address = _address.Text.Trim();
        if (_name.Text.Trim().Length == 0 || address.Length == 0) problem = "MachineRequired";
        else if (!RemoteDesktop.IsValidAddress(address)) problem = "InvalidAddress";
        else if (Password != null && _user.Text.Trim().Length == 0 && !RemoteDesktop.IsWebAddress(address)) problem = "UserRequired";

        // Only the details for the chosen way of managing the machine are kept, so a machine moved
        // from Azure to LCS (or to on-premises) cannot be started through its old details.
        var azure = IsCloud && !IsLcs;
        var candidate = new DevMachine
        {
            Location = (MachineLocation)_location.SelectedIndex,
            CloudSource = (CloudSource)Math.Max(0, _cloudSource.SelectedIndex),
            AzureSubscription = azure ? _subscription.Text.Trim() : "",
            AzureResourceGroup = azure ? _resourceGroup.Text.Trim() : "",
            AzureVmName = azure ? _azureVm.Text.Trim() : "",
            LcsProjectId = IsLcs ? _lcsProject.Text.Trim() : "",
            LcsEnvironmentId = IsLcs ? _lcsEnvironment.Text.Trim() : "",
            LcsApiUrl = IsLcs && _lcsApiUrl.Text.Trim().Length > 0 ? _lcsApiUrl.Text.Trim().TrimEnd('/') : MachinePower.DefaultLcsApiUrl,
            HyperVHost = IsCloud ? "" : _hyperVHost.Text.Trim(),
            HyperVVmName = IsCloud ? "" : _hyperVVm.Text.Trim()
        };
        problem ??= MachinePower.Validate(candidate);
        if (problem != null)
        {
            MessageBox.Show(this, Loc.T(problem), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, Loc.MsgOptions);
            return;
        }

        Machine.Name = _name.Text.Trim();
        Machine.Location = candidate.Location;
        Machine.CloudSource = candidate.CloudSource;
        Machine.Address = address;
        Machine.UserName = _user.Text.Trim();
        Machine.AzureSubscription = candidate.AzureSubscription;
        Machine.AzureResourceGroup = candidate.AzureResourceGroup;
        Machine.AzureVmName = candidate.AzureVmName;
        Machine.LcsProjectId = candidate.LcsProjectId;
        Machine.LcsEnvironmentId = candidate.LcsEnvironmentId;
        Machine.LcsApiUrl = candidate.LcsApiUrl;
        Machine.HyperVHost = candidate.HyperVHost;
        Machine.HyperVVmName = candidate.HyperVVmName;
        DialogResult = DialogResult.OK;
        Close();
    }
}
