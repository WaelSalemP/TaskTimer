using System.Globalization;
using TaskTimer.Models;
using TaskTimer.Services;

namespace TaskTimer.Forms;

/// <summary>Lets the user correct or delete the locally recorded time of one task before it is sent.</summary>
public sealed class EditTimeForm : Form
{
    private const string DurationColumn = "Duration";

    private readonly TimeLog _log;
    private readonly List<TimeEntry> _removed = new();
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
        RowHeadersVisible = false, MultiSelect = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Label _total = new() { AutoSize = true, Margin = new Padding(3, 8, 3, 3) };

    public EditTimeForm(WorkItem item, IEnumerable<TimeEntry> pending, TimeLog log)
    {
        _log = log;

        Icon = Program.AppIcon;
        Text = Loc.T("EditTime");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(560, 420);
        Loc.ApplyDirection(this);
        Theme.StyleForm(this, Theme.Surface);
        Theme.StyleGrid(_grid);

        var hint = new Label
        {
            Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(530, 0), Padding = new Padding(12, 12, 12, 8),
            Text = Loc.T("EditTimeHint", item)
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Loc.T("ColStart"), ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Loc.T("ColKind"), ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = DurationColumn, HeaderText = Loc.T("ColDuration"), SortMode = DataGridViewColumnSortMode.NotSortable });
        foreach (var entry in pending.OrderBy(e => e.StartUtc))
        {
            var index = _grid.Rows.Add(entry.StartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), Loc.T(entry.Kind.ToString()),
                Format(entry.Seconds));
            _grid.Rows[index].Tag = entry;
        }
        _grid.CellValidating += OnCellValidating;
        _grid.CellEndEdit += (_, e) =>
        {
            // Show the value the way it was understood, e.g. "1:5" becomes "1:05:00".
            var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (TryParse(cell.Value?.ToString(), out var seconds)) cell.Value = Format(seconds);
            UpdateTotal();
        };

        var remove = new Button { Text = Loc.T("Remove"), AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
        var removeAll = new Button { Text = Loc.T("RemoveAll"), AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
        var save = new Button { Text = Loc.T("Save"), AutoSize = true, Padding = new Padding(12, 3, 12, 3) };
        var cancel = new Button { Text = Loc.T("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
        Theme.StyleButton(remove, Theme.Neutral);
        Theme.StyleButton(removeAll, Theme.Coral);
        Theme.StyleButton(save, Theme.Primary);
        Theme.StyleButton(cancel, Theme.Neutral);
        remove.Click += (_, _) => Remove(_grid.SelectedRows.Cast<DataGridViewRow>().ToList());
        removeAll.Click += (_, _) => Remove(_grid.Rows.Cast<DataGridViewRow>().ToList());
        save.Click += (_, _) => SaveAndClose();

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var left = new FlowLayoutPanel { AutoSize = true };
        left.Controls.AddRange(new Control[] { remove, removeAll, _total });
        var right = new FlowLayoutPanel { AutoSize = true };
        right.Controls.AddRange(new Control[] { save, cancel });
        bottom.Controls.Add(left, 0, 0);
        bottom.Controls.Add(right, 1, 0);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(hint);
        CancelButton = cancel;
        UpdateTotal();
    }

    /// <summary>Hours may exceed 23, which TimeSpan's own formats cannot express.</summary>
    private static string Format(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Round(seconds));
        return $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";
    }

    /// <summary>Accepts "h:mm" or "h:mm:ss".</summary>
    private static bool TryParse(string? text, out double seconds)
    {
        seconds = 0;
        var parts = (text ?? "").Trim().Split(':');
        if (parts.Length is < 2 or > 3) return false;

        var values = new int[3];
        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                return false;
        if (values[1] > 59 || values[2] > 59) return false;

        seconds = values[0] * 3600.0 + values[1] * 60 + values[2];
        return true;
    }

    private void OnCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_grid.Columns[e.ColumnIndex].Name != DurationColumn || !_grid.IsCurrentCellInEditMode) return;
        if (TryParse(e.FormattedValue?.ToString(), out _)) return;

        e.Cancel = true;
        MessageBox.Show(this, Loc.T("InvalidDuration"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1, Loc.MsgOptions);
    }

    private void Remove(List<DataGridViewRow> rows)
    {
        _grid.CancelEdit();
        foreach (var row in rows)
        {
            if (row.Tag is TimeEntry entry) _removed.Add(entry);
            _grid.Rows.Remove(row);
        }
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        var seconds = _grid.Rows.Cast<DataGridViewRow>()
            .Sum(r => TryParse(r.Cells[DurationColumn].Value?.ToString(), out var s) ? s : 0);
        _total.Text = Loc.T("TotalTime", Format(seconds));
    }

    private void SaveAndClose()
    {
        if (!_grid.EndEdit()) return; // an invalid duration is still being typed

        var changed = new Dictionary<TimeEntry, double>();
        foreach (DataGridViewRow row in _grid.Rows)
            if (row.Tag is TimeEntry entry && TryParse(row.Cells[DurationColumn].Value?.ToString(), out var seconds))
                changed[entry] = seconds;

        _log.Update(changed, _removed);
        DialogResult = DialogResult.OK;
        Close();
    }
}
