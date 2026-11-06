using System.Text;
using DiffBeacon.Core;

namespace DiffBeacon.App;

public sealed class MainForm : DarkWindow
{
    public bool SmokeFailed { get; private set; }
    internal bool IsSmoke => smoke != null;
    internal void FailSmoke(Exception ex) { Console.Error.WriteLine(ex); SmokeFailed = true; }
    readonly TabControl tabs = new DarkTabs() { Dock = DockStyle.Fill };
    readonly Panel commandHost = new() { Dock = DockStyle.Top, Height = 136 };
    readonly string? smoke;
    Rectangle? firstRestoreBounds;
    Rectangle? startupMaximizedBounds;
    public MainForm(string mode, string left, string right, string? smokePath)
    {
        smoke = smokePath; Text = "DiffBeacon — Compare With Clarity"; MinimumSize = new(900, 620); Font = new("Segoe UI", 10); StartPosition = FormStartPosition.Manual;
        var startupScreen = Screen.FromPoint(Cursor.Position);
        var startupBounds = StartupRestoreBounds(startupScreen.WorkingArea, MinimumSize);
        Bounds = startupBounds;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File"); file.DropDownItems.Add("New Comparison", null, (_, _) => Add("Text", "", "")); file.DropDownItems.Add("New Three-Way Merge", null, (_, _) => AddThreeWay()); file.DropDownItems.Add("Open Session…", null, (_, _) => OpenSession()); file.DropDownItems.Add("Close Tab", null, (_, _) => CloseTab()); file.DropDownItems.Add("Exit", null, (_, _) => Close()); menu.Items.Add(file);
        menu.Items.Add("About", null, (_, _) => DarkPrompt.Show(this, "DiffBeacon · Core Preview\nNative, local-first file comparison.\nSee docs/FEATURES.md for current limits.\nNo automatic network access or telemetry.", "About DiffBeacon"));
        WindowContent.Controls.Add(tabs); WindowContent.Controls.Add(commandHost); WindowContent.Controls.Add(menu);
        tabs.SelectedIndexChanged += (_, _) => HostCommands(); MainMenuStrip = menu;
        Add(mode, left, right); HostCommands();
        FormClosing += (_, e) => { if (IsSmoke) return; foreach (TabPage tab in tabs.TabPages) { if (!CanCloseTab(tab)) { e.Cancel = true; return; } } };
        firstRestoreBounds = startupBounds;
        WindowState = FormWindowState.Maximized;
        Shown += async (_, _) =>
        {
            if (smoke == null) return;
            try
            {
                var smallWorkArea = new Rectangle(-1024, 40, 1024, 720);
                if (StartupRestoreBounds(smallWorkArea, new(900, 620)) != smallWorkArea ||
                    StartupRestoreBounds(new(0, 0, 1920, 1040), new(900, 620)) != new Rectangle(330, 95, 1260, 850))
                    throw new Exception("Startup restore bounds did not fit the small or large work-area fixture.");
                CheckMaximizedClientBounds();
                var initialView = (CompareView)tabs.SelectedTab!.Controls[0];
                initialView.CommandBar.CheckInitialLayout();
                CapturePreview(Path.ChangeExtension(smoke, ".startup-maximized.png"));
                if (Screen.FromHandle(Handle).DeviceName != startupScreen.DeviceName || RestoreBounds.Size != startupBounds.Size)
                    throw new Exception($"Maximized startup changed the monitor or restore size: {RestoreBounds}, expected {startupBounds} on {startupScreen.DeviceName}.");
                WindowState = FormWindowState.Normal;
                await Task.Delay(60);
                if (WindowState != FormWindowState.Normal || FormBorderStyle != FormBorderStyle.Sizable ||
                    Size != startupBounds.Size || Width < MinimumSize.Width || Height < MinimumSize.Height)
                    throw new Exception($"Maximized startup failed to restore the resizable window: {Bounds}, expected {startupBounds}, minimum {MinimumSize}.");
                if (startupScreen.WorkingArea.Width >= MinimumSize.Width && startupScreen.WorkingArea.Height >= MinimumSize.Height &&
                    (!startupScreen.WorkingArea.Contains(Bounds) || !startupScreen.WorkingArea.Contains(RectangleToScreen(ClientRectangle))))
                    throw new Exception($"Restored window or client is clipped by the work area: {Bounds}, work area {startupScreen.WorkingArea}.");
                Console.WriteLine($"Startup restore: {Bounds}, work area {startupScreen.WorkingArea}.");
                CapturePreview(Path.ChangeExtension(smoke, ".startup.png"));
                initialView.CommandBar.CheckInitialLayout();
                await SmokeChrome();
                DarkPrompt.Smoke(this, Path.ChangeExtension(smoke, ".prompt.png"));
                SmokeSessionOwnership();
                var view = (CompareView)tabs.SelectedTab!.Controls[0];
                await view.RunSmoke(smoke);
                var threeWay = AddThreeWay();
                await threeWay.RunSmoke(Path.ChangeExtension(smoke, ".three-way.png"));
                CloseTab();
            }
            catch (Exception ex) { FailSmoke(ex); }
            Close();
        };
    }
    static Rectangle StartupRestoreBounds(Rectangle workArea, Size minimum)
    {
        int width = Math.Clamp(1260, minimum.Width, Math.Max(minimum.Width, workArea.Width));
        int height = Math.Clamp(850, minimum.Height, Math.Max(minimum.Height, workArea.Height));
        return new(workArea.Left + Math.Max(0, (workArea.Width - width) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - height) / 2), width, height);
    }
    protected override void WndProc(ref Message message)
    {
        const int wmSize = 0x0005, sizeRestored = 0, sizeMaximized = 2;
        base.WndProc(ref message);
        if (message.Msg != wmSize || firstRestoreBounds is not { } restore) return;
        if (message.WParam == sizeMaximized)
        {
            startupMaximizedBounds = Bounds;
            return;
        }
        if (message.WParam != sizeRestored || startupMaximizedBounds is not { } maximized || WindowState != FormWindowState.Normal) return;
        firstRestoreBounds = null;
        var workArea = Screen.FromRectangle(maximized).WorkingArea;
        if (!workArea.Contains(restore)) restore = StartupRestoreBounds(workArea, MinimumSize);
        // A small monitor can leave the maximized frame rectangle in place during
        // the first restore. Let native placement or caption drag finish first.
        BeginInvoke(() =>
        {
            if (!IsDisposed && WindowState == FormWindowState.Normal && Bounds == maximized)
                Bounds = restore;
        });
    }
    void HostCommands()
    {
        commandHost.Controls.Clear();
        if (tabs.SelectedTab?.Controls[0] is CompareView view)
        {
            commandHost.Controls.Add(view.CommandBar);
            view.CommandBar.FitPages(); commandHost.Height = view.CommandBar.Height;
            commandHost.Visible = true;
        }
        else commandHost.Visible = false;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) commandHost.Controls.Clear();
        base.Dispose(disposing);
    }
    void Add(string mode, string left, string right)
    { var tab = new TabPage($"{mode} {tabs.TabCount + 1}"); var view = new CompareView(mode, left, right) { Dock = DockStyle.Fill }; view.CommandBar.SizeChanged += (_, _) => { if (view.CommandBar.Parent == commandHost) commandHost.Height = view.CommandBar.Height; }; tab.Controls.Add(view); DarkTheme.Apply(tab); tabs.TabPages.Add(tab); tabs.SelectedTab = tab; }
    ThreeWayMergeView AddThreeWay() { var tab = new TabPage("Three-Way Merge"); var view = new ThreeWayMergeView(); tab.Controls.Add(view); DarkTheme.Apply(tab); tabs.TabPages.Add(tab); tabs.SelectedTab = tab; return view; }
    bool CanCloseTab(TabPage tab) => IsSmoke || tab.Controls[0] switch { CompareView twoWay => twoWay.CanClose(), ThreeWayMergeView threeWay => threeWay.CanClose(), _ => true };
    void CloseTab() { if (tabs.SelectedTab is { } tab && CanCloseTab(tab)) { tabs.TabPages.Remove(tab); tab.Dispose(); if (tabs.TabCount == 0) Add("Text", "", ""); } }
    void OpenSession()
    {
        using var dialog = new OpenFileDialog { Filter = "DiffBeacon Session|*.json" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { var s = Session.Load(dialog.FileName); Add(s.Mode, s.Left, s.Right); ((CompareView)tabs.SelectedTab!.Controls[0]).ApplySession(s); }
        catch (Exception ex) { DarkPrompt.Show(this, ex.Message, "Session Could Not Be Opened"); }
    }
    void SmokeSessionOwnership()
    {
        var originalTab = tabs.SelectedTab!; var original = (CompareView)originalTab.Controls[0];
        Add("Text", "", ""); var secondTab = tabs.SelectedTab!; var second = (CompareView)secondTab.Controls[0];
        void CheckHost(CompareView expected)
        {
            if (!commandHost.Visible || commandHost.Controls.Count != 1 || commandHost.Controls[0] != expected.CommandBar || expected.CommandBar.IsDisposed) throw new Exception("Session selected another view's command deck.");
            expected.CheckHostedCommand();
        }
        for (int i = 0; i < 3; i++) { tabs.SelectedTab = originalTab; CheckHost(original); tabs.SelectedTab = secondTab; CheckHost(second); }
        Add("Text", "", ""); var inactiveTab = tabs.SelectedTab!; var inactive = (CompareView)inactiveTab.Controls[0];
        tabs.SelectedTab = secondTab; tabs.TabPages.Remove(inactiveTab); inactiveTab.Dispose();
        if (!inactive.IsDisposed || !inactive.CommandBar.IsDisposed || second.CommandBar.IsDisposed) throw new Exception("Inactive session did not release its own deck exclusively.");
        CheckHost(second); CloseTab(); CheckHost(original);
        if (!second.IsDisposed || !second.CommandBar.IsDisposed) throw new Exception("Closing active session leaked its deck.");
        AddThreeWay(); if (commandHost.Visible || commandHost.Controls.Count != 0) throw new Exception("Two-way commands appeared in a three-way session.");
        CloseTab(); CheckHost(original);
        Console.WriteLine("Session command routing, active/inactive disposal and three-way isolation: passed.");
    }
}

sealed class CompareView : UserControl
{
    readonly ComboBox mode = new DarkComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, AccessibleName = "Comparison Mode" };
    readonly TextBox leftPath = new() { Dock = DockStyle.Fill }, rightPath = new() { Dock = DockStyle.Fill };
    readonly ComparisonEditor leftText = Editor(), rightText = Editor();
    readonly DataGridView results = new DarkGrid() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = DarkTheme.Surface };
    readonly SplitContainer editors = new() { Dock = DockStyle.Fill, SplitterWidth = 6 };
    internal ComparisonCommandBar CommandBar { get; } = new();
    readonly SplitContainer workspace = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
    readonly Panel sharedPaths = new() { Dock = DockStyle.Top, Height = 82 };
    readonly CheckBox stacked = new() { Text = "Stacked Editors", AutoSize = true };
    readonly CheckBox showResults = new() { Text = "Show Results", AutoSize = true, Checked = true };
    readonly Panel leftHeader, rightHeader;
    double editorProportion = .5, resultProportion = .8;
    bool arranging;
    readonly DifferenceOverview scrubber = new();
    readonly Panel emptyState = new() { Dock = DockStyle.Fill, BackColor = DarkTheme.Window };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 38, Padding = new(12, 9, 0, 0), BackColor = DarkTheme.Caption, ForeColor = DarkTheme.Muted, AutoEllipsis = true };
    readonly CheckBox ignoreCase = new() { Text = "Ignore Case", AutoSize = true }, ignoreSpace = new() { Text = "Ignore Whitespace", AutoSize = true }, ignoreBlank = new() { Text = "Ignore Blank Lines", AutoSize = true }, wrap = new() { Text = "Word Wrap", AutoSize = true };
    readonly TextBox delimiter = new() { Text = ",", Width = 28 };
    readonly TextBox fileFilter = new() { Dock = DockStyle.Fill, MaxLength = 4096, PlaceholderText = "*.cs;NOT *.g.cs", AccessibleName = "File Filter" }, ignoredDirectories = new() { Dock = DockStyle.Fill, MaxLength = 4096, PlaceholderText = ".git;bin;obj", AccessibleName = "Ignore Directories" };
    readonly TableLayoutPanel folderFilters = new() { Dock = DockStyle.Top, Height = 72, ColumnCount = 2, RowCount = 2, Padding = new(10, 4, 10, 4), Visible = false };
    readonly Label folderScope = new() { Dock = DockStyle.Top, Height = 62, Padding = new(12, 5, 12, 4), Visible = false, AccessibleName = "Folder Comparison Scope" };
    readonly ToolTip help = new();
    readonly Button compare = new() { Text = "Compare", AutoSize = true };
    readonly Button cancel;
    readonly Button blockToRight = new() { Text = "Block → Right", AutoSize = true, Enabled = false, AccessibleName = "Merge Aligned Block Into Right Editor" }, blockToLeft = new() { Text = "Left ← Block", AutoSize = true, Enabled = false, AccessibleName = "Merge Aligned Block Into Left Editor" };
    readonly Stack<(string Left, string Right)> undo = new();
    readonly ComboBox granularity = new DarkComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 105, AccessibleName = "Highlight Granularity" };
    readonly CheckBox syncScrolling = new() { Text = "Sync scrolling", AutoSize = true, Checked = true };
    readonly FlowLayoutPanel textControls = new() { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
    CancellationTokenSource? detailCancellation;
    long detailGeneration;
    InlineDiffResult? detail;
    TextScrollMap? scrollMap;
    bool syncing, completedResult, hasDecoration, decorationMayBeInUndo;
    bool clearQueued;
    bool SmokeOwned => FindForm() is MainForm { IsSmoke: true };
    const int MaxPaintSpans = 20000;
    TextDocument? leftDoc, rightDoc;
    TextResult? textResult; FolderResult? folderResult;
    string? comparedLeft, comparedRight;
    CompareOptions? comparedOptions;
    CancellationTokenSource? cancellation;
    bool dirtyLeft, dirtyRight, suppress, disposed;
    string summary = "Choose Files Or Paste Text, Then Compare";
    List<string?[]> reportRows = new();

    static ComparisonEditor Editor() => new() { Dock = DockStyle.Fill, Font = new("Consolas", 11), AcceptsTab = true, WordWrap = false, DetectUrls = false, BorderStyle = BorderStyle.None, BackColor = Color.White, MaxLength = TextComparison.MaxCharacters / 2, HideSelection = false };
    static void StyleButton(Button button) => DarkTheme.Button(button);
    public CompareView(string initialMode, string left, string right)
    {
        leftText.AccessibleName = "Left Text"; rightText.AccessibleName = "Right Text";
        BackColor = DarkTheme.Window; ForeColor = DarkTheme.Text; mode.Items.AddRange(["Text", "Folders", "Binary", "Tables"]); mode.SelectedItem = initialMode; StyleButton(compare);
        results.BackgroundColor = DarkTheme.Surface; results.GridColor = DarkTheme.DecorativeBorder; results.DefaultCellStyle.BackColor = DarkTheme.Surface; results.DefaultCellStyle.ForeColor = DarkTheme.Text; results.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 118, 128); results.DefaultCellStyle.SelectionForeColor = Color.White; results.ColumnHeadersDefaultCellStyle.BackColor = DarkTheme.Raised; results.ColumnHeadersDefaultCellStyle.ForeColor = DarkTheme.Text; results.EnableHeadersVisualStyles = false;
        leftPath.Text = left; rightPath.Text = right;
        Panel PathHeader(string label, TextBox path)
        {
            var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 38, ColumnCount = 3, RowCount = 1, Padding = new(6, 4, 6, 4) };
            header.ColumnStyles.Add(new(SizeType.Absolute, 52));
            header.ColumnStyles.Add(new(SizeType.Percent, 100));
            header.ColumnStyles.Add(new(SizeType.Absolute, 90));
            path.MinimumSize = Size.Empty;
            var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill, Margin = new(4, 0, 0, 0) };
            StyleButton(browse); browse.Click += (_, _) => Browse(path);
            header.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            header.Controls.Add(path, 1, 0); header.Controls.Add(browse, 2, 0);
            return header;
        }
        leftHeader = PathHeader("Left", leftPath); rightHeader = PathHeader("Right", rightPath);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new(10, 4, 0, 0), WrapContents = true, AutoScroll = false }; tools.Controls.Add(mode); tools.Controls.Add(compare); compare.Click += async (_, _) => await Compare();
        tools.BackColor = BackColor; Button Tool(string title, Action action) { var button = new Button { Text = title, AutoSize = true }; StyleButton(button); button.Click += (_, _) => { try { action(); } catch (Exception ex) { Error(ex); } }; tools.Controls.Add(button); return button; }
        cancel = Tool("Cancel", () => cancellation?.Cancel()); cancel.Enabled = false; Tool("Swap", Swap); Tool("New Snippets", NewSnippets); Tool("Save Session…", SaveSession); Tool("Export HTML…", Export);
        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new(10, 5, 0, 0), WrapContents = true, AutoScroll = false, BackColor = BackColor }; foreach (var check in new[] { ignoreCase, ignoreSpace, ignoreBlank, wrap }) { check.ForeColor = ForeColor; check.BackColor = BackColor; } delimiter.BackColor = DarkTheme.Surface; delimiter.ForeColor = ForeColor; var separator = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty }; separator.Controls.AddRange([new Label { Text = "Table Separator", AutoSize = true, ForeColor = ForeColor, BackColor = BackColor }, delimiter]); options.Controls.AddRange([ignoreCase, ignoreSpace, ignoreBlank, wrap, separator]);
        granularity.Items.AddRange(["Line", "Word", "Character"]); granularity.SelectedIndex = 0;
        textControls.Controls.AddRange([new Label { Text = "Highlight", AutoSize = true, ForeColor = ForeColor, Padding = new(0, 4, 0, 0) }, granularity, syncScrolling]); options.Controls.Add(textControls);
        syncScrolling.ForeColor = ForeColor; syncScrolling.BackColor = BackColor;
        help.SetToolTip(syncScrolling, "Maps fresh exact comparison lines in both directions. Stale or positional comparisons scroll independently. Toggling does not move either pane.");
        granularity.SelectedIndexChanged += async (_, _) => { try { await ColorTextAsync(); } catch (Exception ex) { Error(ex); } };
        leftText.ViewportChanged += (_, _) => { SyncViewport(leftText, rightText, true); QueueVisibleDecoration(); };
        rightText.ViewportChanged += (_, _) => { SyncViewport(rightText, leftText, false); QueueVisibleDecoration(); };
        leftText.Resize += (_, _) => QueueVisibleDecoration(); rightText.Resize += (_, _) => QueueVisibleDecoration();
        wrap.CheckedChanged += (_, _) => { leftText.WordWrap = rightText.WordWrap = wrap.Checked; QueueVisibleDecoration(); };
        folderFilters.ColumnStyles.Add(new(SizeType.Absolute, 150)); folderFilters.ColumnStyles.Add(new(SizeType.Percent, 100));
        folderFilters.Controls.Add(new Label { Text = "File Filter", AutoSize = true, Padding = new(0, 4, 0, 0) }, 0, 0); folderFilters.Controls.Add(fileFilter, 1, 0);
        folderFilters.Controls.Add(new Label { Text = "Ignore Directories", AutoSize = true, Padding = new(0, 4, 0, 0) }, 0, 1); folderFilters.Controls.Add(ignoredDirectories, 1, 1);
        foreach (var field in new[] { fileFilter, ignoredDirectories }) { field.BackColor = DarkTheme.Surface; field.ForeColor = ForeColor; field.TextChanged += (_, _) => InvalidateComparison(); }
        help.SetToolTip(fileFilter, "Semicolon-separated basename globs at every depth. * and ? only. Positive clauses are OR; NOT followed by a space excludes. Exclusions win. No positive clauses means *. *.* requires a dot. Blank means all files. No paths, **, [], or {}.");
        help.SetToolTip(ignoredDirectories, "Semicolon-separated exact directory names, ignoring case, below each selected root. Example .git;bin;obj. No default exclusions, wildcards, or paths. The selected root itself is never excluded.");
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new(10, 3, 0, 0), WrapContents = true, AutoScroll = false, BackColor = BackColor };
        void Action(string title, System.Action action) { var b = new Button { Text = title, AutoSize = true }; StyleButton(b); b.Click += (_, _) => { try { action(); } catch (Exception ex) { Error(ex); } }; actions.Controls.Add(b); }
        Action("Previous Difference", () => Navigate(-1)); Action("Next Difference", () => Navigate(1)); Action("Selected → Right", () => Merge(true, false)); Action("Left ← Selected", () => Merge(false, false));
        foreach (var (button, toRight) in new[] { (blockToRight, true), (blockToLeft, false) }) { StyleButton(button); actions.Controls.Add(button); button.Click += (_, _) => { try { MergeBlock(toRight); } catch (Exception ex) { Error(ex); } }; string guidance = "Merge the entire contiguous aligned difference around the selected overview/Previous/Next difference. Requires a fresh exact Text comparison with all ignore options off. Changes only the destination editor; review and Save As to write."; button.AccessibleDescription = guidance; help.SetToolTip(button, guidance); }
        Action("All → Right", () => Merge(true, true)); Action("Undo Merge", Undo); Action("Save Left As…", () => Save(false)); Action("Save Right As…", () => Save(true));
        editors.Orientation = Orientation.Vertical; editors.Panel1.Controls.Add(leftText); editors.Panel2.Controls.Add(rightText);
        editors.Panel1.Controls.Add(leftHeader); editors.Panel2.Controls.Add(rightHeader);
        leftText.BackColor = rightText.BackColor = DarkTheme.AmberText; leftText.ForeColor = rightText.ForeColor = DarkTheme.Text; editors.Panel1.BackColor = editors.Panel2.BackColor = DarkTheme.Window;
        scrubber.DifferenceRequested += SetSelectedDifference;
        workspace.Panel1.Controls.Add(editors); workspace.Panel2.Controls.Add(results);
        var welcome = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new(16) };
        welcome.ColumnStyles.Add(new(SizeType.Percent, 50)); welcome.ColumnStyles.Add(new(SizeType.Percent, 50));
        welcome.RowStyles.Add(new(SizeType.Percent, 100)); welcome.RowStyles.Add(new(SizeType.Absolute, 44));
        void StartupTarget(string side, TextBox path, int column)
        {
            var target = new Button { Text = $"Open {side} File…\nOr Drop A File Here", Dock = DockStyle.Fill, AllowDrop = true, Margin = new(8) };
            StyleButton(target); target.Click += (_, _) => Browse(path);
            target.DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            target.DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) path.Text = files[0]; };
            path.TextChanged += (_, _) => { target.Text = path.Text.Length == 0 ? $"Open {side} File…\nOr Drop A File Here" : $"{side}: {Path.GetFileName(path.Text)}\nChoose Another File…"; help.SetToolTip(target, path.Text); };
            welcome.Controls.Add(target, column, 0);
        }
        StartupTarget("Left", leftPath, 0); StartupTarget("Right", rightPath, 1);
        var paste = new Button { Text = "Paste Snippets", AutoSize = true, Anchor = AnchorStyles.None };
        StyleButton(paste); paste.Click += (_, _) => { emptyState.Visible = false; leftText.Focus(); if (Clipboard.ContainsText()) leftText.Paste(); status.Text = "Paste Text Into Either Pane, Then Compare"; };
        welcome.Controls.Add(paste, 0, 1); welcome.SetColumnSpan(paste, 2); emptyState.Controls.Add(welcome);
        var workspaceHost = new Panel { Dock = DockStyle.Fill }; workspaceHost.Controls.Add(workspace); emptyState.Dock = DockStyle.Fill; workspaceHost.Controls.Add(emptyState); emptyState.BringToFront();
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false, Padding = new(6, 0, 0, 0) };
        var navigationControls = actions.Controls.Cast<Control>().Take(2).ToArray();
        navigation.Controls.AddRange(navigationControls);
        navigation.Controls.Add(showResults);
        var home = CommandBar.AddPage("Home");
        var homeCommands = tools.Controls.Cast<Control>().ToArray();
        CommandBar.AddGroup(home, "Compare", 220, homeCommands.Take(4).ToArray());
        CommandBar.AddGroup(home, "Session", 255, homeCommands.Skip(4).ToArray());
        CommandBar.AddGroup(home, "Options", 335, ignoreCase, ignoreBlank, ignoreSpace, separator);
        var merge = CommandBar.AddPage("Merge");
        var mergeCommands = actions.Controls.Cast<Control>().ToArray();
        CommandBar.AddGroup(merge, "Apply Changes", 380, mergeCommands.Take(5).ToArray());
        CommandBar.AddGroup(merge, "History", 125, mergeCommands[5]);
        CommandBar.AddGroup(merge, "Save", 180, mergeCommands.Skip(6).ToArray());
        var viewPage = CommandBar.AddPage("View");
        CommandBar.AddGroup(viewPage, "Layout", 220, stacked, wrap);
        CommandBar.AddGroup(viewPage, "Differences", 200, new Label { Text = "Highlight", AutoSize = true }, granularity);
        CommandBar.AddGroup(viewPage, "Scrolling", 185, syncScrolling);
        tools.Dispose(); options.Dispose(); actions.Dispose();
        Controls.Add(workspaceHost); Controls.Add(folderScope); Controls.Add(scrubber); Controls.Add(navigation); Controls.Add(folderFilters); Controls.Add(sharedPaths); Controls.Add(status);
        stacked.CheckedChanged += (_, _) => SetEditorLayout(stacked.Checked);
        showResults.CheckedChanged += (_, _) => { workspace.Panel2Collapsed = !showResults.Checked && Mode == "Text"; ReflowSplits(); };
        editors.SizeChanged += (_, _) => ReflowSplits(); workspace.SizeChanged += (_, _) => ReflowSplits();
        // SplitterMoved also fires during automatic WinForms resizing. Only drag
        // proposals update user proportions; programmatic clamps must not drift them.
        editors.SplitterMoving += (_, e) => { if (!arranging && EditorAxis > 0) editorProportion = (double)(editors.Orientation == Orientation.Vertical ? e.SplitX : e.SplitY) / EditorAxis; };
        workspace.SplitterMoving += (_, e) => { if (!arranging && workspace.Height > workspace.SplitterWidth) resultProportion = (double)e.SplitY / (workspace.Height - workspace.SplitterWidth); };
        leftText.TextChanged += (_, _) => { if (!suppress) { dirtyLeft = true; InvalidateComparison(); UpdateEmptyState(); status.Text = "Left Edited · Compare Again Before Navigating Differences"; } };
        rightText.TextChanged += (_, _) => { if (!suppress) { dirtyRight = true; InvalidateComparison(); UpdateEmptyState(); status.Text = "Right Edited · Compare Again Before Navigating Differences"; } };
        foreach (var path in new[] { leftPath, rightPath }) path.TextChanged += (_, _) => InvalidateComparison();
        foreach (var option in new[] { ignoreCase, ignoreSpace, ignoreBlank }) option.CheckedChanged += (_, _) => InvalidateComparison();
        delimiter.TextChanged += (_, _) => InvalidateComparison();
        void ModeLayout()
        {
            bool text = Mode == "Text";
            sharedPaths.Visible = !text;
            if (text) { editors.Panel1.Controls.Add(leftHeader); editors.Panel2.Controls.Add(rightHeader); }
            else { sharedPaths.Controls.Add(rightHeader); sharedPaths.Controls.Add(leftHeader); leftHeader.BringToFront(); }
            scrubber.Visible = text;
            workspace.Panel2Collapsed = false;
            workspace.Panel1Collapsed = !text;
            if (text) workspace.Panel2Collapsed = !showResults.Checked;
            UpdateMode();
            stacked.Enabled = wrap.Enabled = granularity.Enabled = syncScrolling.Enabled = text;
            separator.Visible = Mode == "Tables"; CommandBar.FitPages(); ReflowSplits();
        }
        mode.SelectedIndexChanged += (_, _) => { InvalidateComparison(); ModeLayout(); }; ModeLayout();
        foreach (var box in new[] { leftPath, rightPath }) { box.AllowDrop = true; box.DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; }; box.DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) box.Text = files[0]; }; }
        results.CellDoubleClick += (_, e) => { if (Mode == "Folders" && folderResult != null && e.RowIndex >= 0) { string relative = Convert.ToString(results.Rows[e.RowIndex].Cells[1].Value) ?? ""; var row = folderResult.Rows.FirstOrDefault(r => r.RelativePath == relative); if (row != null && row.Status != Change.Error) { var l = Path.Combine(leftPath.Text, row.RelativePath); var r = Path.Combine(rightPath.Text, row.RelativePath); if (File.Exists(l) && File.Exists(r) && CanClose()) { leftPath.Text = l; rightPath.Text = r; leftDoc = rightDoc = null; mode.SelectedItem = "Text"; } } } };
        DarkTheme.Apply(this); DarkTheme.Apply(CommandBar); DarkTheme.Tooltip(help);
    }
    int EditorAxis => (editors.Orientation == Orientation.Vertical ? editors.ClientSize.Width : editors.ClientSize.Height) - editors.SplitterWidth;
    void ReflowSplits()
    {
        if (arranging || disposed) return;
        arranging = true;
        try
        {
            FitWorkspace();
        }
        finally { arranging = false; }
    }
    void FitWorkspace()
    {
        int paneHeight = leftHeader.Height + 2 * leftText.Font.Height;
        int editorHeight = editors.Orientation == Orientation.Horizontal ? paneHeight * 2 + editors.SplitterWidth : paneHeight;
        int resultsHeight = results.ColumnHeadersHeight + results.RowTemplate.Height + 2 * SystemInformation.BorderSize.Height;
        SetSplit(workspace, resultProportion, editorHeight, resultsHeight);
        SetSplit(editors, editorProportion, editors.Orientation == Orientation.Horizontal ? paneHeight : 160, editors.Orientation == Orientation.Horizontal ? paneHeight : 160);
    }
    static void SetSplit(SplitContainer split, double proportion, int firstMinimum = 0, int secondMinimum = 0)
    {
        if (split.Panel1Collapsed || split.Panel2Collapsed) return;
        int available = (split.Orientation == Orientation.Vertical ? split.ClientSize.Width : split.ClientSize.Height) - split.SplitterWidth;
        if (available <= 0 || available < split.Panel1MinSize + split.Panel2MinSize) return;
        int first = Math.Max(split.Panel1MinSize, firstMinimum), second = Math.Max(split.Panel2MinSize, secondMinimum);
        if (first + second > available) { first = split.Panel1MinSize; second = split.Panel2MinSize; }
        split.SplitterDistance = Math.Clamp((int)(available * proportion), first, available - second);
    }
    void SetEditorLayout(bool verticalStack)
    {
        var leftAnchor = leftText.CaptureViewport(); var rightAnchor = rightText.CaptureViewport();
        bool previousSyncing = syncing; syncing = true; arranging = true;
        try
        {
            editors.Orientation = verticalStack ? Orientation.Horizontal : Orientation.Vertical;
            FitWorkspace();
            leftText.RestoreViewport(leftAnchor); rightText.RestoreViewport(rightAnchor);
        }
        finally { syncing = previousSyncing; arranging = false; }
        QueueVisibleDecoration();
    }
    string Mode => (string)mode.SelectedItem!;
    internal void ApplyTheme()
    {
        int leftStart = leftText.SelectionStart, leftLength = leftText.SelectionLength, rightStart = rightText.SelectionStart, rightLength = rightText.SelectionLength, selected = differenceIndex;
        bool previous = suppress; suppress = true;
        try
        {
            DarkTheme.ApplyCore(this); DarkTheme.ApplyCore(CommandBar); DarkTheme.Tooltip(help);
            if (textResult != null) { ColorText(); if (selected >= 0) SetSelectedDifference(selected); }
            leftText.Select(Math.Min(leftStart, leftText.TextLength), Math.Min(leftLength, Math.Max(0, leftText.TextLength - leftStart)));
            rightText.Select(Math.Min(rightStart, rightText.TextLength), Math.Min(rightLength, Math.Max(0, rightText.TextLength - rightStart)));
        }
        finally { suppress = previous; }
    }
    void UpdateEmptyState() => emptyState.Visible = Mode == "Text" && textResult == null && leftText.TextLength == 0 && rightText.TextLength == 0 && !leftText.Focused && !rightText.Focused;
    CompareOptions Options => new(ignoreCase.Checked, ignoreSpace.Checked, ignoreBlank.Checked);
    void InvalidateComparison() { cancellation?.Cancel(); CancelDetail(); scrollMap = null; completedResult = false; ClearDecoration(); textResult = null; folderResult = null; comparedLeft = comparedRight = null; comparedOptions = null; blockToRight.Enabled = blockToLeft.Enabled = false; reportRows.Clear(); summary = "Inputs Changed · Compare Again"; status.Text = summary; results.Rows.Clear(); differenceIndex = -1; scrubber.SetRows(null); folderScope.Text = "Folder Scope Changed · Compare Again To Refresh Results And Exclusion Counts"; help.SetToolTip(folderScope, folderScope.Text); }
    public void SetOptions(CompareOptions o) { ignoreCase.Checked = o.IgnoreCase; ignoreSpace.Checked = o.IgnoreWhitespace; ignoreBlank.Checked = o.IgnoreBlankLines; }
    public void ApplySession(Session session) { SetOptions(session.Options); fileFilter.Text = session.FileFilter; ignoredDirectories.Text = session.IgnoredDirectories; granularity.SelectedIndex = (int)session.Granularity; syncScrolling.Checked = session.SyncScrolling; wrap.Checked = session.WordWrap; delimiter.Text = session.TableSeparator == "\t" ? "\\t" : session.TableSeparator; }
    void UpdateMode() { textControls.Visible = Mode == "Text"; bool folder = Mode == "Folders"; folderFilters.Visible = folderScope.Visible = folder; foreach (var check in new[] { ignoreCase, ignoreSpace, ignoreBlank, wrap }) check.Visible = check.Enabled = !folder; editors.Visible = Mode == "Text"; UpdateEmptyState(); status.Text = Mode == "Binary" ? "Full-File Streaming · Positional Bytes · Bounded Samples" : "Choose Inputs And Compare · Folder And Table Modes Are Read-Only"; }
    void Error(Exception ex) { CancelDetail(); scrollMap = null; completedResult = false; status.Text = "Error · " + ex.Message; if (FindForm() is MainForm { IsSmoke: true } owner) { owner.FailSmoke(ex); return; } if (FindForm()?.Visible == true) DarkPrompt.Show(this, ex.Message, "DiffBeacon", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    void Browse(TextBox target)
    {
        if (Mode == "Folders") { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.SelectedPath; }
        else { using var d = new OpenFileDialog(); if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.FileName; }
    }
    bool ConfirmDiscard() => SmokeOwned || !(dirtyLeft || dirtyRight) || DarkPrompt.Show(this, "Discard Unsaved Editor Changes?", "DiffBeacon", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    public bool CanClose() => ConfirmDiscard();
    void NewSnippets() { if (!ConfirmDiscard()) return; InvalidateComparison(); leftPath.Clear(); rightPath.Clear(); leftDoc = rightDoc = null; SetTexts("", ""); dirtyLeft = dirtyRight = false; undo.Clear(); mode.SelectedItem = "Text"; status.Text = "Choose Inputs And Compare"; }
    void SetTexts(string left, string right) { suppress = true; try { leftText.Text = left; rightText.Text = right; } finally { suppress = false; } }
    void Swap() { InvalidateComparison(); undo.Clear(); (leftPath.Text, rightPath.Text) = (rightPath.Text, leftPath.Text); (leftDoc, rightDoc) = (rightDoc, leftDoc); (dirtyLeft, dirtyRight) = (dirtyRight, dirtyLeft); var a = leftText.Text; SetTexts(rightText.Text, a); status.Text = "Sides Swapped · Compare Again"; }
    async Task Compare(bool showErrors = true)
    {
        if (SmokeOwned) showErrors = false;
        InvalidateComparison(); var cts = new CancellationTokenSource(); cancellation = cts; var token = cts.Token;
        compare.Enabled = false; cancel.Enabled = true; status.Text = "Comparing…";
        try
        {
            var selectedMode = Mode; string lp = leftPath.Text, rp = rightPath.Text; var options = Options;
            var folderOptions = selectedMode == "Folders" ? FolderFilters.Parse(fileFilter.Text, ignoredDirectories.Text) : null;
            if (selectedMode == "Text")
            {
                bool loadLeft = lp.Length > 0 && (leftDoc == null || !string.Equals(leftDoc.Path, Path.GetFullPath(lp), StringComparison.OrdinalIgnoreCase));
                bool loadRight = rp.Length > 0 && (rightDoc == null || !string.Equals(rightDoc.Path, Path.GetFullPath(rp), StringComparison.OrdinalIgnoreCase));
                if ((loadLeft && dirtyLeft || loadRight && dirtyRight) && !ConfirmDiscard()) { status.Text = "Comparison Not Run · Inputs Changed · Compare Again"; return; }
                var ld = loadLeft ? await Task.Run(() => TextDocument.Load(lp), token) : leftDoc; var rd = loadRight ? await Task.Run(() => TextDocument.Load(rp), token) : rightDoc;
                if (disposed || cts != cancellation) return; token.ThrowIfCancellationRequested();
                if (loadLeft || loadRight) undo.Clear();
                if (loadLeft) { leftDoc = ld; suppress = true; leftText.Text = ld!.Text; suppress = false; dirtyLeft = false; }
                if (loadRight) { rightDoc = rd; suppress = true; rightText.Text = rd!.Text; suppress = false; dirtyRight = false; }
                var lt = leftText.Text; var rt = rightText.Text; var result = await Task.Run(() => TextComparison.Compare(lt, rt, options, token), token);
                if (disposed || cts != cancellation) return; token.ThrowIfCancellationRequested();
                textResult = result; comparedLeft = lt; comparedRight = rt; comparedOptions = options; summary = result.Summary; scrubber.SetRows(result.Rows); differenceIndex = -1; UpdateEmptyState(); UpdateBlockActions(); reportRows = result.Rows.Select(r => new string?[] { r.Status.ToString(), r.LeftLine?.ToString(), r.Left, r.RightLine?.ToString(), r.Right }).ToList();
                Grid(["Status", "Left Line", "Left", "Right Line", "Right"], reportRows.Take(10000)); scrollMap = result.PositionalFallback || result.Rows.Any(r => r.Status == Change.Error) ? null : TextScrollMap.Create(result, leftText.LogicalStarts.Length, rightText.LogicalStarts.Length); await ColorTextAsync(); if (disposed || cts != cancellation) return; token.ThrowIfCancellationRequested();
            }
            else if (selectedMode == "Binary")
            {
                var result = await Task.Run(() => BinaryComparison.Compare(lp, rp, token), token); if (disposed || cts != cancellation) return; token.ThrowIfCancellationRequested();
                summary = result.Summary; reportRows = result.Samples.Select(s => new string?[] { "0x" + s.Offset.ToString("X"), s.Offset.ToString(), Hex(s.Left), Ascii(s.Left), Hex(s.Right), Ascii(s.Right) }).ToList(); Grid(["Hex Offset", "Decimal Offset", "Left Hex", "Left ASCII", "Right Hex", "Right ASCII"], reportRows);
            }
            else if (selectedMode == "Folders")
            {
                var result = await Task.Run(() => FolderComparison.Compare(lp, rp, folderOptions!, token), token); if (disposed || cts != cancellation) return; token.ThrowIfCancellationRequested();
                folderResult = result; summary = result.Summary; folderScope.Text = summary; help.SetToolTip(folderScope, summary); reportRows = result.Rows.Select(r => new string?[] { r.Status.ToString(), r.RelativePath, r.LeftSize?.ToString(), r.RightSize?.ToString(), r.Detail }).ToList(); Grid(["Status", "Relative Path", "Left Bytes", "Right Bytes", "Detail"], reportRows.Take(10000)); if (reportRows.Count > 10000) summary += " · Grid Shows First 10,000; Export Includes Retained Rows";
            }
            else
            {
                char sep = delimiter.Text == "\\t" ? '\t' : delimiter.Text.Length == 1 ? delimiter.Text[0] : throw new ArgumentException("Enter one separator character, or \\t for a tab.");
                var table = await Task.Run(() =>
                {
                    var a = DelimitedText.Parse(TextDocument.Load(lp).Text, sep); var b = DelimitedText.Parse(TextDocument.Load(rp).Text, sep); var data = new List<string?[]>(); int differences = 0;
                    for (int i = 0; i < Math.Max(a.Count, b.Count); i++) { token.ThrowIfCancellationRequested(); for (int j = 0; j < Math.Max(i < a.Count ? a[i].Length : 0, i < b.Count ? b[i].Length : 0); j++) { var av = i < a.Count && j < a[i].Length ? a[i][j] : null; var bv = i < b.Count && j < b[i].Length ? b[i][j] : null; bool equal = av == bv; if (!equal) differences++; if (data.Count < 10000) data.Add([equal ? "Equal" : "Changed", (i + 1).ToString(), (j + 1).ToString(), av, bv]); } }
                    return (Data: data, Summary: $"{differences:N0} Different Cells · Positional Rows/Columns · First 10,000 Cells Retained · Read-Only");
                }, token);
                if (disposed || cts != cancellation) return; token.ThrowIfCancellationRequested(); summary = table.Summary; reportRows = table.Data; Grid(["Status", "Row", "Column", "Left", "Right"], reportRows);
            }
            completedResult = true; status.Text = summary;
        }
        catch (OperationCanceledException) { if (!disposed && cts == cancellation && status.Text == "Comparing…") { InvalidateComparison(); status.Text = "Cancelled · Comparison Is Incomplete"; } }
        catch (Exception) when (token.IsCancellationRequested) { if (!disposed && cts == cancellation && status.Text == "Comparing…") { InvalidateComparison(); status.Text = "Cancelled · Comparison Is Incomplete"; } }
        catch (Exception ex) { if (!disposed && cts == cancellation && !token.IsCancellationRequested) { completedResult = false; if (showErrors) Error(ex); else { status.Text = "Error · " + ex.Message; throw; } } }
        finally { if (!disposed && cts == cancellation) { compare.Enabled = true; cancel.Enabled = false; cancellation = null; } cts.Dispose(); }
    }
    static string Hex(byte? value) => value?.ToString("X2") ?? "—";
    static string Ascii(byte? value) => !value.HasValue ? "—" : value >= 32 && value <= 126 ? ((char)value).ToString() : ".";
    void Grid(string[] columns, IEnumerable<string?[]> rows)
    {
        results.SuspendLayout();
        try
        {
            results.Rows.Clear(); results.Columns.Clear(); foreach (string name in columns) results.Columns.Add(name, name);
            var prepared = rows.Select(values =>
            {
                var row = new DataGridViewRow(); row.CreateCells(results, values.Select(x => (object)(x ?? "")).ToArray());
                if (values[0] is "Changed" or "LeftOnly" or "RightOnly" or "Error") DarkTheme.DifferenceRow(row.DefaultCellStyle, values[0]);
                return row;
            }).ToArray();
            if (prepared.Length > 0) results.Rows.AddRange(prepared);
        }
        finally { results.ResumeLayout(); }
    }
    (ComparisonEditor Box, int Start, int Length)[] visiblePaintPlan = [];
    bool[] paintedDetail = [];
    Task? visiblePaintTask;
    bool visiblePaintPending;
    void CancelDetail() { detailGeneration++; detailCancellation?.Cancel(); detail = null; visiblePaintPlan = []; paintedDetail = []; visiblePaintPending = false; }
    void QueueVisibleDecoration()
    {
        if (syncing || disposed || visiblePaintPlan.Length == 0) return;
        _ = RefreshVisibleDecoration();
    }
    async Task RefreshVisibleDecoration() { try { await PaintVisibleAsync(); } catch (Exception ex) { if (!disposed) Error(ex); } }
    Task PaintVisibleAsync()
    {
        visiblePaintPending = true;
        if (visiblePaintTask == null || visiblePaintTask.IsCompleted) visiblePaintTask = PaintVisibleLoopAsync();
        return visiblePaintTask;
    }
    async Task PaintVisibleLoopAsync()
    {
        // Return to the message pump before formatting a newly visible viewport.
        await Task.Yield();
        while (visiblePaintPending && !disposed)
        {
            visiblePaintPending = false;
            var plan = visiblePaintPlan; var done = paintedDetail; long generation = detailGeneration;
            bool Current() => !disposed && generation == detailGeneration && ReferenceEquals(plan, visiblePaintPlan);
            var candidates = new List<int>();
            foreach (var box in new[] { leftText, rightText })
            {
                if (!box.IsHandleCreated) continue;
                int first = Math.Max(0, box.LogicalAt(box.RawAtDisplayLine(box.FirstDisplayLine)) - 1);
                int last = Math.Min(box.LogicalStarts.Length - 1, box.LogicalAt(box.GetCharIndexFromPosition(new Point(Math.Max(0, box.ClientSize.Width - 1), Math.Max(0, box.ClientSize.Height - 1)))) + 1);
                int start = box.LogicalStarts[first], end = last + 1 < box.LogicalStarts.Length ? box.LogicalStarts[last + 1] : box.TextLength;
                for (int i = 0; i < plan.Length; i++) if (!done[i] && ReferenceEquals(plan[i].Box, box) && plan[i].Start < end && plan[i].Start + plan[i].Length > start) candidates.Add(i);
            }
            int offset = 0;
            while (offset < candidates.Count && Current())
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                int limit = Math.Min(candidates.Count, offset + 128);
                bool previous = suppress; suppress = true; syncing = true;
                try
                {
                    hasDecoration = decorationMayBeInUndo = true;
                    while (offset < limit)
                    {
                        var box = plan[candidates[offset]].Box;
                        box.Decorate(() =>
                        {
                            while (offset < limit && ReferenceEquals(plan[candidates[offset]].Box, box))
                            {
                                int index = candidates[offset++]; var item = plan[index];
                                box.Select(item.Start, item.Length);
                                box.SelectionBackColor = SystemInformation.HighContrast ? SystemColors.Highlight : DarkTheme.Amber;
                                box.SelectionColor = SystemInformation.HighContrast ? SystemColors.HighlightText : DarkTheme.AmberText;
                                done[index] = true;
                                if (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 16) break;
                            }
                        });
                        if (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 16) break;
                    }
                }
                finally { suppress = previous; syncing = false; }
                // A timer-backed break lets WinForms process input/paint, unlike
                // an uninterrupted chain of posted Task.Yield continuations.
                if (offset < candidates.Count)
                {
                    await Task.Delay(1);
                    if (visiblePaintPending) break;
                }
            }
        }
    }
    void ClearDecoration()
    {
        // Native text undo can restore the character format captured before an
        // edit, even after the currently visible decoration was already cleared.
        if (!hasDecoration && !decorationMayBeInUndo) return;
        if (leftText.ChangingText || rightText.ChangingText)
        {
            if (!clearQueued && IsHandleCreated && !disposed)
            {
                clearQueued = true;
                BeginInvoke((Action)(() => { clearQueued = false; if (!disposed) { try { ClearDecoration(); } catch (Exception ex) { Error(ex); } } }));
            }
            return;
        }
        bool previous = suppress; suppress = true; syncing = true;
        try { foreach (var box in new[] { leftText, rightText }) box.Decorate(() => { box.SelectAll(); box.SelectionBackColor = box.BackColor; box.SelectionColor = box.ForeColor; }); hasDecoration = false; }
        finally { suppress = previous; syncing = false; }
    }
    void ColorText() { _ = RefreshDecoration(); }
    async Task RefreshDecoration() { try { await ColorTextAsync(); } catch (Exception ex) { if (!disposed) Error(ex); } }
    async Task ColorTextAsync()
    {
        CancelDetail(); ClearDecoration();
        if (textResult == null || comparedOptions == null || Mode != "Text") return;
        var result = textResult; var options = comparedOptions; var choice = (HighlightGranularity)granularity.SelectedIndex;
        string left = leftText.Snapshot, right = rightText.Snapshot;
        long generation = detailGeneration; var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation?.Token ?? CancellationToken.None); detailCancellation = cts;
        try
        {
            var computed = await Task.Run(() => InlineDifferences.Compare(result, options, choice, cts.Token), cts.Token);
            bool Current() => !disposed && generation == detailGeneration && ReferenceEquals(result, textResult) && left == leftText.Snapshot && right == rightText.Snapshot && !cts.IsCancellationRequested;
            if (!Current()) return;
            detail = computed;
            var paint = new List<(ComparisonEditor Box, int Start, int Length)>(); int available = 0;
            for (int i = 0; i < result.Rows.Count; i++)
            {
                var row = result.Rows[i]; var spans = computed.Rows[i];
                Add(leftText, row.LeftLine, spans.Left); Add(rightText, row.RightLine, spans.Right);
            }
            void Add(ComparisonEditor box, int? line, IReadOnlyList<TextSpan> spans)
            {
                if (!line.HasValue || line.Value < 1 || line.Value > box.LogicalStarts.Length) return;
                foreach (var span in spans)
                {
                    available++;
                    if (paint.Count < MaxPaintSpans && span.Length > 0)
                    {
                        int start = box.LogicalStarts[line.Value - 1] + span.Start;
                        if (start >= 0 && span.Length <= box.TextLength - start) paint.Add((box, start, span.Length));
                    }
                }
            }
            // Keep the full bounded plan, but format native ranges only as they
            // enter the viewport. Detail computation/export remain independent.
            visiblePaintPlan = paint.ToArray(); paintedDetail = new bool[paint.Count];
            await PaintVisibleAsync();
            if (Current())
            {
                string limits = computed.FallbackLines > 0 ? $" · {computed.FallbackLines:N0} Lines Use Whole-Line Detail Fallback" : "";
                if (available > MaxPaintSpans) limits += $" · First {MaxPaintSpans:N0} Highlight Spans Shown";
                string scrolling = scrollMap == null ? " · Independent Scrolling (Exact Alignment Unavailable)" : "";
                string scope = result.Rows.Count > 10000 ? " · Grid Shows First 10,000 Rows; Export Contains All Retained Rows" : "";
                summary = result.Summary + scope + " · " + choice + " Highlight" + limits + scrolling;
                status.Text = summary;
            }
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(detailCancellation, cts)) detailCancellation = null; cts.Dispose(); }
    }
    void SyncViewport(ComparisonEditor source, ComparisonEditor destination, bool fromLeft)
    {
        if (syncing || !syncScrolling.Checked || scrollMap == null || textResult == null || source.Snapshot != (fromLeft ? comparedLeft : comparedRight)) return;
        syncing = true;
        try
        {
            int first = source.FirstDisplayLine, raw = source.RawAtDisplayLine(first);
            int logical = source.LogicalAt(raw);
            int target = fromLeft ? scrollMap.MapLeftToRight(logical) : scrollMap.MapRightToLeft(logical);
            target = Math.Clamp(target, 0, destination.LogicalStarts.Length - 1);
            int within = Math.Max(0, first - source.DisplayLineAt(source.LogicalStarts[logical]));
            int targetFirst = destination.DisplayLineAt(destination.LogicalStarts[target]);
            int targetEnd = target + 1 < destination.LogicalStarts.Length ? destination.DisplayLineAt(destination.LogicalStarts[target + 1]) - 1 : destination.DisplayLineAt(destination.TextLength);
            destination.ScrollDisplayTo(targetFirst + (wrap.Checked ? Math.Clamp(within, 0, Math.Max(0, targetEnd - targetFirst)) : 0));
            if (!wrap.Checked) destination.ScrollHorizontalTo(source.HorizontalPosition);
        }
        finally { syncing = false; }
    }
    int differenceIndex = -1;
    void Navigate(int delta)
    {
        if (textResult == null) return; var differences = textResult.Rows.Where(r => r.Status != Change.Equal).ToArray(); if (differences.Length == 0) return;
        SetSelectedDifference(differenceIndex < 0 ? (delta < 0 ? differences.Length - 1 : 0) : (differenceIndex + delta + differences.Length) % differences.Length);
    }
    void SetSelectedDifference(int index)
    {
        if (textResult == null) return;
        var differences = textResult.Rows.Where(r => r.Status != Change.Equal).ToArray();
        if (index < 0 || index >= differences.Length) return;
        void MarkLine(ComparisonEditor box, int? line)
        {
            if (!line.HasValue || line.Value < 1 || line.Value > box.LogicalStarts.Length) return;
            int at = box.LogicalStarts[line.Value - 1]; box.Select(at, 0); box.ScrollToCaret();
        }
        suppress = true;
        try
        {

            var row = differences[index]; differenceIndex = index; MarkLine(leftText, row.LeftLine); MarkLine(rightText, row.RightLine);
            scrubber.SetSelected(index); status.Text = $"Difference {index + 1} Of {differences.Length} · {row.Status}"; UpdateBlockActions();
        }
        finally { suppress = false; }
        QueueVisibleDecoration();
    }
    void Merge(bool toRight, bool all)
    {
        if (Mode != "Text") return; var source = toRight ? leftText : rightText; var destination = toRight ? rightText : leftText;
        if (!all && source.SelectionLength == 0) throw new InvalidOperationException("Select source text and a destination range/caret first.");
        string replacement = all ? source.Text : source.SelectedText;
        _ = TextEditProposal.Replace(destination.Text, all ? 0 : destination.SelectionStart, all ? destination.TextLength : destination.SelectionLength, replacement, source.Text);
        PushMergeUndo();
        if (all) destination.Text = replacement; else destination.SelectedText = replacement; InvalidateComparison(); status.Text = "Merged Into Editor · Review And Save As To Write A File";
    }
    bool CanMergeBlock() => Mode == "Text" && textResult != null && !textResult.PositionalFallback && !textResult.Rows.Any(r => r.Status == Change.Error) && differenceIndex >= 0 && differenceIndex < textResult.Differences && comparedOptions is { IgnoreCase: false, IgnoreWhitespace: false, IgnoreBlankLines: false } && !ignoreCase.Checked && !ignoreSpace.Checked && !ignoreBlank.Checked && string.Equals(comparedLeft, leftText.Text, StringComparison.Ordinal) && string.Equals(comparedRight, rightText.Text, StringComparison.Ordinal);
    void UpdateBlockActions() => blockToRight.Enabled = blockToLeft.Enabled = CanMergeBlock();
    void PushMergeUndo() { undo.Push((leftText.Text, rightText.Text)); if (undo.Count > 20) { var recent = undo.Take(20).Reverse().ToArray(); undo.Clear(); foreach (var item in recent) undo.Push(item); } }
    void MergeBlock(bool toRight)
    {
        if (!CanMergeBlock()) throw new InvalidOperationException("Select a difference in a fresh exact Text comparison with all ignore options off before merging a block.");
        var plan = AlignedBlockMerge.Plan(leftText.Text, rightText.Text, textResult!, comparedOptions!, differenceIndex, toRight);
        string output = AlignedBlockMerge.Apply(plan, leftText.Text, rightText.Text);
        PushMergeUndo();
        (toRight ? rightText : leftText).Text = output;
        InvalidateComparison();
        status.Text = $"Block Merged Into {(toRight ? "Right" : "Left")} Editor · Review And Save As To Write A File";
    }
    void Undo() { if (undo.TryPop(out var item)) { SetTexts(item.Left, item.Right); dirtyLeft = dirtyRight = true; InvalidateComparison(); status.Text = "Merge Undone · Compare Again"; } }
    void Save(bool right)
    {
        if (Mode != "Text") return; using var dialog = new SaveFileDialog { FileName = Path.GetFileName(right ? rightPath.Text : leftPath.Text), OverwritePrompt = true, Filter = "All Files|*.*" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string destination = Path.GetFullPath(dialog.FileName);
        if (right) { TextDocument.SavePane(ref rightDoc, ref dirtyRight, rightText.Text, destination, leftDoc); rightPath.Text = destination; }
        else { TextDocument.SavePane(ref leftDoc, ref dirtyLeft, leftText.Text, destination, rightDoc); leftPath.Text = destination; }
        status.Text = "Saved · Existing Destination Backed Up Beside File";
    }
    void SaveSession()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiffBeacon", "Sessions"); Directory.CreateDirectory(folder);
        using var dialog = new SaveFileDialog { InitialDirectory = folder, Filter = "DiffBeacon Session|*.json", DefaultExt = "json", FileName = "Comparison.json" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        CaptureSession(Path.GetFileNameWithoutExtension(dialog.FileName)).Save(dialog.FileName); status.Text = "Session Metadata And Folder Scope Saved · Editor Contents Are Not Stored";
    }
    Session CaptureSession(string name) => new(3, name, Mode, leftPath.Text, rightPath.Text, Options, fileFilter.Text, ignoredDirectories.Text, (HighlightGranularity)granularity.SelectedIndex, syncScrolling.Checked, wrap.Checked, delimiter.Text == "\\t" ? "\t" : delimiter.Text);
    void Export()
    {
        if (!completedResult) throw new InvalidOperationException("Complete a current comparison before exporting.");
        using var dialog = new SaveFileDialog { Filter = "HTML Report|*.html", DefaultExt = "html", FileName = "DiffBeacon.html" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var bytes = Encoding.UTF8.GetBytes(Reports.Html("DiffBeacon · " + Mode, leftPath.Text + " ↔ " + rightPath.Text + " · " + summary, reportRows)); SafeFiles.Write(dialog.FileName, bytes, File.Exists(dialog.FileName) ? FileStamp.Capture(dialog.FileName) : null, true);
    }
    public async Task RunSmoke(string screenshot)
    {
        scrubber.SmokeGeometryAndPaint(screenshot);
        await RunLayoutSmoke(screenshot);
        await RunGranularSmoke();
        SetTexts("// DiffBeacon · Compare With Clarity\npublic sealed class Beacon\n{\n    public string Name => \"Original\";\n    public int ReviewCount => 2;\n}\n", "// DiffBeacon · Compare With Clarity\npublic sealed class Beacon\n{\n    public string Name => \"Reviewed\";\n    public int ReviewCount => 3;\n    public bool Ready => true;\n}\n");
        await Compare(); if (textResult?.Differences != 3) throw new Exception("Text smoke comparison failed.");
        if (status.Text != summary) throw new Exception("Completed comparison status did not show its summary.");
        foreach (var option in new[] { ignoreCase, ignoreSpace, ignoreBlank }) { option.Checked = !option.Checked; AssertClearedStatus("Inputs Changed · Compare Again"); option.Checked = !option.Checked; await Compare(); }
        leftPath.Text = "changed-path.txt"; AssertClearedStatus("Inputs Changed · Compare Again"); leftPath.Clear(); await Compare();
        rightPath.Text = "changed-path.txt"; AssertClearedStatus("Inputs Changed · Compare Again"); rightPath.Clear(); await Compare();
        delimiter.Text = ";"; AssertClearedStatus("Inputs Changed · Compare Again"); delimiter.Text = ","; await Compare();
        Merge(true, true); if (rightText.Text != leftText.Text || reportRows.Count != 0) throw new Exception("Merge/invalidation smoke failed.");
        Undo(); await Compare(); if (textResult?.Differences != 3) throw new Exception("Undo smoke failed.");
        leftText.AppendText("// changed"); if (textResult != null || reportRows.Count != 0) throw new Exception("Edit did not invalidate result.");
        Undo(); // No remaining merge history: restore explicit fixture below.
        var original = leftText.Text; var pending = Compare(); leftText.AppendText("// mid-flight"); await pending;
        if (textResult != null || reportRows.Count != 0) throw new Exception("Stale asynchronous results were published.");
        SetTexts(original[..original.LastIndexOf("// changed", StringComparison.Ordinal)], rightText.Text);
        foreach (var item in new[] { "Folders", "Binary", "Tables", "Text" }) mode.SelectedItem = item;
        await Compare(); dirtyLeft = dirtyRight = false; Swap(); AssertClearedStatus("Sides Swapped · Compare Again"); Swap(); await Compare();
        var fixtureLeft = leftText.Text; var fixtureRight = rightText.Text; NewSnippets(); AssertClearedStatus("Choose Inputs And Compare");
        string cancellationFixture = string.Join('\n', Enumerable.Range(0, 1200)); SetTexts(cancellationFixture, cancellationFixture + "\nchanged");
        var cancelPending = Compare(); bool wasRunning = !cancelPending.IsCompleted;
        if (wasRunning && status.Text != "Comparing…") throw new Exception("Running comparison did not show progress."); cancellation?.Cancel(); await cancelPending;
        if (wasRunning && status.Text != "Cancelled · Comparison Is Incomplete") throw new Exception("Cancelled comparison status was inaccurate.");
        mode.SelectedItem = "Binary";
        leftPath.Text = Path.Combine(Path.GetTempPath(), "DiffBeacon-Missing-" + Guid.NewGuid().ToString("N")); rightPath.Text = leftPath.Text;
        var faultPending = Compare(false);
        // Smoke only: let the real worker fault while its UI continuation is queued,
        // then mutate the input before yielding the UI thread back to that continuation.
        Thread.Sleep(100);
        leftPath.Text += ".changed"; AssertClearedStatus("Inputs Changed · Compare Again");
        await faultPending; AssertClearedStatus("Inputs Changed · Compare Again");
        var cancelledFaultPending = Compare(false); Thread.Sleep(100); bool cancelledFault = cancellation != null; cancellation?.Cancel(); await cancelledFaultPending;
        if (cancelledFault && status.Text != "Cancelled · Comparison Is Incomplete") throw new Exception("Manual cancellation racing a worker fault left inaccurate progress/error feedback.");
        leftPath.Clear(); rightPath.Clear(); mode.SelectedItem = "Text";
        SetTexts(fixtureLeft, fixtureRight); await Compare();
        if (scrubber.MarkerCount != 3) throw new Exception("Overview marker count failed.");
        scrubber.ActivateAt(scrubber.MarkerX(2)); if (differenceIndex != 2 || scrubber.SelectedIndex != 2 || scrubber.SelectedStatus != Change.RightOnly) throw new Exception("Overview click navigation failed.");
        Navigate(1); if (differenceIndex != 0 || scrubber.SelectedIndex != 0) throw new Exception("Overview button synchronization failed.");
        await ((DarkWindow)FindForm()!).SmokeResponsive(this, screenshot, async () => { foreach (var item in new[] { "Folders", "Binary", "Tables", "Text" }) { mode.SelectedItem = item; PerformLayout(); DarkWindow.CheckCommandBounds(this); CommandBar.VisitPages(CommandBar.CheckBounds); } await Compare(); });
        var form = FindForm()!; form.Width = 1340; form.Height = 900; await Task.Delay(250);
        CommandBar.VisitPages(() => ((DarkWindow)form).CapturePreview(Path.ChangeExtension(screenshot, $".commands-{CommandBar.ActivePageName.ToLowerInvariant()}.png")));
        ((DarkWindow)form).CapturePreview(Path.GetFullPath(screenshot));
        dirtyLeft = dirtyRight = false;
        SetTexts("", ""); InvalidateComparison(); status.Text = "Choose Files Or Paste Text, Then Compare"; emptyState.Visible = true; emptyState.BringToFront(); await Task.Delay(150);
        // Keep the empty-state capture on the same custom-frame path. Form.DrawToBitmap
        // paints a stock nonclient caption even though the live window uses custom chrome.
        ((DarkWindow)form).CapturePreview(Path.ChangeExtension(Path.GetFullPath(screenshot), ".empty.png"), emptyState);
        await RunBlockMergeSmoke(Path.ChangeExtension(Path.GetFullPath(screenshot), ".block-merge.png"));
        await RunFolderSmoke(Path.ChangeExtension(Path.GetFullPath(screenshot), ".folders.png"));
    }
    internal void CheckHostedCommand()
    {
        SetTexts("left host fixture", "right host fixture");
        CommandBar.VisitPages(() =>
        {
            foreach (var button in CommandBar.Descendants().OfType<Button>().Where(b => b.Visible && b.Text == "Swap")) button.PerformClick();
        });
        if (leftText.Text != "right host fixture" || rightText.Text != "left host fixture") throw new Exception("Hosted Swap did not route to its owning comparison.");
        SetTexts("", "");
    }
    async Task RunLayoutSmoke(string screenshot)
    {
        if (editors.Orientation != Orientation.Vertical || CommandBar.Parent == null || !CommandBar.Visible) throw new Exception("Default side-by-side workspace or command host is missing.");
        stacked.Checked = true; stacked.Checked = false;
        string left = string.Join('\n', Enumerable.Range(0, 500).Select(i => $"row {i:D3} old " + new string('x', 100)));
        string right = left.Replace("old", "new", StringComparison.Ordinal);
        SetTexts(left, right); leftText.ClearUndo(); leftText.Select(leftText.TextLength, 0); leftText.SelectedText = " typed";
        string typed = leftText.Text; await Compare(); SetSelectedDifference(100);
        leftText.Select(7, 3); rightText.Select(9, 2);
        var map = scrollMap; long generation = detailGeneration; int selected = differenceIndex;
        bool leftDirty = dirtyLeft, rightDirty = dirtyRight; int history = undo.Count;
        foreach (bool wrapped in new[] { false, true })
        {
            wrap.Checked = wrapped;
            // Establish typing after the native WordWrap setter; this gate tests
            // layout reflow of both wrapped and unwrapped editors.
            SetTexts(left, right); leftText.ClearUndo(); leftText.Select(leftText.TextLength, 0); leftText.SelectedText = " typed";
            await Compare(); SetSelectedDifference(100); leftText.Select(7, 3); rightText.Select(9, 2);
            map = scrollMap; generation = detailGeneration; selected = differenceIndex;
            leftDirty = dirtyLeft; rightDirty = dirtyRight;
            leftText.ScrollDisplayTo(leftText.DisplayLineAt(leftText.LogicalStarts[100]));
            for (int cycle = 0; cycle < 4; cycle++)
            {
                int leftLine = leftText.LogicalAt(leftText.RawAtDisplayLine(leftText.FirstDisplayLine));
                int rightLine = rightText.LogicalAt(rightText.RawAtDisplayLine(rightText.FirstDisplayLine));
                stacked.Checked = !stacked.Checked; await Task.Delay(20);
                if (visiblePaintTask != null) await visiblePaintTask;
                if (leftText.Text != typed || rightText.Text != right || leftText.SelectionStart != 7 || leftText.SelectionLength != 3 || rightText.SelectionStart != 9 || rightText.SelectionLength != 2 || dirtyLeft != leftDirty || dirtyRight != rightDirty || undo.Count != history || !ReferenceEquals(map, scrollMap) || detailGeneration != generation || differenceIndex != selected)
                    throw new Exception("Changing editor layout changed comparison or editor state.");
                if (leftText.LogicalAt(leftText.RawAtDisplayLine(leftText.FirstDisplayLine)) != leftLine || rightText.LogicalAt(rightText.RawAtDisplayLine(rightText.FirstDisplayLine)) != rightLine)
                    throw new Exception("Changing editor layout lost a logical viewport anchor.");
            }
            leftText.Undo(); if (leftText.Text != left) throw new Exception($"Layout switch lost native typing undo (wrapped={wrapped}, CanUndo={leftText.CanUndo}).");
            leftText.Redo(); if (leftText.Text != typed) throw new Exception("Layout switch lost native typing redo.");
        }
        Merge(true, true); stacked.Checked = true; Undo();
        if (rightText.Text != right || leftText.Text != typed) throw new Exception("Layout switch lost merge undo.");
        wrap.Checked = false; stacked.Checked = false;
        await Compare(); var retainedMap = scrollMap;
        double retainedProportion = resultProportion;
        showResults.Checked = false;
        if (!workspace.Panel2Collapsed || !showResults.Visible || !ReferenceEquals(scrollMap, retainedMap)) throw new Exception("Results collapse changed comparison state or hid its toggle.");
        showResults.Checked = true;
        if (workspace.Panel2Collapsed || Math.Abs(resultProportion - retainedProportion) > .02) throw new Exception("Results expansion lost its split proportion.");
        var form = (DarkWindow)FindForm()!;
        foreach (int width in new[] { 900, 1140, 1260 })
        {
            form.Size = new(width, 620); await Task.Delay(20);
            foreach (bool stack in new[] { false, true })
            {
                stacked.Checked = stack; await Task.Delay(20); CommandBar.VisitPages(CommandBar.CheckBounds);
                foreach (var box in new[] { leftText, rightText }) if (box.Width < 100 || box.Height < box.Font.Height) { form.CapturePreview(Path.ChangeExtension(screenshot, ".layout-failure.png")); throw new Exception($"Layout leaves no usable editor viewport: width={width}, stacked={stack}, editor={box.Size}, deck={CommandBar.Height}, workspace={workspace.Size}, split={workspace.SplitterDistance}, proportions={editorProportion}/{resultProportion}."); }
                foreach (var header in new[] { leftHeader, rightHeader }) foreach (Control child in header.Controls)
                {
                    if (!header.ClientRectangle.Contains(child.Bounds)) throw new Exception("Pane path or Browse is clipped.");
                    if (child is Label or Button)
                    {
                        var textSize = TextRenderer.MeasureText(child.Text, child.Font);
                        if (textSize.Width > child.ClientSize.Width || textSize.Height > child.ClientSize.Height)
                            throw new Exception($"Pane header text is clipped: {child.Text}, measured {textSize}, available {child.ClientSize}.");
                    }
                }
                if (results.DisplayedRowCount(includePartialRow: false) == 0)
                    throw new Exception($"Results shows no complete row: width={width}, stacked={stack}, grid height={results.Height}, workspace={workspace.Height}, split={workspace.SplitterDistance}/{workspace.SplitterWidth}, header={leftHeader.Height}, font={leftText.Font.Height}, results header/row={results.ColumnHeadersHeight}/{results.RowTemplate.Height}.");
                if (width == 1260) form.CapturePreview(Path.ChangeExtension(screenshot, stack ? ".stacked.png" : ".side-by-side.png"));
            }
        }
        SetTexts(left, right); var pending = Compare(); stacked.Checked = !stacked.Checked;
        CommandBar.VisitPages(() => { if (cancel.Visible) cancel.PerformClick(); }); await pending;
        if (completedResult || cancel.Enabled || visiblePaintPlan.Length != 0) throw new Exception("Layout switch interfered with comparison cancellation.");
        stacked.Checked = false; wrap.Checked = false; dirtyLeft = dirtyRight = false; undo.Clear(); SetTexts("", ""); InvalidateComparison();
        Console.WriteLine("Layout default, reflow, viewport, undo, results, dimensions and cancellation checks: passed.");
    }
    async Task RunGranularSmoke()
    {
        Program.CheckArgumentRouting();
        if (cancel.Enabled) throw new Exception("Cancel is enabled without an active comparison.");
        void CheckPassiveSurfaces(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is Label or CheckBox or RadioButton && child.Dock != DockStyle.Bottom && child.BackColor != Color.Transparent)
                    throw new Exception("A passive control obscures its containing surface.");
                CheckPassiveSurfaces(child);
            }
        }
        CheckPassiveSurfaces(this);
        if (DarkTheme.GridSelectionOutline(SystemColors.Highlight, true).ToArgb() != SystemColors.HighlightText.ToArgb() ||
            DarkTheme.GridSelectionOutline(SystemColors.Window, true).ToArgb() != SystemColors.WindowText.ToArgb())
            throw new Exception("High-contrast grid outline does not contrast with row selection.");
        if (leftText.AccessibleName != "Left Text" || rightText.AccessibleName != "Right Text") throw new Exception("Editor accessible names are missing.");
        mode.SelectedItem = "Text"; leftPath.Clear(); rightPath.Clear();
        SetTexts("replacement", new string('d', TextComparison.MaxCharacters / 2));
        leftText.SelectAll(); rightText.Select(rightText.TextLength, 0); int mergeCount = undo.Count; bool oversizedRejected = false;
        try { Merge(true, false); } catch (InvalidDataException) { oversizedRejected = true; }
        if (!oversizedRejected || undo.Count != mergeCount || rightText.TextLength != TextComparison.MaxCharacters / 2) throw new Exception("Oversized selection merge mutated state before validation.");
        SetTexts("prefix original value\nlast\n", "prefix revised value\nlast\n");
        leftText.ClearUndo(); leftText.Select(leftText.TextLength, 0); leftText.SelectedText = "typed";
        string typed = leftText.Text; leftText.Select(7, 8); leftText.Focus();
        leftText.CheckSuspendedDecoration();
        bool modified = leftText.Modified, canUndo = leftText.CanUndo;
        int first = leftText.FirstDisplayLine, horizontal = leftText.HorizontalPosition;
        granularity.SelectedIndex = (int)HighlightGranularity.Word; await Compare();
        if (!completedResult || detail == null || leftText.SelectionStart != 7 || leftText.SelectionLength != 8 || !leftText.Focused || leftText.Modified != modified || leftText.CanUndo != canUndo || first != leftText.FirstDisplayLine || horizontal != leftText.HorizontalPosition)
            throw new Exception($"Granular decoration changed native editor state: complete={completedResult}, detail={detail != null}, selection={leftText.SelectionStart}/{leftText.SelectionLength} expected=7/8, focused={leftText.Focused}, modified={leftText.Modified}/{modified}, undo={leftText.CanUndo}/{canUndo}, viewport={leftText.FirstDisplayLine}/{first},{leftText.HorizontalPosition}/{horizontal}, suspendHR=0x{leftText.LastSuspendHResult:X8}, actual={leftText.LastSuspendActual}.");
        if (!canUndo) throw new Exception("Native undo fixture did not create an undo action.");
        leftText.Undo();
        if (leftText.Text != typed[..^5] || completedResult || detail != null) throw new Exception("Decoration polluted native typing undo or retained stale detail.");
        await Task.Yield();
        if (!leftText.CanRedo) throw new Exception("Clearing stale decoration erased native redo.");
        leftText.Redo(); await Task.Yield();
        if (leftText.Text != typed) throw new Exception("Decoration changed native typed redo.");
        Console.WriteLine($"Native decoration/typing undo/redo: passed; suspendHR=0x{leftText.LastSuspendHResult:X8}, actual={leftText.LastSuspendActual}.");
        SetTexts("prefix original value\nlast", "prefix revised value\nlast"); await Compare();
        string decoratedBefore = leftText.Text;
        leftText.Select(7, 8); leftText.SelectedText = "edited"; string decoratedAfter = leftText.Text;
        async Task WaitForDecorationClear()
        {
            for (int i = 0; i < 100 && clearQueued; i++) await Task.Delay(1);
            if (clearQueued) throw new Exception("Deferred decoration clear did not finish.");
        }
        await WaitForDecorationClear();
        void CheckClearedEdit(string expected, int length)
        {
            leftText.Select(7, length);
            if (leftText.Text != expected || completedResult || detail != null || textResult != null || leftText.SelectionBackColor.ToArgb() != leftText.BackColor.ToArgb() || leftText.SelectionColor.ToArgb() != leftText.ForeColor.ToArgb())
                throw new Exception($"Editing a highlighted word restored stale native formatting or comparison state: text={leftText.Text == expected}, ready={completedResult}, detail={detail != null}, result={textResult != null}, background={leftText.SelectionBackColor}/{leftText.BackColor}, foreground={leftText.SelectionColor}/{leftText.ForeColor}.");
        }
        CheckClearedEdit(decoratedAfter, 6);
        leftText.Undo(); await WaitForDecorationClear(); CheckClearedEdit(decoratedBefore, 8);
        leftText.Redo(); await WaitForDecorationClear(); CheckClearedEdit(decoratedAfter, 6);
        foreach (var edit in new[] { (Length: 0, Text: "inserted "), (Length: 8, Text: "") })
        {
            SetTexts("prefix original value\nlast", "prefix revised value\nlast"); await Compare();
            string beforeEdit = leftText.Text;
            leftText.Select(7, edit.Length); leftText.SelectedText = edit.Text; string afterEdit = leftText.Text;
            await WaitForDecorationClear(); CheckClearedEdit(afterEdit, Math.Max(1, edit.Text.Length));
            for (int cycle = 0; cycle < 2; cycle++)
            {
                leftText.Undo(); await WaitForDecorationClear(); CheckClearedEdit(beforeEdit, 8);
                leftText.Redo(); await WaitForDecorationClear(); CheckClearedEdit(afterEdit, Math.Max(1, edit.Text.Length));
            }
        }
        SetTexts("prefix original value\nlast", "prefix revised value\nlast"); await Compare();
        Navigate(-1); if (differenceIndex != textResult!.Differences - 1) throw new Exception("First Previous did not select the last difference.");
        granularity.SelectedIndex = (int)HighlightGranularity.Character; var stale = ColorTextAsync();
        leftText.AppendText(" edited"); await stale;
        if (detail != null || textResult != null || hasDecoration || completedResult) throw new Exception("Stale detail survived editor mutation.");
        SetTexts("one old word", "one new word"); await Compare();
        granularity.SelectedIndex = (int)HighlightGranularity.Word;
        granularity.SelectedIndex = (int)HighlightGranularity.Character; await ColorTextAsync();
        if (detail == null || !summary.Contains("Character Highlight", StringComparison.Ordinal)) throw new Exception("Asynchronous granularity switch published an obsolete mode.");
        string longLine = new('x', 320);
        SetTexts(longLine + "\nold suffix", longLine + "\nnew suffix"); wrap.Checked = true; await Compare();
        int second = leftText.LogicalStarts[1];
        if (second != leftText.Text.IndexOf('\n') + 1 || leftText.DisplayLineAt(second) <= 1) throw new Exception("Wrap fixture did not distinguish logical and display lines.");
        var span = detail!.Rows.First(r => r.Left.Count > 0).Left[0];
        leftText.Select(second + span.Start, span.Length);
        if (leftText.SelectionBackColor != (SystemInformation.HighContrast ? SystemColors.Highlight : DarkTheme.Amber)) throw new Exception("Wrapped detail used visual offsets.");
        granularity.SelectedIndex = 1; syncScrolling.Checked = false; wrap.Checked = true; delimiter.Text = "\\t";
        string sessionPath = Path.Combine(Path.GetTempPath(), "DiffBeacon-Preferences-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            CaptureSession("Preferences").Save(sessionPath);
            var applied = Session.Load(sessionPath);
            if (applied.TableSeparator != "\t") throw new Exception("Session did not serialize an actual tab separator.");
            granularity.SelectedIndex = 0; syncScrolling.Checked = true; wrap.Checked = false; delimiter.Text = ",";
            ApplySession(applied);
        }
        finally { File.Delete(sessionPath); }
        if (granularity.SelectedIndex != 1 || syncScrolling.Checked || !wrap.Checked || delimiter.Text != "\\t") throw new Exception("Session preferences were not applied.");
        delimiter.Text = ","; wrap.Checked = false; syncScrolling.Checked = true; granularity.SelectedIndex = 0;
        string lines = string.Join('\n', Enumerable.Range(0, 160).Select(i => $"line {i:D3} " + new string('q', 400)));
        SetTexts(lines, "inserted\n" + lines); await Compare();
        if (scrollMap == null) throw new Exception("Fresh exact scroll map unavailable.");
        leftText.Select(9, 3); rightText.Select(4, 2);
        leftText.ScrollDisplayTo(70);
        if (Math.Abs(rightText.FirstDisplayLine - scrollMap.MapLeftToRight(leftText.FirstDisplayLine)) > 1 || leftText.SelectionStart != 9 || rightText.SelectionStart != 4) throw new Exception("Forward mapped scrolling changed selection or alignment.");
        rightText.ScrollDisplayTo(45);
        if (Math.Abs(leftText.FirstDisplayLine - scrollMap.MapRightToLeft(rightText.FirstDisplayLine)) > 1) throw new Exception("Reverse mapped scrolling failed.");
        leftText.ScrollHorizontalTo(140);
        if (Math.Abs(leftText.HorizontalPosition - rightText.HorizontalPosition) > 1) throw new Exception("Horizontal synchronization failed.");
        syncScrolling.Checked = false; int independent = rightText.FirstDisplayLine;
        leftText.ScrollDisplayTo(85);
        if (rightText.FirstDisplayLine != independent) throw new Exception("Disabled synchronization moved the other pane.");
        syncScrolling.Checked = true; if (rightText.FirstDisplayLine != independent) throw new Exception("Toggling synchronization moved the viewport.");
        wrap.Checked = true; leftText.ScrollDisplayTo(100);
        int logical = leftText.LogicalAt(leftText.RawAtDisplayLine(leftText.FirstDisplayLine));
        int mapped = rightText.LogicalAt(rightText.RawAtDisplayLine(rightText.FirstDisplayLine));
        if (Math.Abs(mapped - scrollMap.MapLeftToRight(logical)) > 1) throw new Exception("Wrapped logical scroll anchor failed.");
        wrap.Checked = false;
        string wideLine = new('w', 24000);
        SetTexts(wideLine + "\nend", wideLine + "\nend"); await Compare(false);
        leftText.ScrollHorizontalTo(80000);
        int widePosition = leftText.HorizontalPosition;
        if (widePosition <= 65535 || Math.Abs(widePosition - rightText.HorizontalPosition) > 1) throw new Exception("32-bit horizontal source position was truncated or not synchronized.");
        leftText.Decorate(() => { leftText.Select(2, 2); leftText.SelectionBackColor = DarkTheme.Amber; });
        if (leftText.HorizontalPosition != widePosition) throw new Exception("Decoration truncated the 32-bit horizontal viewport.");
        string gridLines = string.Join('\n', Enumerable.Repeat("same", 10002));
        SetTexts(gridLines, gridLines); await Compare(false);
        granularity.SelectedIndex = 1; await ColorTextAsync();
        if (!summary.Contains("Grid Shows First 10,000 Rows", StringComparison.Ordinal)) throw new Exception("Granularity refresh lost the retained-grid scope warning.");
        await RunLazyPaintSmoke();
        SetTexts(lines, "short"); await Compare(); leftText.ScrollHorizontalTo(300);
        if (rightText.HorizontalPosition != 0) throw new Exception("Narrow destination horizontal scroll was not clamped.");
        // Readiness must be independent of retained row count.
        string temporary = Path.Combine(Path.GetTempPath(), "DiffBeacon-Equal-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(temporary, []); mode.SelectedItem = "Binary"; leftPath.Text = rightPath.Text = temporary; await Compare();
            if (!completedResult || reportRows.Count != 0) throw new Exception("Equal binary summary was not report-ready.");
            mode.SelectedItem = "Tables"; await Compare();
            if (!completedResult) throw new Exception("Empty table summary was not report-ready.");
            File.WriteAllText(temporary, "a\tb\n1\t2"); delimiter.Text = "\\t"; await Compare(false);
            if (!completedResult || reportRows.Count != 4) throw new Exception("Applied tab separator did not split actual table input.");
        }
        finally { File.Delete(temporary); leftPath.Clear(); rightPath.Clear(); mode.SelectedItem = "Text"; dirtyLeft = dirtyRight = false; SetTexts("", ""); }
    }
    async Task RunLazyPaintSmoke()
    {
        string left = string.Join('\n', Enumerable.Range(0, 500).Select(i => $"row {i:D3} old"));
        string right = string.Join('\n', Enumerable.Range(0, 500).Select(i => $"row {i:D3} new"));
        granularity.SelectedIndex = (int)HighlightGranularity.Word;
        SetTexts(left, right); await Compare();
        Color highlight = SystemInformation.HighContrast ? SystemColors.Highlight : DarkTheme.Amber;
        void CheckColor(ComparisonEditor box, int line, Color expected, string reason, bool prefix = false)
        {
            int start = box.SelectionStart, length = box.SelectionLength;
            try
            {
                int end = line + 1 < box.LogicalStarts.Length ? box.LogicalStarts[line + 1] - 1 : box.TextLength;
                box.Select(prefix ? box.LogicalStarts[line] : end - 3, 3);
                if (box.SelectionBackColor.ToArgb() != expected.ToArgb()) throw new Exception(reason);
            }
            finally { box.Select(start, length); }
        }
        async Task WaitForPaint()
        {
            await Task.Delay(20);
            if (visiblePaintTask != null) await visiblePaintTask;
        }
        if (paintedDetail.Count(value => value) >= visiblePaintPlan.Length || results.Rows.Count != 500) throw new Exception("Lazy renderer eagerly painted offscreen content or changed retained grid rows.");
        CheckColor(leftText, 300, leftText.BackColor, "Offscreen fixture was unexpectedly painted.");
        SetSelectedDifference(300); await WaitForPaint();
        CheckColor(leftText, 300, highlight, "Difference navigation did not paint newly visible left detail.");
        CheckColor(rightText, 300, highlight, "Difference navigation did not paint newly visible right detail.");
        leftText.Select(3, 2); rightText.Select(4, 2); bool modified = leftText.Modified;
        leftText.ScrollDisplayTo(120); await WaitForPaint();
        if (leftText.SelectionStart != 3 || leftText.SelectionLength != 2 || rightText.SelectionStart != 4 || leftText.Modified != modified) throw new Exception("Lazy scrolling changed selection or modified state.");
        CheckColor(leftText, 120, highlight, "Scrolling did not paint left detail.");
        CheckColor(rightText, 120, highlight, "Synchronized scrolling did not paint right detail.");
        syncScrolling.Checked = false; rightText.ScrollDisplayTo(410); await WaitForPaint();
        CheckColor(rightText, 410, highlight, "Independent scrolling did not paint right detail.");
        string wide = new('q', 500);
        SetTexts(string.Join('\n', Enumerable.Range(0, 30).Select(i => $"row {i:D3} {wide} old")), string.Join('\n', Enumerable.Range(0, 30).Select(i => $"row {i:D3} {wide} new")));
        wrap.Checked = true; await Compare();
        CheckColor(leftText, 5, leftText.BackColor, "Wrapped fixture did not leave a later logical line offscreen.");
        wrap.Checked = false; await WaitForPaint();
        CheckColor(leftText, 5, highlight, "Wrap reflow left newly visible detail unpainted.");
        SetTexts(left, right); await Compare(); leftText.ScrollDisplayTo(120); await WaitForPaint();
        syncScrolling.Checked = true;
        var pending = ColorTextAsync(); leftText.Select(leftText.TextLength, 0); leftText.SelectedText = " stale"; await pending;
        for (int i = 0; i < 100 && clearQueued; i++) await Task.Delay(1);
        if (detail != null || textResult != null || completedResult || visiblePaintPlan.Length != 0 || hasDecoration) throw new Exception("Pending lazy paint survived input mutation.");
        CheckColor(leftText, 120, leftText.BackColor, "Stale lazy highlight survived invalidation.");
        SetTexts(left, right); var cancelled = Compare();
        if (!cancel.Enabled || cancellation == null) throw new Exception("Running comparison did not enable Cancel.");
        cancel.PerformClick(); await cancelled;
        if (cancel.Enabled || completedResult || textResult != null || detail != null || visiblePaintPlan.Length != 0) throw new Exception("Cancelled comparison retained a lazy paint plan or enabled Cancel.");
        var superseded = Compare(); SetTexts("latest old", "latest new"); await Compare(); await superseded;
        if (!completedResult || cancel.Enabled || comparedLeft != "latest old" || comparedRight != "latest new" || detail == null) throw new Exception("An obsolete comparison replaced the latest lazy result or left Cancel enabled.");
        var changingMode = ColorTextAsync(); mode.SelectedItem = "Tables"; await changingMode;
        if (detail != null || visiblePaintPlan.Length != 0 || completedResult) throw new Exception("Mode change retained lazy text detail.");
        mode.SelectedItem = "Text";

        static IEnumerable<string> LargeRows(int count, string changed) =>
            Enumerable.Range(0, count).Select(i => $"row {i:D4} {(i % 4 == 2 ? changed : "same")}");
        var shifted = LargeRows(989, "new").ToList();
        shifted.Insert(256, "right-only insertion");
        SetTexts(string.Join('\n', LargeRows(1999, "old")), string.Join('\n', shifted));
        await Compare();
        if (textResult?.Rows.Count != 2000 || scrollMap == null) throw new Exception("Large scroll fixture did not produce 2,000 exactly aligned rows.");
        int initiallyPainted = paintedDetail.Count(value => value);
        leftText.ScrollDisplayTo(402);
        if (leftText.FirstDisplayLine != 402 || rightText.FirstDisplayLine != 403) throw new Exception("Large forward scroll did not immediately map the shifted row.");
        if (paintedDetail.Count(value => value) != initiallyPainted) throw new Exception("The large native scroll handler painted inline detail before returning.");
        rightText.ScrollDisplayTo(607);
        if (leftText.FirstDisplayLine != 606 || rightText.FirstDisplayLine != 607) throw new Exception("Large reverse scroll did not immediately map the newest row.");
        if (visiblePaintTask is { } newestPaint) await newestPaint;
        if (leftText.FirstDisplayLine != 606 || rightText.FirstDisplayLine != 607) throw new Exception("Deferred paint moved the newest viewport.");
        CheckColor(leftText, 606, highlight, "Newest large scroll viewport lacked left word highlighting.");
        CheckColor(rightText, 607, highlight, "Newest large scroll viewport lacked right word highlighting.");
        CheckColor(leftText, 606, leftText.BackColor, "Newest left word highlighting colored unchanged text.", prefix: true);
        CheckColor(rightText, 607, rightText.BackColor, "Newest right word highlighting colored unchanged text.", prefix: true);
        CheckColor(leftText, 402, leftText.BackColor, "A superseded large scroll viewport was unnecessarily painted.");
        CheckColor(rightText, 403, rightText.BackColor, "A superseded paired viewport was unnecessarily painted.");
        Console.WriteLine("Large aligned scroll: both directions mapped immediately and deferred word detail followed the latest viewport.");
        Console.WriteLine("Lazy detail navigation, scrolling, wrap, selection and stale-generation checks: passed.");
    }
    async Task RunBlockMergeSmoke(string screenshot)
    {
        string a = "header\nleft-one\nleft-two\nanchor\nleft-late\ntail\n", b = "header\nright-one\nright-two\nanchor\nright-late\ntail\n";
        void CheckRejected()
        {
            string l = leftText.Text, r = rightText.Text; int count = undo.Count; bool rejected = false;
            try { MergeBlock(true); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || l != leftText.Text || r != rightText.Text || undo.Count != count) throw new Exception("Rejected block merge mutated editor/undo state.");
        }
        mode.SelectedItem = "Text"; leftPath.Clear(); rightPath.Clear(); SetTexts(a, b); await Compare();
        if (blockToRight.Enabled || blockToLeft.Enabled) throw new Exception("Block merge enabled without a selected difference."); CheckRejected();
        Navigate(1); Navigate(1); Navigate(1); if (differenceIndex != 2 || !blockToRight.Enabled) throw new Exception("Previous/Next did not select the later aligned block.");
        int undoBefore = undo.Count; MergeBlock(true);
        if (leftText.Text != a || rightText.Text != b.Replace("right-late", "left-late", StringComparison.Ordinal) || undo.Count != undoBefore + 1 || status.Text != "Block Merged Into Right Editor · Review And Save As To Write A File" || textResult != null || results.Rows.Count != 0 || scrubber.MarkerCount != 0 || differenceIndex != -1 || blockToRight.Enabled) throw new Exception("Right aligned block merge or invalidation failed.");
        Undo(); if (leftText.Text != a || rightText.Text != b || undo.Count != undoBefore) throw new Exception("Block undo did not restore exact editor snapshots.");
        await Compare(); scrubber.ActivateAt(scrubber.MarkerX(1)); if (differenceIndex != 1) throw new Exception("Overview selected the wrong difference ordinal."); MergeBlock(false);
        if (leftText.Text != a.Replace("left-one\nleft-two", "right-one\nright-two", StringComparison.Ordinal) || rightText.Text != b) throw new Exception("Overview block merge did not replace the whole contiguous hunk only.");
        Undo(); if (leftText.Text != a || rightText.Text != b) throw new Exception("Left block undo changed unrelated content.");
        foreach (var option in new[] { ignoreCase, ignoreSpace, ignoreBlank }) { option.Checked = true; await Compare(); Navigate(1); if (blockToRight.Enabled || blockToLeft.Enabled) throw new Exception("Block merge enabled with ignore options."); CheckRejected(); option.Checked = false; }
        SetTexts(string.Join('\n', Enumerable.Range(0, 1500)), string.Join('\n', Enumerable.Range(0, 1500)) + "x"); await Compare(); Navigate(1); if (textResult?.PositionalFallback != true || blockToRight.Enabled) throw new Exception("Fallback alignment enabled block merge."); CheckRejected();
        SetTexts(a, b); await Compare(); Navigate(1); leftText.AppendText("changed"); if (blockToRight.Enabled) throw new Exception("Editor mutation retained block-merge eligibility."); CheckRejected();
        SetTexts("take", "leave"); leftText.SelectAll(); rightText.SelectAll(); Merge(true, false); if (rightText.Text != "take") throw new Exception("Existing free-text merge changed behavior."); Undo(); if (rightText.Text != "leave") throw new Exception("Existing free-text undo changed behavior.");
        SetTexts(a, b); await Compare(); Navigate(1);
        var changedRow = results.Rows.Cast<DataGridViewRow>().First(r => Equals(r.Cells[0].Value, "Changed"));
        var cellStyle = changedRow.Cells[0].InheritedStyle;
        if (cellStyle.BackColor != DarkTheme.Amber || cellStyle.ForeColor != DarkTheme.AmberText || cellStyle.SelectionBackColor != DarkTheme.Amber || cellStyle.SelectionForeColor != DarkTheme.AmberText) throw new Exception("Changed grid row normal/selected contrast regressed.");
        var form = FindForm()!; form.Width = 1540; form.Height = 980; await Task.Delay(150); ((DarkWindow)form).CapturePreview(screenshot);
        dirtyLeft = dirtyRight = false;
    }
    void AssertClearedStatus(string expected)
    {
        if (status.Text != expected || textResult != null || folderResult != null || reportRows.Count != 0 || results.Rows.Count != 0 || scrubber.MarkerCount != 0 || help.GetToolTip(folderScope) != folderScope.Text)
            throw new Exception("Invalidated comparison retained stale feedback/results; expected footer: " + expected);
    }
    async Task RunFolderSmoke(string screenshot)
    {
        string root = Path.Combine(Path.GetTempPath(), "DiffBeacon-Folder-Smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string side in new[] { "Left", "Right" })
            {
                string folder = Path.Combine(root, side); Directory.CreateDirectory(Path.Combine(folder, "src")); Directory.CreateDirectory(Path.Combine(folder, "bin"));
                File.WriteAllText(Path.Combine(folder, "src", "Beacon.cs"), side); File.WriteAllText(Path.Combine(folder, "src", "Shared.cs"), "same");
                File.WriteAllText(Path.Combine(folder, "src", "Generated.g.cs"), "excluded"); File.WriteAllText(Path.Combine(folder, "notes.txt"), "excluded"); File.WriteAllText(Path.Combine(folder, "bin", "Pruned.cs"), "pruned");
            }
            leftPath.Text = Path.Combine(root, "Left"); rightPath.Text = Path.Combine(root, "Right"); mode.SelectedItem = "Folders"; fileFilter.Text = "*.cs;NOT *.g.cs"; ignoredDirectories.Text = "bin";
            await Compare(); if (folderResult?.Rows.Count != 2 || folderResult.LeftCounts?.ExcludedFiles != 2 || folderResult.LeftCounts.PrunedDirectories != 1 || !summary.Contains("Filtered Scope")) throw new Exception("Filtered folder UI comparison failed.");
            string sessionPath = Path.Combine(root, "Scope.json"); var session = new Session(2, "Folder Scope", Mode, leftPath.Text, rightPath.Text, Options, fileFilter.Text, ignoredDirectories.Text); session.Save(sessionPath);
            fileFilter.Text = "*.txt"; AssertClearedStatus("Inputs Changed · Compare Again");
            ApplySession(Session.Load(sessionPath)); mode.SelectedItem = "Text"; mode.SelectedItem = "Folders"; if (fileFilter.Text != "*.cs;NOT *.g.cs" || ignoredDirectories.Text != "bin") throw new Exception("Session/mode change lost folder scope.");
            var pending = Compare(); fileFilter.Text = "*.png"; await pending; if (folderResult != null || reportRows.Count != 0) throw new Exception("Stale folder scope results were published.");
            await Compare(); if (folderResult?.Rows.Count != 0 || !summary.Contains("No Files Match These Filters")) throw new Exception("Empty filtered-scope message failed.");
            fileFilter.Text = session.FileFilter; await Compare();
            ignoredDirectories.Text = "bin;obj"; AssertClearedStatus("Inputs Changed · Compare Again"); ignoredDirectories.Text = session.IgnoredDirectories; await Compare();
            fileFilter.Text = "**"; bool invalidFilterRejected = false;
            try { await Compare(false); } catch (ArgumentException) { invalidFilterRejected = true; }
            if (!invalidFilterRejected || !status.Text.StartsWith("Error · ", StringComparison.Ordinal) || folderResult != null || reportRows.Count != 0) throw new Exception("Compare error retained stale counts/results.");
            fileFilter.Text = session.FileFilter; await Compare(); if (status.Text != summary) throw new Exception("Folder completion footer did not show current scope counts.");
            var form = FindForm()!; form.Width = 1440; form.Height = 980; await Task.Delay(150); ((DarkWindow)form).CapturePreview(screenshot);
        }
        finally { dirtyLeft = dirtyRight = false; if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    protected override void Dispose(bool disposing) { if (disposing) { disposed = true; cancellation?.Cancel(); CancelDetail(); help.Dispose(); CommandBar.Parent?.Controls.Remove(CommandBar); CommandBar.Dispose(); } base.Dispose(disposing); }
}
