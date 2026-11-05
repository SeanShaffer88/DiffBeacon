namespace DiffBeacon.App;

static class DarkTheme
{
    public static bool HighContrast => SystemInformation.HighContrast;
    public static Color Window => HighContrast ? SystemColors.Control : Color.FromArgb(20, 20, 22);
    public static Color Caption => HighContrast ? SystemColors.Control : Color.FromArgb(17, 17, 19);
    public static Color Surface => HighContrast ? SystemColors.Window : Color.FromArgb(27, 27, 31);
    public static Color Editor => HighContrast ? SystemColors.Window : Color.FromArgb(20, 20, 22);
    public static Color Raised => HighContrast ? SystemColors.Control : Color.FromArgb(35, 35, 39);
    public static Color Border => HighContrast ? SystemColors.WindowText : Color.FromArgb(119, 118, 128);
    public static Color Text => HighContrast ? SystemColors.ControlText : Color.FromArgb(238, 239, 244);
    public static Color SurfaceText => HighContrast ? SystemColors.WindowText : Text;
    public static Color Muted => HighContrast ? SystemColors.ControlText : Color.FromArgb(178, 177, 187);
    public static Color DecorativeBorder => HighContrast ? SystemColors.WindowText : Color.FromArgb(52, 52, 59);
    public static Color ChromeAccent => HighContrast ? SystemColors.Highlight : Color.FromArgb(143, 50, 71);
    public static Color ChromeSelection => HighContrast ? SystemColors.Highlight : Color.FromArgb(90, 39, 51);
    public static Color AccentHover => HighContrast ? SystemColors.Highlight : Color.FromArgb(166, 64, 88);
    public static Color Focus => HighContrast ? SystemColors.HighlightText : Color.FromArgb(225, 139, 158);
    public static Color Selection => HighContrast ? SystemColors.Highlight : Color.FromArgb(35, 118, 128);
    public static Color SelectionText => HighContrast ? SystemColors.HighlightText : Color.White;
    public static Color Amber => HighContrast ? SystemColors.Window : Color.FromArgb(244, 194, 89);
    public static Color AmberText => HighContrast ? SystemColors.WindowText : Color.FromArgb(32, 35, 40);
    public static void Button(Button button)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = AccentHover; button.FlatAppearance.MouseDownBackColor = ChromeSelection;
        button.BackColor = Raised; button.ForeColor = Text; button.UseVisualStyleBackColor = false;
        button.MouseEnter -= OnButtonMouseEnter; button.MouseLeave -= OnButtonMouseLeave;
        button.MouseEnter += OnButtonMouseEnter; button.MouseLeave += OnButtonMouseLeave;
    }
    static void OnButtonMouseEnter(object? sender, EventArgs e) { if (HighContrast && sender is Button button) button.ForeColor = SystemColors.HighlightText; }
    static void OnButtonMouseLeave(object? sender, EventArgs e) { if (sender is Button button) button.ForeColor = Text; }
    public static void DifferenceRow(DataGridViewCellStyle style, string? status = "Changed")
    { bool sideOnly = status is "LeftOnly" or "RightOnly"; style.BackColor = sideOnly ? Selection : Amber; style.ForeColor = sideOnly ? SelectionText : AmberText; style.SelectionBackColor = style.BackColor; style.SelectionForeColor = style.ForeColor; }
    public static void Tooltip(ToolTip tip) { tip.BackColor = Raised; tip.ForeColor = Text; }
    public static void Apply(Control control)
    { if (control is CompareView view) { view.ApplyTheme(); return; } ApplyCore(control); }
    internal static void ApplyCore(Control control)
    {
        control.BackColor = control is RichTextBox ? Editor : control is TextBoxBase or ComboBox ? Raised : control is ListBox ? Surface : control is Button ? Raised : Window;
        control.ForeColor = control is RichTextBox or ListBox ? SurfaceText : Text;
        if (control is Button button) Button(button);
        if (control is Label label && label.Dock == DockStyle.Bottom) { label.BackColor = Caption; label.ForeColor = Muted; }
        if (control is TextBoxBase box) box.BorderStyle = box is RichTextBox ? BorderStyle.None : BorderStyle.FixedSingle;
        if (control is ToolStrip strip)
        {
            strip.Renderer = new DarkMenuRenderer(); strip.BackColor = Window; strip.ForeColor = Text;
            foreach (ToolStripItem item in strip.Items) ApplyMenu(item);
        }
        if (control is DataGridView grid)
        {
            grid.BackgroundColor = Surface; grid.GridColor = Border; grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = SurfaceText; grid.DefaultCellStyle.SelectionBackColor = ChromeSelection; grid.DefaultCellStyle.SelectionForeColor = SelectionText;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Raised; grid.ColumnHeadersDefaultCellStyle.ForeColor = Text; grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Raised; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
            foreach (DataGridViewRow row in grid.Rows) if (Convert.ToString(row.Cells[0].Value) is "Changed" or "LeftOnly" or "RightOnly" or "Error") DifferenceRow(row.DefaultCellStyle, Convert.ToString(row.Cells[0].Value));
        }
        foreach (Control child in control.Controls) Apply(child);
        control.Invalidate();
    }
    static void ApplyMenu(ToolStripItem item)
    {
        item.BackColor = Raised; item.ForeColor = Text;
        if (item is ToolStripDropDownItem menu) { menu.DropDown.Renderer = new DarkMenuRenderer(); menu.DropDown.BackColor = Raised; menu.DropDown.ForeColor = Text; foreach (ToolStripItem child in menu.DropDownItems) ApplyMenu(child); }
    }
    sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using var pen = new Pen(Border); e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, e.ToolStrip.Width - 1), Math.Max(0, e.ToolStrip.Height - 1)); }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? e.Item.Selected ? SelectionText : Text : Muted; base.OnRenderItemText(e); }
    }
    sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Raised;
        public override Color MenuStripGradientBegin => Window;
        public override Color MenuStripGradientEnd => Window;
        public override Color ImageMarginGradientBegin => Raised;
        public override Color ImageMarginGradientMiddle => Raised;
        public override Color ImageMarginGradientEnd => Raised;
        public override Color MenuItemSelected => ChromeSelection;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelectedGradientBegin => ChromeSelection;
        public override Color MenuItemSelectedGradientEnd => ChromeSelection;
        public override Color MenuItemPressedGradientBegin => Raised;
        public override Color MenuItemPressedGradientMiddle => Raised;
        public override Color MenuItemPressedGradientEnd => Raised;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}

sealed class DarkTabs : TabControl
{
    public DarkTabs() { DrawMode = TabDrawMode.OwnerDrawFixed; Padding = new(15, 8); SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(DarkTheme.Window);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(DarkTheme.Window);
        for (int i = 0; i < TabCount; i++) OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i, i == SelectedIndex ? DrawItemState.Selected : DrawItemState.None));
        using var border = new Pen(DarkTheme.DecorativeBorder); var page = DisplayRectangle; page.Inflate(1, 1); e.Graphics.DrawRectangle(border, page);
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = e.Index == SelectedIndex; using var brush = new SolidBrush(selected ? DarkTheme.Raised : DarkTheme.Window); e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, e.Bounds, selected ? DarkTheme.Text : DarkTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (selected) { using var pen = new Pen(DarkTheme.ChromeAccent, Math.Max(2, DeviceDpi / 48)); e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 2, e.Bounds.Right, e.Bounds.Bottom - 2); }
        if (Focused && selected) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -4, -4), DarkTheme.Text, DarkTheme.Raised);
    }
}

sealed class DarkComboBox : ComboBox
{
    public DarkComboBox() { DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat; }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0; using var brush = new SolidBrush(selected ? DarkTheme.ChromeSelection : DarkTheme.Surface); e.Graphics.FillRectangle(brush, e.Bounds);
        if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, Rectangle.Inflate(e.Bounds, -4, 0), selected ? DarkTheme.SelectionText : DarkTheme.SurfaceText, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
}

sealed class DarkListBox : ListBox
{
    public DarkListBox() { DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 27; BorderStyle = BorderStyle.FixedSingle; }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ItemHeight = Font.Height + 8; }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0; using var brush = new SolidBrush(selected ? DarkTheme.ChromeSelection : DarkTheme.Surface); e.Graphics.FillRectangle(brush, e.Bounds);
        if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, Rectangle.Inflate(e.Bounds, -5, 0), selected ? DarkTheme.SelectionText : DarkTheme.SurfaceText, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
}

sealed class DarkGrid : DataGridView
{
    protected override void OnRowPostPaint(DataGridViewRowPostPaintEventArgs e)
    { base.OnRowPostPaint(e); if (Rows[e.RowIndex].Selected) { using var pen = new Pen(DarkTheme.Focus, Math.Max(1, DeviceDpi / 96)); var rectangle = e.RowBounds; rectangle.Inflate(-1, -1); e.Graphics.DrawRectangle(pen, rectangle); } }
}
