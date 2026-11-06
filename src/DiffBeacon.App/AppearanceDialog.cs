namespace DiffBeacon.App;

sealed class AppearanceDialog : DarkWindow
{
    readonly AppearanceStore? store;
    readonly Dictionary<AppearanceTheme, RadioButton> themes = new();
    readonly Dictionary<AppearanceAccent, RadioButton> accents = new();
    readonly CheckBox texture = new() { Text = "Workspace Texture", AutoSize = true };
    readonly Label status = new() { AutoSize = true, Visible = false };
    bool updatingChoices;

    AppearanceDialog(AppearanceStore? store)
    {
        this.store = store;
        Text = "Settings · Appearance";
        Font = new Font("Segoe UI", 10);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        Size = new(540, 300);
        MinimumSize = new(455, 285);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, Padding = new(12)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(RowLabel("Theme"), 0, 0);
        content.Controls.Add(Choices("Theme", themes, new[] { AppearanceTheme.System, AppearanceTheme.Light, AppearanceTheme.Dark },
            value => VaporGridAppearance.Current with { Theme = value }), 1, 0);
        content.Controls.Add(RowLabel("Accent Color"), 0, 1);
        content.Controls.Add(Choices("Accent Color", accents, Enum.GetValues<AppearanceAccent>(),
            value => VaporGridAppearance.Current with { Accent = value }), 1, 1);
        content.Controls.Add(texture, 1, 2);
        var hint = new Label { Text = "Square Tread Appears In Dark Mode.", AutoSize = true };
        content.Controls.Add(hint, 1, 3);
        content.Controls.Add(status, 0, 4);
        content.SetColumnSpan(status, 2);
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, AutoSize = true, Anchor = AnchorStyles.Right };
        content.Controls.Add(close, 1, 5);
        AcceptButton = CancelButton = close;
        WindowContent.Controls.Add(content);
        WindowContent.SizeChanged += (_, _) => status.MaximumSize = new(Math.Max(1, WindowContent.ClientSize.Width - 30), 0);
        content.SizeChanged += (_, _) => FitBody(content);
        Shown += (_, _) => FitBody(content);
        SynchronizeChoices();
        texture.CheckedChanged += (_, _) =>
            Save(VaporGridAppearance.Current with { Finish = texture.Checked ? WorkspaceFinish.SquareTread : WorkspaceFinish.Flat });
        ApplyChrome();
    }

    void FitBody(Control body)
    {
        if (!IsHandleCreated) return;
        int required = WindowContent.Top + body.Height + (Height - ClientSize.Height);
        int available = Screen.FromControl(this).WorkingArea.Height;
        WindowContent.AutoScroll = required > available;
        int minimumHeight = Math.Min(available, Math.Max(LogicalToDeviceUnits(285), required));
        if (MinimumSize.Height != minimumHeight) MinimumSize = new(MinimumSize.Width, minimumHeight);
    }

    static Label RowLabel(string text) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 6, 4)
    };

    FlowLayoutPanel Choices<T>(string accessibleName, Dictionary<T, RadioButton> buttons,
        IEnumerable<T> choices, Func<T, AppearanceOptions> select) where T : struct, Enum
    {
        var group = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, AccessibleName = accessibleName };
        foreach (T choice in choices)
        {
            var option = new AppearanceChoice { Text = choice.ToString(), Size = new(87, 32), Margin = new Padding(0, 3, 8, 3) };
            option.CheckedChanged += (_, _) => { if (option.Checked) Save(select(choice)); };
            buttons.Add(choice, option);
            group.Controls.Add(option);
        }
        return group;
    }

    void SynchronizeChoices()
    {
        updatingChoices = true;
        try
        {
            var options = VaporGridAppearance.Current;
            themes[options.Theme].Checked = true;
            accents[options.Accent].Checked = true;
            texture.Checked = options.Finish == WorkspaceFinish.SquareTread;
        }
        finally { updatingChoices = false; }
    }

    void Save(AppearanceOptions options)
    {
        if (updatingChoices) return;
        if (VaporGridAppearance.TrySet(options, store, out string? failure))
        {
            status.Visible = false;
            return;
        }
        SynchronizeChoices();
        status.Text = failure;
        status.ForeColor = DarkTheme.Danger;
        status.Visible = true;
    }

    public static void ShowFor(DarkWindow owner)
    {
        using var dialog = new AppearanceDialog(VaporGridAppearance.Store);
        dialog.ShowDialog(owner);
    }

    internal static void RunSmoke(DarkWindow owner, string screenshot)
    {
        if (VaporGridAppearance.Store != null) throw new Exception("Appearance smoke cannot use real preferences.");
        var original = VaporGridAppearance.Current;
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(screenshot))!, "Appearance-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "data", "appearance.json");
        var scratch = new AppearanceStore(path);
        var defaults = AppearanceOptions.Default;
        if (scratch.Load() != defaults || Directory.Exists(root)) throw new Exception("Missing appearance settings wrote portable state.");
        if (VaporGridAppearance.IsLightFor(AppearanceTheme.Dark, true) || !VaporGridAppearance.IsLightFor(AppearanceTheme.Light, false) ||
            VaporGridAppearance.IsLightFor(AppearanceTheme.System, false) || !VaporGridAppearance.IsLightFor(AppearanceTheme.System, true) ||
            DarkTheme.UsesTread(light: true, highContrast: false, WorkspaceFinish.SquareTread) ||
            DarkTheme.UsesTread(light: false, highContrast: true, WorkspaceFinish.SquareTread))
            throw new Exception("Appearance mode resolution or texture policy failed.");
        var amber = Color.FromArgb(0xF2, 0xCD, 0x82);
        foreach (var preset in new[] { AppearanceAccent.Blue, AppearanceAccent.Teal })
        {
            var indicator = VaporGridPalette.Cues(false, preset).Selection;
            if (DarkTheme.GridSelectionKeyline(amber, indicator) is not Color ink ||
                DarkTheme.ContrastRatio(amber, ink) < 3 || DarkTheme.ContrastRatio(indicator, ink) < 3)
                throw new Exception($"The {preset} selection marker needs a visible keyline on amber diff rows.");
        }
        try
        {
            using (var dialog = new AppearanceDialog(scratch))
            {
                dialog.Show(owner);
                dialog.Size = dialog.MinimumSize;
                CheckFit(dialog);
                if (Directory.Exists(root)) throw new Exception("Opening appearance settings wrote portable state.");
                dialog.themes[AppearanceTheme.Light].Checked = true;
                dialog.accents[AppearanceAccent.Blue].Checked = true;
                dialog.texture.Checked = false;
                var wanted = new AppearanceOptions(AppearanceTheme.Light, AppearanceAccent.Blue, WorkspaceFinish.Flat);
                if (scratch.Load() != wanted || VaporGridAppearance.Current != wanted ||
                    (!DarkTheme.HighContrast && (DarkTheme.Window.ToArgb() != unchecked((int)0xFFEDEDF0) ||
                    DarkTheme.SelectionIndicator.ToArgb() != unchecked((int)0xFF1558A6) || DarkTheme.Focus.ToArgb() != unchecked((int)0xFF327AD1) ||
                    owner.BackColor.ToArgb() != DarkTheme.Window.ToArgb())))
                    throw new Exception("Live Light/Blue selection or persisted values differ from the pinned tokens.");
                CheckFit(dialog);
                dialog.CapturePreview(Path.ChangeExtension(screenshot, ".appearance-light-blue.png"));
                dialog.themes[AppearanceTheme.Dark].Checked = true;
                dialog.accents[AppearanceAccent.Teal].Checked = true;
                dialog.texture.Checked = true;
                if (scratch.Load() != new AppearanceOptions(AppearanceTheme.Dark, AppearanceAccent.Teal, WorkspaceFinish.SquareTread))
                    throw new Exception("Dark, accent and workspace texture preferences are not independent.");
                CheckFit(dialog);
                dialog.CapturePreview(Path.ChangeExtension(screenshot, ".appearance-dark-teal.png"));
                if (!DarkTheme.HighContrast)
                {
                    using var tile = new Bitmap(64, 64);
                    using var graphics = Graphics.FromImage(tile);
                    DarkTheme.PaintWorkspace(graphics, new Rectangle(0, 0, 64, 64), 96);
                    if (tile.GetPixel(0, 0) == tile.GetPixel(1, 1) || tile.GetPixel(0, 0) != tile.GetPixel(32, 32) ||
                        DarkTheme.Focus.ToArgb() != unchecked((int)0xFF8FE4D9) || DarkTheme.SelectionIndicator.ToArgb() != unchecked((int)0xFF37B7AF))
                        throw new Exception("Dark tread or Teal state cues differ from the pinned tokens.");
                }
            }
            File.WriteAllText(path, "{\"version\":1,\"theme\":\"light\",\"accent\":\"unknown\",\"finish\":2}");
            if (scratch.Load() != defaults with { Theme = AppearanceTheme.Light }) throw new Exception("Invalid appearance fields did not fall back independently.");
            File.WriteAllText(path, "{\"version\":2,\"theme\":\"light\"}");
            if (scratch.Load() != defaults) throw new Exception("Unknown appearance schema was accepted.");
            File.WriteAllText(path, new string(' ', 2049));
            if (scratch.Load() != defaults) throw new Exception("Oversized appearance settings were accepted.");
            string blocker = Path.Combine(root, "blocked");
            File.WriteAllText(blocker, "preserve");
            using var denied = new AppearanceDialog(new AppearanceStore(Path.Combine(blocker, "appearance.json")));
            denied.Show(owner);
            denied.Size = denied.MinimumSize;
            var beforeFailure = VaporGridAppearance.Current;
            denied.themes[AppearanceTheme.Light].Checked = true;
            if (!denied.status.Visible || !denied.themes[beforeFailure.Theme].Checked || VaporGridAppearance.Current != beforeFailure || File.ReadAllText(blocker) != "preserve")
                throw new Exception("Unavailable appearance storage did not preserve the previous live setting and show an error.");
            CheckFit(denied);
            denied.CapturePreview(Path.ChangeExtension(screenshot, ".appearance-save-error.png"));
        }
        finally { VaporGridAppearance.Apply(original); }
        Console.WriteLine($"VaporGrid appearance, native fit and portable settings checks passed. Scratch: {root}");
    }

    static void CheckFit(AppearanceDialog dialog)
    {
        if (dialog.WindowContent.VerticalScroll.Visible || dialog.WindowContent.HorizontalScroll.Visible)
            throw new Exception("Appearance settings have a scrollbar at their minimum supported size.");
        CheckCommandBounds(dialog.WindowContent);
        if (dialog.status.Visible && !dialog.WindowContent.RectangleToScreen(dialog.WindowContent.ClientRectangle)
            .Contains(dialog.status.RectangleToScreen(dialog.status.ClientRectangle)))
            throw new Exception("Appearance settings clipped the save failure message.");
    }

    sealed class AppearanceChoice : RadioButton
    {
        internal AppearanceChoice()
        {
            Appearance = Appearance.Button;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var bounds = ClientRectangle;
            if (bounds.Width < 8 || bounds.Height < 8) return;
            using (var background = new SolidBrush(Checked ? DarkTheme.Selection : DarkTheme.Raised))
                e.Graphics.FillRectangle(background, bounds);
            using (var border = new Pen(DarkTheme.Border))
                e.Graphics.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
            if (Checked)
            {
                var indicator = DarkTheme.HighContrast ? SystemColors.HighlightText : DarkTheme.SelectionIndicator;
                using var marker = new SolidBrush(indicator);
                e.Graphics.FillRectangle(marker, 1, 3, Math.Max(2, DeviceDpi / 48), bounds.Height - 6);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Checked ? DarkTheme.SelectionText : DarkTheme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (Focused)
            {
                var color = Checked && DarkTheme.HighContrast ? SystemColors.HighlightText : DarkTheme.Focus;
                using var focus = new Pen(color, Math.Max(2, DeviceDpi / 48));
                e.Graphics.DrawRectangle(focus, Rectangle.Inflate(bounds, -4, -4));
            }
        }
    }
}
