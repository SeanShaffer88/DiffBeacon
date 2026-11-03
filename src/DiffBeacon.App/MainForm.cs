using System.Text;
using DiffBeacon.Core;

namespace DiffBeacon.App;

public sealed class MainForm : Form
{
    public bool SmokeFailed { get; private set; }
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly string? smoke;
    public MainForm(string mode, string left, string right, string? smokePath)
    {
        smoke = smokePath; Text = "DiffBeacon — Compare With Clarity"; Width = 1260; Height = 850; MinimumSize = new(900, 620); Font = new("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File"); file.DropDownItems.Add("New Comparison", null, (_, _) => Add("Text", "", "")); file.DropDownItems.Add("Open Session…", null, (_, _) => OpenSession()); file.DropDownItems.Add("Close Tab", null, (_, _) => CloseTab()); file.DropDownItems.Add("Exit", null, (_, _) => Close()); menu.Items.Add(file);
        menu.Items.Add("About", null, (_, _) => MessageBox.Show(this, "DiffBeacon · Core Preview\nNative, local-first file comparison.\nSee docs/FEATURES.md for current limits.\nNo automatic network access or telemetry.", "About DiffBeacon"));
        var heading = new Label { Text = "DiffBeacon    /    Compare With Clarity", Dock = DockStyle.Top, Height = 55, Padding = new(18, 12, 0, 0), ForeColor = Color.FromArgb(235, 239, 244), BackColor = Color.FromArgb(29, 32, 37), Font = new("Segoe UI Semibold", 16) };
        Controls.Add(tabs); Controls.Add(heading); Controls.Add(menu); MainMenuStrip = menu;
        Add(mode, left, right);
        FormClosing += (_, e) => { foreach (TabPage tab in tabs.TabPages) { if (tab.Controls[0] is CompareView view && !view.CanClose()) { e.Cancel = true; return; } } };
        Shown += async (_, _) => { if (smoke != null) { try { var view = (CompareView)tabs.SelectedTab!.Controls[0]; await view.RunSmoke(smoke); } catch (Exception ex) { Console.Error.WriteLine(ex); SmokeFailed = true; } Close(); } };
    }
    void Add(string mode, string left, string right)
    { var tab = new TabPage($"{mode} {tabs.TabCount + 1}"); var view = new CompareView(mode, left, right) { Dock = DockStyle.Fill }; tab.Controls.Add(view); tabs.TabPages.Add(tab); tabs.SelectedTab = tab; }
    void CloseTab() { if (tabs.SelectedTab is { } tab && ((CompareView)tab.Controls[0]).CanClose()) { tabs.TabPages.Remove(tab); tab.Dispose(); if (tabs.TabCount == 0) Add("Text", "", ""); } }
    void OpenSession()
    {
        using var dialog = new OpenFileDialog { Filter = "DiffBeacon Session|*.json" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { var s = Session.Load(dialog.FileName); Add(s.Mode, s.Left, s.Right); ((CompareView)tabs.SelectedTab!.Controls[0]).SetOptions(s.Options); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Session Could Not Be Opened"); }
    }
}

sealed class CompareView : UserControl
{
    readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    readonly TextBox leftPath = new() { Dock = DockStyle.Fill }, rightPath = new() { Dock = DockStyle.Fill };
    readonly RichTextBox leftText = Editor(), rightText = Editor();
    readonly DataGridView results = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White };
    readonly SplitContainer editors = new() { Dock = DockStyle.Fill, SplitterWidth = 6 };
    readonly TrackBar scrubber = new() { Dock = DockStyle.Top, Height = 38, Minimum = 0, Maximum = 0, TickStyle = TickStyle.None, Enabled = false };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 38, Padding = new(12, 9, 0, 0), BackColor = Color.FromArgb(29, 32, 37), ForeColor = Color.FromArgb(173, 182, 193), AutoEllipsis = true };
    readonly CheckBox ignoreCase = new() { Text = "Ignore Case", AutoSize = true }, ignoreSpace = new() { Text = "Ignore Whitespace", AutoSize = true }, ignoreBlank = new() { Text = "Ignore Blank Lines", AutoSize = true }, wrap = new() { Text = "Word Wrap", AutoSize = true };
    readonly TextBox delimiter = new() { Text = ",", Width = 28 };
    readonly Button compare = new() { Text = "Compare", AutoSize = true };
    readonly Stack<(string Left, string Right)> undo = new();
    TextDocument? leftDoc, rightDoc;
    TextResult? textResult; FolderResult? folderResult;
    CancellationTokenSource? cancellation;
    bool dirtyLeft, dirtyRight, suppress, disposed;
    string summary = "Choose Files Or Paste Text, Then Compare";
    List<string?[]> reportRows = new();

    static RichTextBox Editor() => new() { Dock = DockStyle.Fill, Font = new("Consolas", 11), AcceptsTab = true, WordWrap = false, DetectUrls = false, BorderStyle = BorderStyle.None, BackColor = Color.White, MaxLength = TextComparison.MaxCharacters / 2, HideSelection = false };
    static void StyleButton(Button button) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(68, 76, 86); button.BackColor = Color.FromArgb(45, 50, 58); button.ForeColor = Color.FromArgb(225, 229, 235); }
    public CompareView(string initialMode, string left, string right)
    {
        BackColor = Color.FromArgb(30, 33, 38); ForeColor = Color.FromArgb(225, 229, 235); mode.Items.AddRange(["Text", "Folders", "Binary", "Tables"]); mode.SelectedItem = initialMode; StyleButton(compare);
        results.BackgroundColor = Color.FromArgb(37, 41, 47); results.GridColor = Color.FromArgb(53, 59, 67); results.DefaultCellStyle.BackColor = Color.FromArgb(37, 41, 47); results.DefaultCellStyle.ForeColor = Color.FromArgb(225, 229, 235); results.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 118, 128); results.DefaultCellStyle.SelectionForeColor = Color.White; results.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 50, 58); results.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(225, 229, 235); results.EnableHeadersVisualStyles = false;
        leftPath.Text = left; rightPath.Text = right;
        var paths = new TableLayoutPanel { Dock = DockStyle.Top, Height = 82, ColumnCount = 3, RowCount = 2, Padding = new(10, 5, 10, 5) };
        paths.ColumnStyles.Add(new(SizeType.Absolute, 55)); paths.ColumnStyles.Add(new(SizeType.Percent, 100)); paths.ColumnStyles.Add(new(SizeType.Absolute, 90));
        paths.BackColor = BackColor; void PathRow(int row, string label, TextBox path) { path.BackColor = Color.FromArgb(37, 41, 47); path.ForeColor = Color.FromArgb(225, 229, 235); paths.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new(0, 5, 0, 0), ForeColor = ForeColor, BackColor = BackColor }, 0, row); paths.Controls.Add(path, 1, row); var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill }; StyleButton(browse); browse.Click += (_, _) => Browse(path); paths.Controls.Add(browse, 2, row); }
        PathRow(0, "Left", leftPath); PathRow(1, "Right", rightPath);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new(10, 4, 0, 0), WrapContents = false, AutoScroll = true }; tools.Controls.Add(mode); tools.Controls.Add(compare); compare.Click += async (_, _) => await Compare();
        tools.BackColor = BackColor; Button Tool(string title, Action action) { var button = new Button { Text = title, AutoSize = true }; StyleButton(button); button.Click += (_, _) => { try { action(); } catch (Exception ex) { Error(ex); } }; tools.Controls.Add(button); return button; }
        Tool("Cancel", () => cancellation?.Cancel()); Tool("Swap", Swap); Tool("New Snippets", NewSnippets); Tool("Save Session…", SaveSession); Tool("Export HTML…", Export);
        var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 37, Padding = new(10, 5, 0, 0), WrapContents = false, AutoScroll = true, BackColor = BackColor }; foreach (var check in new[] { ignoreCase, ignoreSpace, ignoreBlank, wrap }) { check.ForeColor = ForeColor; check.BackColor = BackColor; } delimiter.BackColor = Color.FromArgb(37, 41, 47); delimiter.ForeColor = ForeColor; options.Controls.AddRange([ignoreCase, ignoreSpace, ignoreBlank, wrap, new Label { Text = "Table Separator", AutoSize = true, ForeColor = ForeColor, BackColor = BackColor }, delimiter]);
        wrap.CheckedChanged += (_, _) => leftText.WordWrap = rightText.WordWrap = wrap.Checked;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new(10, 3, 0, 0), WrapContents = false, AutoScroll = true, BackColor = BackColor };
        void Action(string title, System.Action action) { var b = new Button { Text = title, AutoSize = true }; StyleButton(b); b.Click += (_, _) => { try { action(); } catch (Exception ex) { Error(ex); } }; actions.Controls.Add(b); }
        Action("Previous Difference", () => Navigate(-1)); Action("Next Difference", () => Navigate(1)); Action("Selected → Right", () => Merge(true, false)); Action("Left ← Selected", () => Merge(false, false)); Action("All → Right", () => Merge(true, true)); Action("Undo Merge", Undo); Action("Save Left As…", () => Save(false)); Action("Save Right As…", () => Save(true));
        editors.Orientation = Orientation.Horizontal; editors.Panel1.Controls.Add(leftText); editors.Panel2.Controls.Add(rightText);
        leftText.BackColor = rightText.BackColor = Color.FromArgb(32, 35, 40); leftText.ForeColor = rightText.ForeColor = Color.FromArgb(220, 224, 231); editors.Panel1.BackColor = editors.Panel2.BackColor = Color.FromArgb(26, 29, 34);
        scrubber.BackColor = Color.FromArgb(37, 41, 47); scrubber.ValueChanged += (_, _) => JumpToDifference(scrubber.Value);
        var workspace = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 }; workspace.Panel1.Controls.Add(editors); workspace.Panel2.Controls.Add(results);
        Controls.Add(workspace); Controls.Add(scrubber); Controls.Add(actions); Controls.Add(options); Controls.Add(paths); Controls.Add(tools); Controls.Add(status);
        Resize += (_, _) => { if (editors.Height > 150) editors.SplitterDistance = editors.Height / 2; if (workspace.Height > 150) workspace.SplitterDistance = workspace.Height * 4 / 5; };
        leftText.TextChanged += (_, _) => { if (!suppress) { dirtyLeft = true; InvalidateComparison(); status.Text = "Left Edited · Compare Again Before Navigating Differences"; } };
        rightText.TextChanged += (_, _) => { if (!suppress) { dirtyRight = true; InvalidateComparison(); status.Text = "Right Edited · Compare Again Before Navigating Differences"; } };
        foreach (var path in new[] { leftPath, rightPath }) path.TextChanged += (_, _) => InvalidateComparison();
        foreach (var option in new[] { ignoreCase, ignoreSpace, ignoreBlank }) option.CheckedChanged += (_, _) => InvalidateComparison();
        delimiter.TextChanged += (_, _) => InvalidateComparison();
        mode.SelectedIndexChanged += (_, _) => { InvalidateComparison(); UpdateMode(); }; UpdateMode();
        foreach (var box in new[] { leftPath, rightPath }) { box.AllowDrop = true; box.DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; }; box.DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) box.Text = files[0]; }; }
        results.CellDoubleClick += (_, e) => { if (Mode == "Folders" && folderResult != null && e.RowIndex >= 0) { string relative = Convert.ToString(results.Rows[e.RowIndex].Cells[1].Value) ?? ""; var row = folderResult.Rows.FirstOrDefault(r => r.RelativePath == relative); if (row != null && row.Status != Change.Error) { var l = Path.Combine(leftPath.Text, row.RelativePath); var r = Path.Combine(rightPath.Text, row.RelativePath); if (File.Exists(l) && File.Exists(r) && CanClose()) { leftPath.Text = l; rightPath.Text = r; leftDoc = rightDoc = null; mode.SelectedItem = "Text"; } } } };
    }
    string Mode => (string)mode.SelectedItem!;
    CompareOptions Options => new(ignoreCase.Checked, ignoreSpace.Checked, ignoreBlank.Checked);
    void InvalidateComparison() { cancellation?.Cancel(); textResult = null; folderResult = null; reportRows.Clear(); summary = "Inputs Changed · Compare Again"; results.Rows.Clear(); differenceIndex = -1; }
    public void SetOptions(CompareOptions o) { ignoreCase.Checked = o.IgnoreCase; ignoreSpace.Checked = o.IgnoreWhitespace; ignoreBlank.Checked = o.IgnoreBlankLines; }
    void UpdateMode() { editors.Visible = Mode == "Text"; status.Text = Mode == "Binary" ? "Full-File Streaming · Positional Bytes · Bounded Samples" : "Choose Inputs And Compare · Folder And Table Modes Are Read-Only"; }
    void Error(Exception ex) { status.Text = "Error · " + ex.Message; if (FindForm()?.Visible == true) MessageBox.Show(this, ex.Message, "DiffBeacon", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    void Browse(TextBox target)
    {
        if (Mode == "Folders") { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.SelectedPath; }
        else { using var d = new OpenFileDialog(); if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.FileName; }
    }
    bool ConfirmDiscard() => !(dirtyLeft || dirtyRight) || MessageBox.Show(this, "Discard Unsaved Editor Changes?", "DiffBeacon", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    public bool CanClose() => ConfirmDiscard();
    void NewSnippets() { if (!ConfirmDiscard()) return; InvalidateComparison(); leftPath.Clear(); rightPath.Clear(); leftDoc = rightDoc = null; SetTexts("", ""); dirtyLeft = dirtyRight = false; undo.Clear(); mode.SelectedItem = "Text"; }
    void SetTexts(string left, string right) { suppress = true; try { leftText.Text = left; rightText.Text = right; } finally { suppress = false; } }
    void Swap() { InvalidateComparison(); undo.Clear(); (leftPath.Text, rightPath.Text) = (rightPath.Text, leftPath.Text); (leftDoc, rightDoc) = (rightDoc, leftDoc); (dirtyLeft, dirtyRight) = (dirtyRight, dirtyLeft); var a = leftText.Text; SetTexts(rightText.Text, a); }
    async Task Compare()
    {
        InvalidateComparison(); var cts = new CancellationTokenSource(); cancellation = cts; var token = cts.Token;
        compare.Enabled = false; status.Text = "Comparing…";
        try
        {
            var selectedMode = Mode; string lp = leftPath.Text, rp = rightPath.Text; var options = Options;
            if (selectedMode == "Text")
            {
                bool loadLeft = lp.Length > 0 && (leftDoc == null || !string.Equals(leftDoc.Path, Path.GetFullPath(lp), StringComparison.OrdinalIgnoreCase));
                bool loadRight = rp.Length > 0 && (rightDoc == null || !string.Equals(rightDoc.Path, Path.GetFullPath(rp), StringComparison.OrdinalIgnoreCase));
                if ((loadLeft && dirtyLeft || loadRight && dirtyRight) && !ConfirmDiscard()) return;
                var ld = loadLeft ? await Task.Run(() => TextDocument.Load(lp), token) : leftDoc; var rd = loadRight ? await Task.Run(() => TextDocument.Load(rp), token) : rightDoc;
                if (disposed || cts != cancellation || token.IsCancellationRequested) return;
                if (loadLeft || loadRight) undo.Clear();
                if (loadLeft) { leftDoc = ld; suppress = true; leftText.Text = ld!.Text; suppress = false; dirtyLeft = false; }
                if (loadRight) { rightDoc = rd; suppress = true; rightText.Text = rd!.Text; suppress = false; dirtyRight = false; }
                var lt = leftText.Text; var rt = rightText.Text; var result = await Task.Run(() => TextComparison.Compare(lt, rt, options, token), token);
                if (disposed || cts != cancellation || token.IsCancellationRequested) return;
                textResult = result; summary = result.Summary; var diffCount = result.Rows.Count(r => r.Status != Change.Equal); scrubber.Maximum = Math.Max(0, diffCount - 1); scrubber.Enabled = diffCount > 0; differenceIndex = -1; reportRows = result.Rows.Select(r => new string?[] { r.Status.ToString(), r.LeftLine?.ToString(), r.Left, r.RightLine?.ToString(), r.Right }).ToList();
                Grid(["Status", "Left Line", "Left", "Right Line", "Right"], reportRows.Take(10000)); ColorText();
                if (reportRows.Count > 10000) summary += " · Grid Shows First 10,000 Rows; Export Contains All Retained Rows";
            }
            else if (selectedMode == "Binary")
            {
                var result = await Task.Run(() => BinaryComparison.Compare(lp, rp, token), token); if (disposed || cts != cancellation || token.IsCancellationRequested) return;
                summary = result.Summary; reportRows = result.Samples.Select(s => new string?[] { "0x" + s.Offset.ToString("X"), s.Offset.ToString(), Hex(s.Left), Ascii(s.Left), Hex(s.Right), Ascii(s.Right) }).ToList(); Grid(["Hex Offset", "Decimal Offset", "Left Hex", "Left ASCII", "Right Hex", "Right ASCII"], reportRows);
            }
            else if (selectedMode == "Folders")
            {
                var result = await Task.Run(() => FolderComparison.Compare(lp, rp, token), token); if (disposed || cts != cancellation || token.IsCancellationRequested) return;
                folderResult = result; summary = result.Summary; reportRows = result.Rows.Select(r => new string?[] { r.Status.ToString(), r.RelativePath, r.LeftSize?.ToString(), r.RightSize?.ToString(), r.Detail }).ToList(); Grid(["Status", "Relative Path", "Left Bytes", "Right Bytes", "Detail"], reportRows.Take(10000)); if (reportRows.Count > 10000) summary += " · Grid Shows First 10,000; Export Includes Retained Rows";
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
                if (disposed || cts != cancellation || token.IsCancellationRequested) return; summary = table.Summary; reportRows = table.Data; Grid(["Status", "Row", "Column", "Left", "Right"], reportRows);
            }
            status.Text = summary;
        }
        catch (OperationCanceledException) { if (!disposed && cts == cancellation) status.Text = "Cancelled · Comparison Is Incomplete"; }
        catch (Exception ex) { if (!disposed && cts == cancellation) Error(ex); }
        finally { if (!disposed && cts == cancellation) { compare.Enabled = true; cancellation = null; } cts.Dispose(); }
    }
    static string Hex(byte? value) => value?.ToString("X2") ?? "—";
    static string Ascii(byte? value) => !value.HasValue ? "—" : value >= 32 && value <= 126 ? ((char)value).ToString() : ".";
    void Grid(string[] columns, IEnumerable<string?[]> rows)
    {
        results.Rows.Clear(); results.Columns.Clear(); foreach (string name in columns) results.Columns.Add(name, name);
        foreach (var row in rows) { int index = results.Rows.Add(row.Select(x => (object)(x ?? "")).ToArray()); if (row[0] is "Changed" or "LeftOnly" or "RightOnly" or "Error") results.Rows[index].DefaultCellStyle.BackColor = Color.FromArgb(255, 241, 204); }
    }
    void ColorText()
    {
        if (textResult == null) return;
        suppress = true;
        try { foreach (var box in new[] { leftText, rightText }) { box.SelectAll(); box.SelectionBackColor = box.BackColor; box.SelectionColor = box.ForeColor; foreach (var row in textResult.Rows.Where(r => r.Status != Change.Equal).Take(2000)) { int? line = box == leftText ? row.LeftLine : row.RightLine; if (line.HasValue) { int at = box.GetFirstCharIndexFromLine(line.Value - 1); if (at >= 0) { box.Select(at, box.Lines.ElementAtOrDefault(line.Value - 1)?.Length ?? 0); box.SelectionBackColor = Color.FromArgb(244, 194, 89); box.SelectionColor = Color.FromArgb(32, 35, 40); } } } box.Select(0, 0); } }
        finally { suppress = false; }
    }
    int differenceIndex = -1;
    void Navigate(int delta)
    {
        if (textResult == null) return; var differences = textResult.Rows.Where(r => r.Status != Change.Equal).ToArray(); if (differences.Length == 0) return;
        differenceIndex = (differenceIndex + delta + differences.Length) % differences.Length; var row = differences[differenceIndex];
        void SelectLine(RichTextBox box, int? line) { if (line.HasValue) { int at = box.GetFirstCharIndexFromLine(line.Value - 1); if (at >= 0) { box.Select(at, 0); box.ScrollToCaret(); } } }
        SelectLine(leftText, row.LeftLine); SelectLine(rightText, row.RightLine); status.Text = $"Difference {differenceIndex + 1} Of {differences.Length} · {row.Status}";
        if (scrubber.Value != differenceIndex) scrubber.Value = differenceIndex;
    }
    void JumpToDifference(int index)
    {
        if (textResult == null) return;
        var differences = textResult.Rows.Where(r => r.Status != Change.Equal).ToArray();
        if (index < 0 || index >= differences.Length) return;
        differenceIndex = index - 1;
        Navigate(1);
    }
    void Merge(bool toRight, bool all)
    {
        if (Mode != "Text") return; var source = toRight ? leftText : rightText; var destination = toRight ? rightText : leftText;
        if (!all && source.SelectionLength == 0) throw new InvalidOperationException("Select source text and a destination range/caret first.");
        undo.Push((leftText.Text, rightText.Text)); while (undo.Count > 20) { var recent = undo.Take(20).Reverse().ToArray(); undo.Clear(); foreach (var item in recent) undo.Push(item); }
        if (all) destination.Text = source.Text; else destination.SelectedText = source.SelectedText; textResult = null; status.Text = "Merged Into Editor · Review And Save As To Write A File";
    }
    void Undo() { if (undo.TryPop(out var item)) { SetTexts(item.Left, item.Right); dirtyLeft = dirtyRight = true; InvalidateComparison(); status.Text = "Merge Undone · Compare Again"; } }
    void Save(bool right)
    {
        if (Mode != "Text") return; using var dialog = new SaveFileDialog { FileName = Path.GetFileName(right ? rightPath.Text : leftPath.Text), OverwritePrompt = true, Filter = "All Files|*.*" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var document = right ? rightDoc : leftDoc; var other = right ? leftDoc : rightDoc; string destination = Path.GetFullPath(dialog.FileName); string text = right ? rightText.Text : leftText.Text;
        // An input's original fingerprint always wins over a new Save As snapshot.
        var source = new[] { document, other }.FirstOrDefault(d => d != null && string.Equals(d.Path, destination, StringComparison.OrdinalIgnoreCase));
        var expected = source?.Stamp ?? (File.Exists(destination) ? FileStamp.Capture(destination) : null);
        byte[] bytes = document?.Encode(text) ?? new UTF8Encoding(false, true).GetBytes(text);
        SafeFiles.Write(destination, bytes, expected, File.Exists(destination));
        var updated = TextDocument.Load(destination);
        if (right) { rightDoc = updated; rightPath.Text = destination; dirtyRight = false; } else { leftDoc = updated; leftPath.Text = destination; dirtyLeft = false; }
        if (other != null && string.Equals(other.Path, destination, StringComparison.OrdinalIgnoreCase)) { if (right) leftDoc = updated; else rightDoc = updated; }
        status.Text = "Saved · Existing Destination Backed Up Beside File";
    }
    void SaveSession()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiffBeacon", "Sessions"); Directory.CreateDirectory(folder);
        using var dialog = new SaveFileDialog { InitialDirectory = folder, Filter = "DiffBeacon Session|*.json", DefaultExt = "json", FileName = "Comparison.json" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        new Session(1, Path.GetFileNameWithoutExtension(dialog.FileName), Mode, leftPath.Text, rightPath.Text, Options).Save(dialog.FileName); status.Text = "Session Metadata Saved · Editor Contents Are Not Stored";
    }
    void Export()
    {
        if (reportRows.Count == 0) throw new InvalidOperationException("Compare inputs first. Empty/equal binary summaries can still be viewed in the status bar.");
        using var dialog = new SaveFileDialog { Filter = "HTML Report|*.html", DefaultExt = "html", FileName = "DiffBeacon.html" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var bytes = Encoding.UTF8.GetBytes(Reports.Html("DiffBeacon · " + Mode, leftPath.Text + " ↔ " + rightPath.Text + " · " + summary, reportRows)); SafeFiles.Write(dialog.FileName, bytes, File.Exists(dialog.FileName) ? FileStamp.Capture(dialog.FileName) : null, true);
    }
    public async Task RunSmoke(string screenshot)
    {
        SetTexts("// DiffBeacon · Compare With Clarity\npublic sealed class Beacon\n{\n    public string Name => \"Original\";\n    public int ReviewCount => 2;\n}\n", "// DiffBeacon · Compare With Clarity\npublic sealed class Beacon\n{\n    public string Name => \"Reviewed\";\n    public int ReviewCount => 3;\n    public bool Ready => true;\n}\n");
        await Compare(); if (textResult?.Differences != 3) throw new Exception("Text smoke comparison failed.");
        Merge(true, true); if (rightText.Text != leftText.Text || reportRows.Count != 0) throw new Exception("Merge/invalidation smoke failed.");
        Undo(); await Compare(); if (textResult?.Differences != 3) throw new Exception("Undo smoke failed.");
        leftText.AppendText("// changed"); if (textResult != null || reportRows.Count != 0) throw new Exception("Edit did not invalidate result.");
        Undo(); // No remaining merge history: restore explicit fixture below.
        var original = leftText.Text; var pending = Compare(); leftText.AppendText("// mid-flight"); await pending;
        if (textResult != null || reportRows.Count != 0) throw new Exception("Stale asynchronous results were published.");
        SetTexts(original[..original.LastIndexOf("// changed", StringComparison.Ordinal)], rightText.Text);
        foreach (var item in new[] { "Folders", "Binary", "Tables", "Text" }) mode.SelectedItem = item;
        await Compare(); dirtyLeft = dirtyRight = false; Swap(); if (textResult != null || reportRows.Count != 0) throw new Exception("Swap did not invalidate result."); Swap(); await Compare();
        var fixtureLeft = leftText.Text; var fixtureRight = rightText.Text; NewSnippets(); if (reportRows.Count != 0 || textResult != null) throw new Exception("New snippets did not invalidate result.");
        SetTexts(fixtureLeft, fixtureRight); await Compare(); Navigate(1);
        var form = FindForm()!; form.Width = 1340; form.Height = 900; await Task.Delay(250);
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.GetFullPath(screenshot), System.Drawing.Imaging.ImageFormat.Png);
        dirtyLeft = dirtyRight = false;
    }
    protected override void Dispose(bool disposing) { if (disposing) { disposed = true; cancellation?.Cancel(); } base.Dispose(disposing); }
}
