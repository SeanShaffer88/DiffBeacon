namespace DiffBeacon.App;

// Views own their commands; MainForm only lends the active deck a visible host.
sealed class ComparisonCommandBar : UserControl
{
    readonly TabControl tabs = new DarkTabs { Dock = DockStyle.Fill, AccessibleName = "Comparison Commands" };
    bool arranging;
    internal string ActivePageName => tabs.SelectedTab?.Text ?? "none";
    internal IEnumerable<Control> Descendants()
    {
        static IEnumerable<Control> Walk(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Walk(child)) yield return nested; } }
        return Walk(this);
    }
    public ComparisonCommandBar()
    {
        Dock = DockStyle.Top; Height = 158; Controls.Add(tabs);
        Load += (_, _) => FitPages();
        FontChanged += (_, _) => FitPages();
        SizeChanged += (_, _) => FitPages();
        tabs.SelectedIndexChanged += (_, _) => FitPages();
    }
    public TabPage AddPage(string title)
    {
        var page = new TabPage(title);
        page.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new(3) });
        tabs.TabPages.Add(page); return page;
    }
    public void AddGroup(TabPage page, string title, int width, params Control[] controls)
    {
        var group = new GroupBox { Text = title, Width = width, Margin = new(3) };
        var flow = new FlowLayoutPanel { WrapContents = true };
        foreach (var control in controls) { control.Dock = DockStyle.None; flow.Controls.Add(control); }
        group.Controls.Add(flow); page.Controls[0].Controls.Add(group);
    }
    internal void FitPages()
    {
        if (arranging || Width <= 0 || tabs.SelectedTab is not { } page) return;
        arranging = true;
        try
        {
            // Hidden native tab pages can retain their initial, narrow layout.
            // Their preferred height must not reserve space in the active deck.
                var flow = (FlowLayoutPanel)page.Controls[0];
                flow.Width = Math.Max(1, Width - 12);
                foreach (Control group in flow.Controls)
                {
                    var commands = (FlowLayoutPanel)group.Controls[0];
                    int inset = LogicalToDeviceUnits(6), top = Font.Height + inset;
                    commands.Width = Math.Max(1, group.Width - 2 * inset);
                    commands.Height = commands.GetPreferredSize(new(commands.Width, 0)).Height;
                    commands.Location = new(inset, top); commands.PerformLayout();
                    group.Height = top + commands.Height + inset;
                }
                flow.PerformLayout();
                int contentHeight = flow.GetPreferredSize(new(flow.Width, 0)).Height;
            Height = contentHeight + tabs.ItemSize.Height + 14;
        }
        finally { arranging = false; }
    }
    internal void VisitPages(Action check)
    {
        int selected = tabs.SelectedIndex;
        try { foreach (TabPage page in tabs.TabPages) { tabs.SelectedTab = page; FitPages(); PerformLayout(); page.PerformLayout(); check(); } }
        finally { tabs.SelectedIndex = selected; }
    }
    internal void CheckBounds()
    {
        DarkWindow.CheckCommandBounds(this);
        void Check(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                if (child is Button or ComboBox or CheckBox or TextBox)
                {
                    Rectangle bounds = child.RectangleToScreen(child.ClientRectangle);
                    for (Control? ancestor = child.Parent; ancestor != null && ancestor != Parent; ancestor = ancestor.Parent)
                        if (!ancestor.RectangleToScreen(ancestor.ClientRectangle).Contains(bounds)) throw new Exception($"Command clipped by {ancestor.GetType().Name}: {child.Text}.");
                }
                Check(child);
            }
        }
        Check(this);
    }
    internal void CheckInitialLayout()
    {
        // Called on the first Shown, before visiting or resizing any command page.
        CheckBounds();
        var groups = Descendants().OfType<GroupBox>().Where(group => group.Visible).ToArray();
        if (groups.Length == 0) throw new Exception("Startup command groups are missing.");
        int usedBottom = groups.Max(group => PointToClient(group.PointToScreen(new(0, group.Height))).Y);
        if (Height - usedBottom > LogicalToDeviceUnits(30)) throw new Exception($"Startup command deck contains unused height: deck={Height}, contentBottom={usedBottom}.");
        Console.WriteLine($"Fresh startup deck: {Width}x{Height}, content bottom {usedBottom}.");
    }
}
