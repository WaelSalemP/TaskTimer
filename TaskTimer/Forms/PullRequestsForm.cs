using TaskTimer.Models;
using TaskTimer.Services;

namespace TaskTimer.Forms;

/// <summary>Lists open Azure Repos pull requests and lets the user approve, complete or create them.</summary>
public sealed class PullRequestsForm : Form
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<string> _projects;
    private readonly WorkItem? _currentTask;
    private readonly IReadOnlyList<WorkItem> _linkable;
    private List<PullRequest> _pullRequests = new();

    private readonly CheckBox _onlyMine = new() { AutoSize = true, Checked = true, Margin = new Padding(3, 8, 12, 3) };
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, BorderStyle = BorderStyle.None,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Button _refresh = NewButton("PrRefresh", Theme.Neutral);
    private readonly Button _create = NewButton("PrNew", Theme.Green);
    private readonly Button _approve = NewButton("PrApprove", Theme.Primary);
    private readonly Button _complete = NewButton("PrComplete", Theme.Violet);
    private readonly Button _open = NewButton("OpenInBrowser", Theme.Neutral);
    private readonly Button _review = NewButton("PrReview", Theme.Primary);

    /// <param name="projects">Azure DevOps projects to look in.</param>
    /// <param name="currentTask">Task selected in the main window, offered as the link for a new pull request.</param>
    /// <param name="linkable">Azure DevOps work items a new pull request can be linked to.</param>
    public PullRequestsForm(AppSettings settings, IReadOnlyList<string> projects, WorkItem? currentTask, IReadOnlyList<WorkItem> linkable)
    {
        _settings = settings;
        _projects = projects;
        _currentTask = currentTask;
        _linkable = linkable;

        Icon = Program.AppIcon;
        Text = Loc.T("PrTitle");
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1100, 520);
        MinimumSize = new Size(700, 360);
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);
        Theme.StyleGrid(_grid);

        foreach (var key in new[] { "PrColId", "PrColTitle", "PrColRepo", "PrColBranches", "PrColAuthor", "PrColReviewers", "PrColMyVote", "PrColCreated" })
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = key, HeaderText = Loc.T(key), SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns["PrColId"]!.FillWeight = 35;
        _grid.Columns["PrColTitle"]!.FillWeight = 220;
        _grid.Columns["PrColMyVote"]!.FillWeight = 60;
        _grid.Columns["PrColCreated"]!.FillWeight = 70;

        _onlyMine.Text = Loc.T("PrOnlyMine");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
        top.Controls.AddRange(new Control[] { _onlyMine, _refresh, _status });

        var close = NewButton("MachineClose", Theme.Neutral);
        close.DialogResult = DialogResult.OK;
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        bottom.Controls.AddRange(new Control[] { _create, _review, _approve, _complete, _open, close });

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(bottom);
        CancelButton = close;

        _onlyMine.CheckedChanged += (_, _) => ShowList();
        _refresh.Click += async (_, _) => await LoadAsync();
        _create.Click += async (_, _) => await CreateAsync();
        _approve.Click += async (_, _) => await ApproveAsync();
        _complete.Click += async (_, _) => await CompleteAsync();
        _open.Click += (_, _) => { if (Selected is { } pr) OpenInBrowser(pr.WebUrl); };
        _review.Click += async (_, _) => await ReviewAsync();
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await ReviewAsync(); };
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        Shown += async (_, _) => await LoadAsync();
    }

    private static Button NewButton(string key, Color color)
    {
        var button = new Button { Text = Loc.T(key), AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
        Theme.StyleButton(button, color);
        return button;
    }

    private PullRequest? Selected => _grid.CurrentRow?.Tag as PullRequest;

    private void UpdateButtons()
    {
        var pr = Selected;
        _approve.Enabled = pr != null && pr.MyVote != PullRequestService.VoteApproved;
        _complete.Enabled = pr is { IsDraft: false };
        _open.Enabled = _review.Enabled = pr != null;
    }

    private async Task<bool> RunAsync(Func<PullRequestService, Task> action)
    {
        UseWaitCursor = true;
        foreach (var button in new[] { _refresh, _create, _approve, _complete }) button.Enabled = false;
        try
        {
            using var service = new PullRequestService(_settings);
            await action(service);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            if (!IsDisposed)
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, Loc.MsgOptions);
            return false;
        }
        finally
        {
            if (!IsDisposed)
            {
                UseWaitCursor = false;
                _refresh.Enabled = _create.Enabled = true;
                UpdateButtons();
            }
        }
    }

    private async Task LoadAsync()
    {
        _status.Text = Loc.T("PrLoading");
        await RunAsync(async service =>
        {
            _pullRequests = await service.GetActiveAsync(_projects);
            _myId = await service.GetMyIdAsync();
        });
        if (!IsDisposed) ShowList();
    }

    private void ShowList()
    {
        var selectedId = Selected?.Id;
        var shown = _onlyMine.Checked
            ? _pullRequests.Where(p => p.IAmReviewer || p.CreatedById == _myId).ToList()
            : _pullRequests;
        _grid.Rows.Clear();
        foreach (var pr in shown)
        {
            var title = pr.IsDraft ? Loc.T("PrDraft") + " " + pr.Title : pr.Title;
            var index = _grid.Rows.Add(pr.Id, title, pr.RepositoryName, $"{pr.SourceBranch} → {pr.TargetBranch}", pr.CreatedBy,
                pr.Reviewers, VoteText(pr.MyVote, pr.IAmReviewer), pr.Created?.ToString("yyyy-MM-dd") ?? "");
            _grid.Rows[index].Tag = pr;
            if (pr.Id == selectedId) _grid.CurrentCell = _grid.Rows[index].Cells[0];
        }
        _status.Text = Loc.T("PrCount", shown.Count);
        UpdateButtons();
    }

    /// <summary>The signed-in user's ID, to pick out pull requests they created or review.</summary>
    private string _myId = "";

    private static string VoteText(int vote, bool isReviewer) => vote switch
    {
        10 => Loc.T("PrVoteApproved"),
        5 => Loc.T("PrVoteSuggestions"),
        -5 => Loc.T("PrVoteWaiting"),
        -10 => Loc.T("PrVoteRejected"),
        _ => isReviewer ? Loc.T("PrVoteNone") : ""
    };

    private async Task ReviewAsync()
    {
        if (Selected is not { } pr) return;
        using (var form = new PullRequestReviewForm(_settings, pr)) form.ShowDialog(this);
        await LoadAsync(); // votes and comments may have changed
    }

    private async Task ApproveAsync()
    {
        if (Selected is not { } pr) return;
        if (await RunAsync(service => service.ApproveAsync(pr))) await LoadAsync();
    }

    private async Task CompleteAsync()
    {
        if (Selected is not { } pr) return;
        using var form = new CompletePullRequestForm(pr);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        if (await RunAsync(service => service.CompleteAsync(pr, form.Options))) await LoadAsync();
    }

    private async Task CreateAsync()
    {
        using var form = new NewPullRequestForm(_settings, _projects, _currentTask, _linkable);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await LoadAsync();
        var created = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (r.Tag as PullRequest)?.Id == form.CreatedId);
        if (created != null) _grid.CurrentCell = created.Cells[0];
    }

    private void OpenInBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, Loc.MsgOptions);
        }
    }
}

/// <summary>Asks how to complete (merge) a pull request.</summary>
public sealed class CompletePullRequestForm : Form
{
    private static readonly string[] Strategies = { "noFastForward", "squash", "rebase", "rebaseMerge" };

    private readonly ComboBox _strategy = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly CheckBox _deleteBranch = new() { AutoSize = true, Checked = true };
    private readonly CheckBox _completeItems = new() { AutoSize = true };
    private readonly TextBox _message = new() { Multiline = true, Width = 460, Height = 70, ScrollBars = ScrollBars.Vertical };

    public PullRequestCompletion Options => new(Strategies[Math.Max(0, _strategy.SelectedIndex)], _deleteBranch.Checked,
        _completeItems.Checked, _message.Text.Trim());

    public CompletePullRequestForm(PullRequest pr)
    {
        Icon = Program.AppIcon;
        Text = Loc.T("PrComplete");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);

        _strategy.Items.AddRange(new object[] { Loc.T("PrMergeNoFf"), Loc.T("PrMergeSquash"), Loc.T("PrMergeRebase"), Loc.T("PrMergeSemiLinear") });
        _strategy.SelectedIndex = 0;
        _deleteBranch.Text = Loc.T("PrDeleteBranch");
        _completeItems.Text = Loc.T("PrCompleteItems");
        _message.Text = $"Merged PR {pr.Id}: {pr.Title}";

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(14), Dock = DockStyle.Fill
        };
        layout.Controls.Add(new Label { Text = $"{pr.Id} - {pr.Title}", AutoSize = true, MaximumSize = new Size(470, 0), Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 3, 3, 10) });
        layout.Controls.Add(new Label { Text = Loc.T("PrMergeType"), AutoSize = true });
        layout.Controls.Add(_strategy);
        layout.Controls.Add(new Label { Text = Loc.T("PrMergeMessage"), AutoSize = true, Margin = new Padding(3, 10, 3, 3) });
        layout.Controls.Add(_message);
        layout.Controls.Add(_deleteBranch);
        layout.Controls.Add(_completeItems);

        var ok = new Button { Text = Loc.T("PrComplete"), AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(12, 3, 12, 3) };
        var cancel = new Button { Text = Loc.T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
        Theme.StyleButton(ok, Theme.Violet);
        Theme.StyleButton(cancel, Theme.Neutral);
        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange(new Control[] { ok, cancel });
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

/// <summary>Creates a pull request in an Azure Repos repository, optionally linked to the current task.</summary>
public sealed class NewPullRequestForm : Form
{
    private readonly AppSettings _settings;
    private readonly WorkItem? _task;
    private readonly ComboBox _project = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly ComboBox _repository = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly ComboBox _source = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 360, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly ComboBox _target = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 360, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly TextBox _title = new() { Dock = DockStyle.Fill };
    private readonly TextBox _description = new() { Dock = DockStyle.Fill, Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox _draft = new() { AutoSize = true };
    private readonly CheckedListBox _links = new() { Dock = DockStyle.Fill, Height = 150, CheckOnClick = true, IntegralHeight = false };
    private readonly TextBox _addId = new() { Width = 110 };
    private readonly Button _addIdButton = new() { AutoSize = true, Padding = new Padding(8, 0, 8, 0) };

    /// <summary>A work item in the link list: one of the loaded items, or one added by its ID.</summary>
    private sealed record LinkOption(string Id, string Text)
    {
        public override string ToString() => Text;
    }
    private readonly Button _createButton = new() { AutoSize = true, Padding = new Padding(12, 3, 12, 3) };
    private int _loadVersion;

    public int CreatedId { get; private set; }

    public NewPullRequestForm(AppSettings settings, IReadOnlyList<string> projects, WorkItem? task, IReadOnlyList<WorkItem> linkable)
    {
        _settings = settings;
        // Only an Azure DevOps work item can be linked; local and GitHub items cannot.
        _task = task != null && task.Key.StartsWith("ado:", StringComparison.Ordinal) ? task : null;

        Icon = Program.AppIcon;
        Text = Loc.T("PrNew");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(700, 640);
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string key, Control field)
        {
            table.Controls.Add(new Label { Text = Loc.T(key), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 8, 12, 8) });
            table.Controls.Add(field);
        }
        Row("TaskProject", _project);
        Row("PrRepository", _repository);
        Row("PrSource", _source);
        Row("PrTarget", _target);
        Row("TaskTitle", _title);
        Row("TaskDescription", _description);
        _draft.Text = Loc.T("PrCreateDraft");
        table.Controls.Add(new Label());
        table.Controls.Add(_draft);
        // Tasks, bugs, change requests, stories...: anything loaded can be linked, several at once.
        foreach (var item in linkable.OrderBy(i => i.Type).ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase))
            _links.Items.Add(new LinkOption(item.Id, $"{item.Type} {item.Id} - {item.Title}"), item.Id == _task?.Id);
        _addIdButton.Text = Loc.T("Add");
        _addId.PlaceholderText = Loc.T("PrLinkById");
        _addIdButton.Click += (_, _) => AddLinkById();
        _addId.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true; // Enter adds the ID instead of creating the pull request
            AddLinkById();
        };
        var addPanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        addPanel.Controls.AddRange(new Control[] { _addId, _addIdButton });
        Row("PrLinks", _links);
        table.Controls.Add(new Label());
        table.Controls.Add(addPanel);

        _createButton.Text = Loc.T("PrCreate");
        var cancel = new Button { Text = Loc.T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
        Theme.StyleButton(_createButton, Theme.Green);
        Theme.StyleButton(cancel, Theme.Neutral);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.AddRange(new Control[] { cancel, _createButton });
        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = _createButton;
        CancelButton = cancel;

        _project.Items.AddRange(projects.Cast<object>().ToArray());
        _project.SelectedIndexChanged += async (_, _) => await LoadRepositoriesAsync();
        _repository.SelectedIndexChanged += async (_, _) => await LoadBranchesAsync();
        _createButton.Click += async (_, _) => await CreateAsync();
        // A pull request for a task is usually named after it.
        if (_task != null) _title.Text = _task.Title;
        Shown += (_, _) =>
        {
            var preferred = _task?.Project ?? projects.FirstOrDefault();
            _project.SelectedItem = projects.FirstOrDefault(p => string.Equals(p, preferred, StringComparison.OrdinalIgnoreCase));
            if (_project.SelectedIndex < 0 && _project.Items.Count > 0) _project.SelectedIndex = 0;
        };
    }

    /// <summary>Adds (and ticks) a work item that is not in the list, by its number.</summary>
    private void AddLinkById()
    {
        var id = _addId.Text.Trim().TrimStart('#');
        if (!int.TryParse(id, out var number) || number <= 0)
        {
            MessageBox.Show(this, Loc.T("PrInvalidId"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, Loc.MsgOptions);
            return;
        }
        id = number.ToString();
        var existing = _links.Items.Cast<LinkOption>().ToList().FindIndex(o => o.Id == id);
        if (existing >= 0) _links.SetItemChecked(existing, true);
        else _links.Items.Add(new LinkOption(id, Loc.T("PrLinkedId", id)), true);
        _addId.Clear();
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, Loc.MsgOptions);

    private async Task LoadRepositoriesAsync()
    {
        if (_project.SelectedItem is not string project) return;
        var version = ++_loadVersion;
        _repository.Items.Clear();
        _source.Items.Clear();
        _target.Items.Clear();
        try
        {
            using var service = new PullRequestService(_settings);
            var repositories = await service.GetRepositoriesAsync(project);
            if (version != _loadVersion || IsDisposed) return;
            _repository.Items.AddRange(repositories.Cast<object>().ToArray());
            if (_repository.Items.Count > 0) _repository.SelectedIndex = 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            ShowError(ex);
        }
    }

    private async Task LoadBranchesAsync()
    {
        if (_project.SelectedItem is not string project || _repository.SelectedItem is not GitRepository repository) return;
        var version = ++_loadVersion;
        try
        {
            using var service = new PullRequestService(_settings);
            var branches = await service.GetBranchesAsync(project, repository.Id);
            if (version != _loadVersion || IsDisposed) return;
            _source.Items.Clear();
            _target.Items.Clear();
            _source.Items.AddRange(branches.Cast<object>().ToArray());
            _target.Items.AddRange(branches.Cast<object>().ToArray());
            _target.Text = repository.DefaultBranch;
            _source.Text = branches.FirstOrDefault(b => b != repository.DefaultBranch) ?? "";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            ShowError(ex);
        }
    }

    private async Task CreateAsync()
    {
        var source = _source.Text.Trim();
        var target = _target.Text.Trim();
        string? problem = _repository.SelectedItem is not GitRepository ? "PrChooseRepository"
            : source.Length == 0 || target.Length == 0 ? "PrChooseBranches"
            : string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ? "PrSameBranch"
            : _title.Text.Trim().Length == 0 ? "TitleRequired"
            : null;
        if (problem != null)
        {
            MessageBox.Show(this, Loc.T(problem), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, Loc.MsgOptions);
            return;
        }

        _createButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            using var service = new PullRequestService(_settings);
            var (id, _) = await service.CreateAsync((string)_project.SelectedItem!, (GitRepository)_repository.SelectedItem!,
                source, target, _title.Text.Trim(), _description.Text.Trim(), _draft.Checked,
                _links.CheckedItems.Cast<LinkOption>().Select(o => o.Id).Distinct().ToList());
            CreatedId = id;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            ShowError(ex);
        }
        finally
        {
            UseWaitCursor = false;
            _createButton.Enabled = true;
        }
    }
}
