using TaskTimer.Models;
using TaskTimer.Services;

namespace TaskTimer.Forms;

/// <summary>What the task form needs to know; filled in by the main window.</summary>
public sealed class TaskEditOptions
{
    /// <summary>The task being edited, or null to add a new one.</summary>
    public WorkItem? Existing { get; init; }
    public LocalTask? ExistingLocal { get; init; }
    /// <summary>Whether new tasks can be created in Azure DevOps (connected, and not GitHub).</summary>
    public bool ServerAvailable { get; init; }
    public IReadOnlyList<WorkItem> Parents { get; init; } = Array.Empty<WorkItem>();
    public IReadOnlyList<string> Projects { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Sprints { get; init; } = Array.Empty<string>();
    public string DefaultProject { get; init; } = "";
    public string DefaultSprint { get; init; } = "";
    /// <summary>Whether the assignee can be chosen (Azure DevOps).</summary>
    public bool AllowAssign { get; init; }
    public IReadOnlyList<Person> People { get; init; } = Array.Empty<Person>();
    /// <summary>Offer "Move to Azure DevOps" when editing a local task.</summary>
    public bool CanTransfer { get; init; }
    /// <summary>Adding a task that must go to Azure DevOps (moving a local task there).</summary>
    public bool ForceServer { get; init; }
    public string PrefillTitle { get; init; } = "";
    public string PrefillDescription { get; init; } = "";
    public double? PrefillRemaining { get; init; }
    /// <summary>Statuses the edited task can be set to.</summary>
    public IReadOnlyList<string> States { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Saves the form's values (creates or updates the task). Runs while the form is still open, so
    /// a server error leaves the user's input in place to correct and retry.
    /// </summary>
    public required Func<TaskEditForm, Task> Save { get; init; }
}

/// <summary>Adds a task to Azure DevOps or to the local list, or edits an existing task.</summary>
public sealed class TaskEditForm : Form
{
    private readonly TaskEditOptions _options;
    private readonly ComboBox _where = NewCombo(300);
    private readonly TextBox _title = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _type = NewCombo(160);
    private readonly ComboBox _project = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 300 };
    private readonly ComboBox _parent = NewCombo(460);
    private readonly ComboBox _sprint = NewCombo(460);
    private readonly ComboBox _state = NewCombo(200);
    private readonly ComboBox _assignee = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 460 };
    private string _initialAssignee = "";
    private readonly NumericUpDown _remaining = new() { DecimalPlaces = 2, Maximum = 10000, Increment = 0.5m, Width = 110 };
    private readonly TextBox _customer = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _delivery = new() { ShowCheckBox = true, Checked = false, Format = DateTimePickerFormat.Short, Width = 160 };
    private readonly TextBox _description = new() { Dock = DockStyle.Fill, Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical };
    private readonly Button _save = new() { AutoSize = true, Padding = new Padding(12, 3, 12, 3) };
    private readonly Dictionary<string, Control[]> _rows = new();

    public bool IsAdding => _options.Existing == null;
    /// <summary>True when the task is (or will be) a local one.</summary>
    public bool IsLocal => IsAdding ? _where.SelectedIndex == 1 : _options.ExistingLocal != null;
    /// <summary>Set when the user chose to delete the (local) task.</summary>
    public bool Deleted { get; private set; }
    /// <summary>Set when the user chose to move this local task to Azure DevOps.</summary>
    public bool TransferRequested { get; private set; }

    /// <summary>Account to assign to: null = "Me" (when adding), "" = unassigned, otherwise a person or a typed e-mail.</summary>
    public string? AssignedTo
    {
        get
        {
            var text = _assignee.Text.Trim();
            if (IsAdding && text == Loc.T("AssignToMe")) return null;
            var person = _assignee.SelectedItem as Person ?? _options.People.FirstOrDefault(p => p.ToString() == text);
            return person?.Value ?? text;
        }
    }

    public bool AssigneeChanged => _assignee.Text.Trim() != _initialAssignee;

    public string TaskTitle => _title.Text.Trim();
    public string TaskType => _type.SelectedItem as string ?? "Task";
    public string Project => _project.Text.Trim();
    public string? ParentId => (_parent.SelectedItem as WorkItem)?.Id;
    public string Sprint => _sprint.SelectedIndex > 0 ? (string)_sprint.SelectedItem! : "";
    public string? State => _state.SelectedItem as string;
    public double? RemainingWork => _remaining.Value > 0 || !IsAdding ? (double)_remaining.Value : null;
    public string Customer => _customer.Text.Trim();
    public DateTime? DeliveryDate => _delivery.Checked ? _delivery.Value.Date : null;
    public string Description => _description.Text.Trim();

    public TaskEditForm(TaskEditOptions options)
    {
        _options = options;

        Icon = Program.AppIcon;
        Text = Loc.T(IsAdding ? "AddTask" : "EditTask");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(660, 520);
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string key, Control field)
        {
            var caption = new Label { Text = Loc.T(key), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 8, 12, 8) };
            table.Controls.Add(caption);
            table.Controls.Add(field);
            _rows[key] = new Control[] { caption, field };
        }
        Row("TaskWhere", _where);
        Row("TaskTitle", _title);
        Row("TaskType", _type);
        Row("TaskProject", _project);
        Row("TaskParent", _parent);
        Row("TaskSprint", _sprint);
        Row("TaskState", _state);
        Row("TaskAssignee", _assignee);
        Row("TaskRemaining", _remaining);
        Row("TaskCustomer", _customer);
        Row("TaskDelivery", _delivery);
        Row("TaskDescription", _description);

        _where.Items.AddRange(new object[] { Loc.T("WhereServer"), Loc.T("WhereLocal") });
        _type.Items.AddRange(new object[] { "Task", "Bug" });
        _project.Items.AddRange(options.Projects.Cast<object>().ToArray());
        _parent.Items.Add(Loc.T("NoneOption"));
        _parent.Items.AddRange(options.Parents.Cast<object>().ToArray());
        _sprint.Items.Add(Loc.T("NoneOption"));
        _sprint.Items.AddRange(options.Sprints.Cast<object>().ToArray());
        _state.Items.AddRange(options.States.Cast<object>().ToArray());
        if (IsAdding) _assignee.Items.Add(Loc.T("AssignToMe"));
        _assignee.Items.AddRange(options.People.Cast<object>().ToArray());
        new ToolTip().SetToolTip(_assignee, Loc.T("AssigneeHint"));

        // A new task under a parent belongs to the parent's project and, usually, its sprint.
        _parent.SelectedIndexChanged += (_, _) =>
        {
            if (_parent.SelectedItem is not WorkItem parent) return;
            _project.Text = parent.Project;
            if (_sprint.Items.Contains(parent.Sprint)) _sprint.SelectedItem = parent.Sprint;
        };
        _where.SelectedIndexChanged += (_, _) => UpdateRows();

        _save.Text = Loc.T("Save");
        var cancel = new Button { Text = Loc.T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
        var delete = new Button { Text = Loc.T("DeleteTask"), AutoSize = true, Padding = new Padding(8, 3, 8, 3), Visible = !IsAdding && IsLocal };
        var transfer = new Button
        {
            Text = Loc.T("MoveToServer"), AutoSize = true, Padding = new Padding(8, 3, 8, 3),
            Visible = !IsAdding && IsLocal && options.CanTransfer
        };
        Theme.StyleButton(transfer, Theme.Violet);
        transfer.Click += (_, _) =>
        {
            if (TaskTitle.Length == 0) { Warn(Loc.T("TitleRequired")); return; }
            // The main window opens the "add to Azure DevOps" form with these values.
            TransferRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        Theme.StyleButton(_save, Theme.Primary);
        Theme.StyleButton(cancel, Theme.Neutral);
        Theme.StyleButton(delete, Theme.Coral);
        _save.Click += async (_, _) => await SaveAsync();
        delete.Click += async (_, _) => await DeleteAsync();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.AddRange(new Control[] { cancel, _save, delete, transfer });

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = _save;
        CancelButton = cancel;

        LoadValues();
        UpdateRows();
    }

    private static ComboBox NewCombo(int width) => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };

    private void LoadValues()
    {
        _type.SelectedIndex = 0;
        _parent.SelectedIndex = 0;
        _project.Text = _options.DefaultProject;
        _sprint.SelectedItem = _options.Sprints.Contains(_options.DefaultSprint) ? _options.DefaultSprint : null;
        if (_sprint.SelectedIndex < 0) _sprint.SelectedIndex = 0;

        if (IsAdding)
        {
            _where.SelectedIndex = _options.ServerAvailable ? 0 : 1;
            _where.Enabled = _options.ServerAvailable && !_options.ForceServer;
            _title.Text = _options.PrefillTitle;
            _description.Text = _options.PrefillDescription;
            _remaining.Value = (decimal)Math.Clamp(_options.PrefillRemaining ?? 0, 0, (double)_remaining.Maximum);
            _assignee.SelectedIndex = 0; // "Me"
            _initialAssignee = _assignee.Text;
            return;
        }

        var item = _options.Existing!;
        _title.Text = item.Title;
        var current = _options.People.FirstOrDefault(p =>
            item.AssignedToAccount.Length > 0 ? string.Equals(p.Account, item.AssignedToAccount, StringComparison.OrdinalIgnoreCase) : p.Name == item.AssignedTo);
        _assignee.Text = current?.ToString() ?? item.AssignedTo;
        _initialAssignee = _assignee.Text.Trim();
        _state.SelectedItem = _options.States.FirstOrDefault(s => string.Equals(s, item.State, StringComparison.OrdinalIgnoreCase));
        _remaining.Value = (decimal)Math.Clamp(item.RemainingWork ?? 0, 0, (double)_remaining.Maximum);
        if (_options.ExistingLocal is { } local)
        {
            _customer.Text = local.Customer;
            _description.Text = local.Description;
            if (local.DeliveryDate is { } date)
            {
                _delivery.Value = date;
                _delivery.Checked = true;
            }
        }
    }

    /// <summary>Shows only the fields that apply to adding/editing and to Azure DevOps/local.</summary>
    private void UpdateRows()
    {
        var server = !IsLocal;
        void Show(string key, bool visible) { foreach (var c in _rows[key]) c.Visible = visible; }
        Show("TaskWhere", IsAdding);
        Show("TaskType", IsAdding && server);
        Show("TaskProject", IsAdding && server);
        Show("TaskParent", IsAdding && server);
        Show("TaskSprint", IsAdding && server);
        Show("TaskState", !IsAdding);
        Show("TaskAssignee", server && _options.AllowAssign);
        Show("TaskCustomer", IsLocal);
        Show("TaskDelivery", IsLocal);
        // Azure DevOps keeps descriptions as formatted HTML, which a plain text box would flatten,
        // so for existing server tasks it is edited in the browser instead.
        Show("TaskDescription", IsAdding || IsLocal);
    }

    private async Task SaveAsync()
    {
        string? problem = null;
        if (TaskTitle.Length == 0) problem = "TitleRequired";
        else if (IsAdding && !IsLocal && Project.Length == 0) problem = "ProjectRequired";
        if (problem != null)
        {
            Warn(Loc.T(problem));
            return;
        }

        _save.Enabled = false;
        UseWaitCursor = true;
        try
        {
            await _options.Save(this);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or NotSupportedException or IOException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, Loc.MsgOptions);
        }
        finally
        {
            UseWaitCursor = false;
            _save.Enabled = true;
        }
    }

    private async Task DeleteAsync()
    {
        var answer = MessageBox.Show(this, Loc.T("ConfirmDeleteTask", _options.Existing!.Title), Text, MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2, Loc.MsgOptions);
        if (answer != DialogResult.Yes) return;
        Deleted = true;
        await _options.Save(this);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, Loc.MsgOptions);
}
