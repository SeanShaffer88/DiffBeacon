using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace DiffBeacon.App;

static class DarkTheme
{
    public static bool HighContrast => SystemInformation.HighContrast;
    public static Color Window => HighContrast ? SystemColors.Control : Color.FromArgb(23, 23, 25);
    public static Color Caption => HighContrast ? SystemColors.Control : Color.FromArgb(23, 23, 25);
    public static Color Surface => HighContrast ? SystemColors.Window : Color.FromArgb(32, 32, 35);
    public static Color Editor => HighContrast ? SystemColors.Window : Color.FromArgb(23, 23, 25);
    public static Color Raised => HighContrast ? SystemColors.Control : Color.FromArgb(41, 41, 45);
    public static Color Border => HighContrast ? SystemColors.WindowText : Color.FromArgb(119, 118, 128);
    public static Color Text => HighContrast ? SystemColors.ControlText : Color.FromArgb(241, 241, 243);
    public static Color SurfaceText => HighContrast ? SystemColors.WindowText : Text;
    public static Color Muted => HighContrast ? SystemColors.ControlText : Color.FromArgb(183, 183, 193);
    public static Color DecorativeBorder => HighContrast ? SystemColors.WindowText : Color.FromArgb(64, 64, 71);
    public static Color ChromeAccent => HighContrast ? SystemColors.Highlight : Color.FromArgb(214, 31, 69);
    public static Color ChromeSelection => HighContrast ? SystemColors.Highlight : Color.FromArgb(85, 28, 43);
    public static Color AccentHover => HighContrast ? SystemColors.Highlight : Color.FromArgb(162, 21, 51);
    public static Color Focus => HighContrast ? SystemColors.ControlText : Color.FromArgb(255, 77, 115);
    public static Color Selection => HighContrast ? SystemColors.Highlight : Color.FromArgb(85, 28, 43);
    public static Color SelectionText => HighContrast ? SystemColors.HighlightText : Color.White;
    public static Color Amber => HighContrast ? SystemColors.Window : Color.FromArgb(242, 205, 130);
    public static Color AmberText => HighContrast ? SystemColors.WindowText : Color.FromArgb(32, 35, 40);
    public static Color Success => HighContrast ? SystemColors.WindowText : Color.FromArgb(143, 212, 173);
    public static Color Danger => HighContrast ? SystemColors.WindowText : Color.FromArgb(255, 177, 168);
    public static Color Action => HighContrast ? SystemColors.Highlight : Color.FromArgb(184, 24, 58);
    static readonly ConditionalWeakTable<Button, ButtonVisualState> buttonStates = new();
    sealed class ButtonVisualState { public bool Hover, Pressed, Caption; }
    public static void Button(Button button)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Raised; button.FlatAppearance.MouseDownBackColor = Surface;
        button.BackColor = Raised; button.ForeColor = Text; button.UseVisualStyleBackColor = false;
        // Attach once. State is weakly keyed; no static subscription retains disposed controls.
        if (buttonStates.TryGetValue(button, out _)) { button.Invalidate(); return; }
        buttonStates.Add(button, new());
        button.Paint += PaintButton;
        button.MouseEnter += ButtonEnter; button.MouseLeave += ButtonLeave;
        button.MouseDown += ButtonDown; button.MouseUp += ButtonUp;
        button.KeyDown += ButtonKeyDown; button.KeyUp += ButtonKeyUp;
        button.MouseCaptureChanged += ButtonCaptureChanged;
        button.LostFocus += ButtonLostFocus; button.GotFocus += ButtonInvalidate;
        button.EnabledChanged += ButtonReset;
    }
    internal static void CaptionButton(Button button)
    { Button(button); if (buttonStates.TryGetValue(button, out var state)) state.Caption = true; }
    static void ButtonInvalidate(object? sender, EventArgs e) { if (sender is Button b) b.Invalidate(); }
    static void ButtonEnter(object? sender, EventArgs e) { if (sender is Button b && buttonStates.TryGetValue(b, out var s)) { s.Hover = true; b.Invalidate(); } }
    static void ButtonLeave(object? sender, EventArgs e) { if (sender is Button b && buttonStates.TryGetValue(b, out var s)) { s.Hover = false; b.Invalidate(); } }
    static void ButtonDown(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left && sender is Button b && buttonStates.TryGetValue(b, out var s)) { s.Pressed = true; b.Invalidate(); } }
    static void ButtonUp(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ButtonReset(sender, e); }
    static void ButtonKeyDown(object? sender, KeyEventArgs e) { if (e.KeyCode == Keys.Space && sender is Button b && buttonStates.TryGetValue(b, out var s)) { s.Pressed = true; b.Invalidate(); } }
    static void ButtonKeyUp(object? sender, KeyEventArgs e) { if (e.KeyCode == Keys.Space) ButtonReset(sender, e); }
    static void ButtonCaptureChanged(object? sender, EventArgs e) { if (sender is Button { Capture: false }) ButtonReset(sender, e); }
    static void ButtonLostFocus(object? sender, EventArgs e) => ButtonReset(sender, e);
    static void ButtonReset(object? sender, EventArgs e) { if (sender is Button b && buttonStates.TryGetValue(b, out var s)) { s.Pressed = false; if (!b.Enabled) s.Hover = false; b.Invalidate(); } }
    static void PaintButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button || !buttonStates.TryGetValue(button, out var state)) return;
        var bounds = button.ClientRectangle; if (bounds.Width < 2 || bounds.Height < 2) return;
        bool primary = !state.Caption && button.Text.Replace("&", "") == "Compare";
        bool pressed = state.Pressed && (state.Hover || !button.Capture);
        Color fill = !button.Enabled ? Surface : state.Caption ? state.Hover ? button.FlatAppearance.MouseOverBackColor : button.BackColor : primary ? AccentHover : state.Hover ? Raised : Surface;
        bool relief = button.Enabled && !HighContrast && !state.Caption && !state.Hover && !pressed;
        if (HighContrast) fill = SystemColors.Control;
        if (relief)
        { using var brush = new LinearGradientBrush(bounds, primary ? Action : Raised, primary ? AccentHover : Surface, LinearGradientMode.Vertical); e.Graphics.FillRectangle(brush, bounds); }
        else { using var brush = new SolidBrush(fill); e.Graphics.FillRectangle(brush, bounds); }
        var edge = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);
        if (!state.Caption || !button.Enabled)
        { using var pen = new Pen(Border) { DashStyle = button.Enabled ? DashStyle.Solid : DashStyle.Dash }; e.Graphics.DrawRectangle(pen, edge); }
        if (button.Enabled && !HighContrast && !state.Caption)
        {
            using var pen = new Pen(pressed ? Color.FromArgb(17, 17, 19) : DecorativeBorder);
            e.Graphics.DrawLine(pen, 1, 1, bounds.Width - 2, 1);
            if (pressed) e.Graphics.DrawLine(pen, 1, 1, 1, bounds.Height - 2);
        }
        // Default action is separate from keyboard focus: the prompt message can
        // own focus while Enter still invokes the safe default button.
        if (!state.Caption && IsDefaultAction(button))
        {
            using var defaultEdge = new Pen(Text);
            e.Graphics.DrawRectangle(defaultEdge, Rectangle.Inflate(edge, -1, -1));
        }
        var textBounds = Rectangle.Inflate(bounds, -3, -2);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
        if (!button.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, button.Text, button.Font, textBounds, button.Enabled ? Text : HighContrast ? SystemColors.GrayText : Muted, flags);
        if (button.Focused)
        {
            var focusBounds = Rectangle.Inflate(edge, -3, -3);
            // A contrasting inner keyline keeps focus legible over the primary crimson fill.
            if (primary && !HighContrast) { using var keyline = new Pen(Text, Math.Max(4, button.DeviceDpi / 24)); e.Graphics.DrawRectangle(keyline, focusBounds); }
            using var focus = new Pen(Focus, Math.Max(2, button.DeviceDpi / 48)); e.Graphics.DrawRectangle(focus, focusBounds);
        }
    }
    internal static bool IsDefaultAction(Button button)
    {
        if (!button.Enabled || button.FindForm() is not Form form) return false;
        Control? active = form.ActiveControl;
        while (active is ContainerControl container && container.ActiveControl != null) active = container.ActiveControl;
        return active is Button focused && focused.Focused ? ReferenceEquals(button, focused) : ReferenceEquals(form.AcceptButton, button);
    }
    internal static Color GridSelectionOutline(Color background, bool highContrast) => highContrast
        ? background.ToArgb() == SystemColors.Highlight.ToArgb() ? SystemColors.HighlightText : SystemColors.WindowText
        : ChromeAccent;
    public static void DifferenceRow(DataGridViewCellStyle style, string? status = "Changed")
    { bool sideOnly = status is "LeftOnly" or "RightOnly"; style.BackColor = sideOnly ? Selection : Amber; style.ForeColor = sideOnly ? SelectionText : AmberText; style.SelectionBackColor = style.BackColor; style.SelectionForeColor = style.ForeColor; }
    public static void Tooltip(ToolTip tip) { tip.BackColor = Raised; tip.ForeColor = Text; }
    public static void Apply(Control control)
    { if (control is CompareView view) { view.ApplyTheme(); return; } ApplyCore(control); }
    internal static void ApplyCore(Control control)
    {
        control.BackColor = control is RichTextBox ? Editor : control is TextBoxBase or ComboBox ? Surface : control is ListBox or Panel ? Surface : control is Button ? Raised : Window;
        control.ForeColor = control is RichTextBox or ListBox ? SurfaceText : Text;
        if (control is Label or CheckBox or RadioButton)
        {
            control.BackColor = Color.Transparent;
            if (control.Parent?.BackColor.ToArgb() == Surface.ToArgb()) control.ForeColor = SurfaceText;
        }
        if (control is Button button) Button(button);
        if (control is Panel panel) { panel.Paint -= PaintPanelRim; panel.Paint += PaintPanelRim; }
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
    static void PaintPanelRim(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || panel.ClientSize.Width < 2 || panel.ClientSize.Height < 2) return;
        using var rim = new Pen(HighContrast ? SystemColors.WindowText : DecorativeBorder);
        e.Graphics.DrawRectangle(rim, 0, 0, panel.ClientSize.Width - 1, panel.ClientSize.Height - 1);
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
        if (selected) { using var stripe = new SolidBrush(DarkTheme.ChromeAccent); e.Graphics.FillRectangle(stripe, e.Bounds.Left, e.Bounds.Top, Math.Max(2, DeviceDpi / 48), e.Bounds.Height); }
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
        if (selected) { using var stripe = new SolidBrush(DarkTheme.ChromeAccent); e.Graphics.FillRectangle(stripe, e.Bounds.Left, e.Bounds.Top, Math.Max(2, DeviceDpi / 48), e.Bounds.Height); }
        e.DrawFocusRectangle();
    }
}

sealed class DarkGrid : DataGridView
{
    protected override void OnRowPostPaint(DataGridViewRowPostPaintEventArgs e)
    { base.OnRowPostPaint(e); if (Rows[e.RowIndex].Selected) { using var pen = new Pen(DarkTheme.GridSelectionOutline(Rows[e.RowIndex].InheritedStyle.SelectionBackColor, DarkTheme.HighContrast), Math.Max(2, DeviceDpi / 48)); var rectangle = e.RowBounds; rectangle.Inflate(-1, -1); e.Graphics.DrawRectangle(pen, rectangle); } }
}
