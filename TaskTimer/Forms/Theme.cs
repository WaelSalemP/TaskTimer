using System.Runtime.InteropServices;

namespace TaskTimer.Forms;

/// <summary>
/// Shared colour palette and control styling so every form looks the same. The palette follows
/// <see cref="IsDark"/>; the Style* methods can be called again after it changes to repaint a form.
/// </summary>
public static class Theme
{
    public static bool IsDark { get; set; }

    private static Color Pick(Color light, Color dark) => IsDark ? dark : light;

    public static Color Background => Pick(Color.FromArgb(244, 247, 252), Color.FromArgb(24, 28, 37));
    public static Color Surface => Pick(Color.White, Color.FromArgb(33, 39, 51));
    public static Color Text => Pick(Color.FromArgb(38, 50, 72), Color.FromArgb(228, 233, 242));
    public static Color Muted => Pick(Color.FromArgb(125, 137, 156), Color.FromArgb(140, 151, 170));
    public static Color Neutral => Pick(Color.FromArgb(232, 237, 245), Color.FromArgb(55, 64, 82));
    private static Color Disabled => Pick(Color.FromArgb(214, 220, 230), Color.FromArgb(48, 54, 68));
    private static Color InputBack => Pick(Color.White, Color.FromArgb(24, 28, 37));
    private static Color HeaderBack => Pick(Color.FromArgb(230, 239, 254), Color.FromArgb(44, 54, 74));
    private static Color AlternateRow => Pick(Color.FromArgb(248, 250, 253), Color.FromArgb(38, 45, 59));
    private static Color Selection => Pick(Color.FromArgb(207, 227, 255), Color.FromArgb(48, 80, 132));
    private static Color GridLine => Pick(Color.FromArgb(228, 233, 241), Color.FromArgb(50, 58, 74));

    // Code review: added and removed lines, and comments shown between them.
    public static Color DiffAdded => Pick(Color.FromArgb(220, 252, 231), Color.FromArgb(28, 66, 40));
    public static Color DiffRemoved => Pick(Color.FromArgb(254, 226, 226), Color.FromArgb(78, 34, 38));
    public static Color DiffComment => Pick(Color.FromArgb(239, 246, 255), Color.FromArgb(40, 48, 72));

    // Accent colours read well on both palettes, so they do not change with the theme.
    public static readonly Color Primary = Color.FromArgb(59, 130, 246);
    public static readonly Color Green = Color.FromArgb(22, 163, 74);
    public static readonly Color Coral = Color.FromArgb(229, 83, 83);
    public static readonly Color Violet = Color.FromArgb(124, 92, 224);

    public static void StyleForm(Form form, Color back)
    {
        form.BackColor = back;
        form.ForeColor = Text;
        if (form.IsHandleCreated)
        {
            StyleInputs(form);
            StyleTitleBar(form);
        }
        else
        {
            // Dialogs call this before their controls exist; finish once the form is built.
            form.Load += (_, _) =>
            {
                StyleInputs(form);
                StyleTitleBar(form);
            };
        }
    }

    /// <summary>Text boxes, lists and combo boxes keep the system colours unless told otherwise.</summary>
    private static readonly Color GroupTitleDark = Color.FromArgb(245, 247, 252);

    private static void StyleInputs(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case ComboBox combo:
                    combo.FlatStyle = IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                    combo.BackColor = InputBack;
                    combo.ForeColor = Text;
                    break;
                case TextBoxBase or ListBox or NumericUpDown:
                    control.BackColor = InputBack;
                    control.ForeColor = Text;
                    break;
                case GroupBox:
                    // Windows draws a group box title in its own theme colour (dark) unless the
                    // colour is set on the control itself; an inherited colour is ignored.
                    control.ForeColor = IsDark ? GroupTitleDark : Text;
                    break;
            }
            StyleInputs(control);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Asks Windows for a dark title bar to match; silently ignored on versions without it.</summary>
    private static void StyleTitleBar(Form form)
    {
        const int UseImmersiveDarkMode = 20;
        var enabled = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, UseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    private sealed class ButtonStyle
    {
        public Color Color;
        public bool IsNeutral;
    }

    /// <summary>
    /// Flat filled button. Pass <see cref="Neutral"/> for a secondary (low-emphasis) button.
    /// Calling it again on the same button just refreshes its colours.
    /// </summary>
    public static void StyleButton(Button button, Color color)
    {
        if (button.Tag is not ButtonStyle style)
        {
            button.Tag = style = new ButtonStyle();
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(3, 3, 8, 3);
            button.EnabledChanged += (_, _) => Apply(button, style);
        }
        style.IsNeutral = color == Neutral;
        style.Color = color;
        Apply(button, style);
    }

    private static void Apply(Button button, ButtonStyle style)
    {
        button.BackColor = button.Enabled ? style.Color : Disabled;
        button.ForeColor = style.IsNeutral ? Text : Color.White;
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.GridColor = GridLine;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false; // otherwise Windows ignores the header colours
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 36;
        grid.RowTemplate.Height = 30;

        var header = grid.ColumnHeadersDefaultCellStyle;
        header.BackColor = header.SelectionBackColor = HeaderBack;
        header.ForeColor = header.SelectionForeColor = Text;
        if (header.Font is not { Bold: true }) header.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        header.Padding = new Padding(4, 0, 4, 0);

        var cell = grid.DefaultCellStyle;
        cell.BackColor = Surface;
        cell.ForeColor = Text;
        cell.SelectionBackColor = Selection;
        cell.SelectionForeColor = Text;
        cell.Padding = new Padding(4, 0, 4, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = AlternateRow;
    }

    public static void StyleStrip(ToolStrip strip, Color back)
    {
        strip.BackColor = back;
        strip.ForeColor = Text;
        if (strip.Renderer is not FlatRenderer) strip.Renderer = new FlatRenderer();
    }

    /// <summary>Professional renderer without the gradient background and the border line.</summary>
    private sealed class FlatRenderer : ToolStripProfessionalRenderer
    {
        public FlatRenderer() => RoundedEdges = false;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    }
}
