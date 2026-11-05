using System.Text;
using DiffBeacon.Core;

namespace DiffBeacon.App;

sealed class ThreeWayMergeView : UserControl
{
    readonly TextBox[] paths = [new(), new(), new()];
    readonly RichTextBox[] sources = [Editor(), Editor(), Editor()];
    readonly RichTextBox preview = Editor();
    readonly ListBox conflicts = new DarkListBox() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true, AccessibleName = "Merge Conflicts" };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 48, Padding = new(12, 8, 0, 0) };
    readonly Label outputFormat = new() { Dock = DockStyle.Top, Height = 30, Padding = new(8, 6, 0, 0), Text = "Merged Preview · Read-Only · Resolve Conflicts To Enable Save" };
    readonly Button compare = new() { Text = "Compare", AutoSize = true }, save = new() { Text = "Save Merged As…", AutoSize = true, Enabled = false }, undoButton = new() { Text = "Undo Choice", AutoSize = true, Enabled = false };
    readonly Dictionary<string, MergeChoice> choices = new(StringComparer.Ordinal);
    readonly Stack<(string Id, MergeChoice? Previous)> undo = new();
    readonly List<Button> choiceButtons = new();
    TextDocument[]? documents;
    ThreeWayResult? plan;
    MergeSegment[] conflictSegments = [];
    CancellationTokenSource? cancellation;
    bool disposed, unsavedReview, pathsChanged;

    static RichTextBox Editor() => new() { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false, DetectUrls = false, Font = new("Consolas", 10), BorderStyle = BorderStyle.None, BackColor = DarkTheme.AmberText, ForeColor = DarkTheme.Text, AccessibleDescription = "Read-only comparison content" };
    static void Style(Button button) => DarkTheme.Button(button);
    public ThreeWayMergeView()
    {
        Dock = DockStyle.Fill; BackColor = DarkTheme.Window; ForeColor = DarkTheme.Text;
        var pathPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, ColumnCount = 3, RowCount = 3, Padding = new(10) };
        pathPanel.ColumnStyles.Add(new(SizeType.Absolute, 65)); pathPanel.ColumnStyles.Add(new(SizeType.Percent, 100)); pathPanel.ColumnStyles.Add(new(SizeType.Absolute, 92));
        string[] names = ["Base", "Local", "Remote"];
        for (int i = 0; i < 3; i++)
        {
            int side = i; paths[i].Dock = DockStyle.Fill; paths[i].BackColor = DarkTheme.Surface; paths[i].ForeColor = ForeColor; paths[i].AccessibleName = names[i] + " File Path";
            pathPanel.Controls.Add(new Label { Text = names[i], AutoSize = true, Padding = new(0, 6, 0, 0) }, 0, i); pathPanel.Controls.Add(paths[i], 1, i);
            var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill }; Style(browse); browse.Click += (_, _) => Browse(side); pathPanel.Controls.Add(browse, 2, i);
            paths[i].TextChanged += (_, _) => PathsChanged();
            sources[i].AccessibleName = names[i] + " Source";
        }
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, AutoScroll = false, Padding = new(10, 4, 0, 0) };
        toolbar.Controls.Add(compare); Style(compare); compare.Click += async (_, _) => await CompareAsync(true);
        var cancel = new Button { Text = "Cancel", AutoSize = true }; Style(cancel); toolbar.Controls.Add(cancel); cancel.Click += (_, _) => cancellation?.Cancel();
        Style(save); toolbar.Controls.Add(save); save.Click += (_, _) => { using var d = new SaveFileDialog { Filter = "All Files|*.*", FileName = documents == null ? "Merged.txt" : Path.GetFileName(documents[1].Path), OverwritePrompt = true }; if (d.ShowDialog(this) == DialogResult.OK) TryAction(() => SaveTo(d.FileName, File.Exists(d.FileName))); };
        var sourceTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) { sourceTable.ColumnStyles.Add(new(SizeType.Percent, 100f / 3)); var panel = new Panel { Dock = DockStyle.Fill, Padding = new(4) }; panel.Controls.Add(sources[i]); panel.Controls.Add(new Label { Text = names[i] + " · Read-Only", Dock = DockStyle.Top, Height = 26, Padding = new(4, 4, 0, 0), BackColor = DarkTheme.Raised }); sourceTable.Controls.Add(panel, i, 0); }
        conflicts.BackColor = DarkTheme.Surface; conflicts.ForeColor = ForeColor; conflicts.SelectedIndexChanged += (_, _) => SelectConflict();
        var review = new SplitContainer { Size = new(1100, 350), Dock = DockStyle.Fill, SplitterWidth = 6, FixedPanel = FixedPanel.Panel1, SplitterDistance = 300, Panel1MinSize = 200, Panel2MinSize = 200 };
        var reviewTools = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new(4), AutoScroll = false };
        void Button(string text, Action action, bool choice = false) { var b = new Button { Text = text, AutoSize = true }; Style(b); b.Click += (_, _) => TryAction(action); reviewTools.Controls.Add(b); if (choice) choiceButtons.Add(b); }
        Button("Previous Conflict", () => MoveConflict(-1)); Button("Next Conflict", () => MoveConflict(1)); Button("Accept Local", () => Accept(MergeChoice.Local), true); Button("Accept Remote", () => Accept(MergeChoice.Remote), true); Button("Accept Both", () => Accept(MergeChoice.Both), true);
        Style(undoButton); undoButton.Click += (_, _) => TryAction(UndoChoice); reviewTools.Controls.Add(undoButton);
        review.Panel1.Controls.Add(conflicts); review.Panel1.Controls.Add(new Label { Text = "Conflicts · Explicit Choices Required", Dock = DockStyle.Top, Height = 29, Padding = new(6) });
        preview.AccessibleName = "Merged Preview"; review.Panel2.Controls.Add(preview); review.Panel2.Controls.Add(outputFormat);
        var content = new SplitContainer { Size = new(1100, 600), Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, FixedPanel = FixedPanel.Panel1, SplitterWidth = 6, SplitterDistance = 220 }; content.Panel1.Controls.Add(sourceTable); content.Panel2.Controls.Add(review); content.Panel2.Controls.Add(reviewTools);
        content.Panel2.Controls.SetChildIndex(review, 0); content.Panel2.Controls.SetChildIndex(reviewTools, 1);
        Controls.Add(content); Controls.Add(status); Controls.Add(pathPanel); Controls.Add(toolbar);
        bool sizingWorkspace = false, workspaceInitialized = false;
        void SizeWorkspace()
        {
            if (sizingWorkspace || !IsHandleCreated) return;
            sizingWorkspace = true;
            try
            {
                int toolsHeight = reviewTools.GetPreferredSize(new(content.Panel2.ClientSize.Width, 0)).Height;
                int sourceMinimum = 26 + 8 + sources[0].Font.Height + SystemInformation.HorizontalScrollBarHeight;
                int reviewMinimum = toolsHeight + Math.Max(29 + 2 * conflicts.ItemHeight + 4, outputFormat.Height + 2 * preview.Font.Height) + SystemInformation.HorizontalScrollBarHeight;
                int maximum = content.ClientSize.Height - content.SplitterWidth - reviewMinimum;
                if (maximum < sourceMinimum) return;
                content.Panel1MinSize = sourceMinimum; content.Panel2MinSize = reviewMinimum;
                int distance = Math.Clamp(workspaceInitialized ? content.SplitterDistance : content.ClientSize.Height * 2 / 5, sourceMinimum, maximum);
                workspaceInitialized = true;
                if (content.SplitterDistance != distance) content.SplitterDistance = distance;
            }
            finally { sizingWorkspace = false; }
        }
        HandleCreated += (_, _) => SizeWorkspace(); content.SizeChanged += (_, _) => SizeWorkspace(); reviewTools.SizeChanged += (_, _) => SizeWorkspace();
        InvalidatePlan();
        DarkTheme.Apply(this);
    }
    void Browse(int side) { using var dialog = new OpenFileDialog(); if (dialog.ShowDialog(this) == DialogResult.OK) paths[side].Text = dialog.FileName; }
    void PathsChanged()
    {
        cancellation?.Cancel(); pathsChanged = true; save.Enabled = undoButton.Enabled = false; foreach (var b in choiceButtons) b.Enabled = false;
        // Keep the previous snapshot and choices until Compare explicitly confirms replacement.
        // Typing a path never prompts per keystroke and cannot silently discard an unsaved review.
        outputFormat.Text = plan == null ? "Merged Preview · Compare The Selected Sources" : "Previous Review Retained · Source Paths Changed · Compare To Replace";
        status.Text = plan == null ? "Choose All Three Sources, Then Compare" : "Paths Changed · Previous Choices Retained · Save Disabled Until A New Comparison";
    }
    void TryAction(Action action) { try { action(); } catch (Exception ex) { status.Text = "Error · " + ex.Message; DarkPrompt.Show(this, ex.Message, "Three-Way Merge", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    void InvalidatePlan()
    {
        cancellation?.Cancel(); plan = null; documents = null; conflictSegments = []; choices.Clear(); undo.Clear(); conflicts.Items.Clear(); preview.Clear(); foreach (var source in sources) source.Clear();
        save.Enabled = undoButton.Enabled = false; foreach (var b in choiceButtons) b.Enabled = false;
        outputFormat.Text = "Merged Preview · Read-Only · Encoding And Newlines Will Follow Local";
        status.Text = "Choose Base, Local And Remote Files, Then Compare · Source Files Are Not Edited";
        unsavedReview = false; pathsChanged = false;
    }
    async Task CompareAsync(bool showErrors)
    {
        if (unsavedReview && showErrors && DarkPrompt.Show(this, "Discard The Current Unsaved Merge Review?", "Three-Way Merge", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        InvalidatePlan(); var cts = new CancellationTokenSource(); cancellation = cts; var token = cts.Token; string[] filePaths = paths.Select(p => p.Text).ToArray(); compare.Enabled = false; status.Text = "Comparing Three Inputs…";
        try
        {
            var result = await Task.Run(() => { var loaded = new TextDocument[3]; for (int i = 0; i < 3; i++) { token.ThrowIfCancellationRequested(); loaded[i] = TextDocument.Load(filePaths[i]); } var created = ThreeWayMerge.Create(loaded[0].Text, loaded[1].Text, loaded[2].Text, token); foreach (var doc in loaded) { token.ThrowIfCancellationRequested(); if (!doc.Stamp.Matches(doc.Path)) throw new IOException("A source changed during comparison. Retry."); } return (Documents: loaded, Plan: created); }, token);
            if (disposed || cts != cancellation || token.IsCancellationRequested) return;
            documents = result.Documents; plan = result.Plan; conflictSegments = plan.Segments.Where(s => s.Kind == MergeSegmentKind.Conflict).ToArray();
            for (int i = 0; i < 3; i++) sources[i].Text = documents[i].Text;
            unsavedReview = true; RefreshReview();
        }
        catch (OperationCanceledException) { if (!disposed && cts == cancellation) status.Text = "Cancelled · No Merge Plan Is Available"; }
        catch (Exception ex) { if (!disposed && cts == cancellation) { status.Text = "Error · " + ex.Message; if (showErrors) DarkPrompt.Show(this, ex.Message, "Three-Way Merge", MessageBoxButtons.OK, MessageBoxIcon.Warning); else throw; } }
        finally { if (!disposed && cts == cancellation) { cancellation = null; compare.Enabled = true; } cts.Dispose(); }
    }
    void RefreshReview()
    {
        if (plan == null || documents == null) return;
        int selected = conflicts.SelectedIndex; conflicts.BeginUpdate();
        try { conflicts.Items.Clear(); for (int i = 0; i < conflictSegments.Length; i++) { var s = conflictSegments[i]; conflicts.Items.Add($"{i + 1}. Base Line {s.BaseStart + 1} · {(choices.TryGetValue(s.Id, out var c) ? c.ToString() : "Unresolved")}"); } if (conflicts.Items.Count > 0) conflicts.SelectedIndex = Math.Clamp(selected, 0, conflicts.Items.Count - 1); }
        finally { conflicts.EndUpdate(); }
        int unresolved = conflictSegments.Count(s => !choices.ContainsKey(s.Id));
        save.Enabled = !pathsChanged && unresolved == 0; undoButton.Enabled = !pathsChanged && undo.Count > 0; foreach (var b in choiceButtons) b.Enabled = !pathsChanged && conflictSegments.Length > 0;
        var lines = new List<string>(); int number = 0;
        foreach (var segment in plan.Segments)
        {
            if (segment.Kind != MergeSegmentKind.Conflict) { lines.AddRange(segment.MergedLines); continue; }
            number++;
            if (choices.TryGetValue(segment.Id, out var choice)) { if (choice is MergeChoice.Local or MergeChoice.Both) lines.AddRange(segment.LocalLines); if (choice is MergeChoice.Remote or MergeChoice.Both) lines.AddRange(segment.RemoteLines); }
            else { lines.Add($"UNRESOLVED CONFLICT {number}"); lines.Add("<<<<<<< LOCAL"); lines.AddRange(segment.LocalLines); lines.Add("======="); lines.AddRange(segment.RemoteLines); lines.Add(">>>>>>> REMOTE"); }
        }
        preview.Text = string.Join('\n', lines);
        outputFormat.Text = $"Output Uses Local: {documents[1].Encoding.WebName} · BOM {(documents[1].Preamble.Length > 0 ? "Preserved" : "None")} · Local Newlines Preserved";
        status.Text = $"{unresolved} Unresolved Of {conflictSegments.Length} Conflicts · {(unresolved == 0 ? "Ready To Save After Review" : "Save Disabled Until All Conflicts Have Explicit Choices")}";
        SelectConflict();
    }
    void MoveConflict(int delta) { if (conflictSegments.Length > 0) conflicts.SelectedIndex = Math.Clamp(conflicts.SelectedIndex + delta, 0, conflictSegments.Length - 1); }
    void SelectConflict()
    {
        int index = conflicts.SelectedIndex; if (index < 0 || index >= conflictSegments.Length) return;
        var segment = conflictSegments[index];
        int at = sources[0].GetFirstCharIndexFromLine(segment.BaseStart); if (at >= 0) { sources[0].Select(at, 0); sources[0].ScrollToCaret(); }
        string marker = "UNRESOLVED CONFLICT " + (index + 1); int previewAt = preview.Text.IndexOf(marker, StringComparison.Ordinal); if (previewAt >= 0) { preview.Select(previewAt, marker.Length); preview.ScrollToCaret(); }
    }
    void Accept(MergeChoice choice)
    {
        int index = conflicts.SelectedIndex; if (pathsChanged || plan == null || index < 0 || index >= conflictSegments.Length) return;
        string id = conflictSegments[index].Id;
        if (choices.TryGetValue(id, out var previous) && previous == choice) return;
        undo.Push((id, choices.ContainsKey(id) ? previous : null));
        if (undo.Count > 100) { var recent = undo.Take(100).Reverse().ToArray(); undo.Clear(); foreach (var item in recent) undo.Push(item); }
        choices[id] = choice; unsavedReview = true; RefreshReview();
    }
    void UndoChoice() { if (pathsChanged || !undo.TryPop(out var previous)) return; if (previous.Previous.HasValue) choices[previous.Id] = previous.Previous.Value; else choices.Remove(previous.Id); unsavedReview = true; RefreshReview(); }
    void SaveTo(string destination, bool overwrite)
    {
        if (plan == null || documents == null) throw new InvalidOperationException("Compare sources first.");
        if (pathsChanged) throw new InvalidOperationException("Source paths changed. Compare again before saving; the previous review has been retained.");
        string text = ThreeWayMerge.Render(plan, choices); // Refuses unresolved/stale choice IDs regardless of button state.
        foreach (var doc in documents) if (!doc.Stamp.Matches(doc.Path)) { PathsChanged(); throw new IOException("A source changed externally. The previous review is retained; compare again before saving."); }
        destination = Path.GetFullPath(destination);
        var input = documents.FirstOrDefault(d => string.Equals(d.Path, destination, StringComparison.OrdinalIgnoreCase));
        var expected = input?.Stamp ?? (File.Exists(destination) ? FileStamp.Capture(destination) : null);
        SafeFiles.Write(destination, documents[1].Encode(text), expected, overwrite);
        unsavedReview = false; status.Text = "Merged Output Saved · Review Sources Again Before Another Merge";
        // If the output replaced a source, the original plan is stale. Keep displayed review but block reuse.
        if (input != null) { plan = null; save.Enabled = false; foreach (var b in choiceButtons) b.Enabled = false; undoButton.Enabled = false; }
    }
    public bool CanClose() => !unsavedReview || DarkPrompt.Show(this, "Close This Unsaved Merge Review?", "Three-Way Merge", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    public async Task RunSmoke(string screenshot)
    {
        string root = Path.Combine(Path.GetTempPath(), "DiffBeacon-ThreeWay-Smoke-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string[] text = ["name=base\r\nvalue=1\r\n", "name=local\r\nvalue=1\r\n", "name=remote\r\nvalue=2\r\n"];
            for (int i = 0; i < 3; i++) { paths[i].Text = Path.Combine(root, i + ".txt"); File.WriteAllText(paths[i].Text, text[i], new UTF8Encoding(true)); }
            await CompareAsync(false); if (plan?.ConflictCount != 1 || save.Enabled || !preview.Text.Contains("UNRESOLVED CONFLICT 1")) throw new Exception("Three-way unresolved UI state failed.");
            bool blocked = false; try { SaveTo(Path.Combine(root, "blocked.txt"), false); } catch (InvalidOperationException) { blocked = true; } if (!blocked || File.Exists(Path.Combine(root, "blocked.txt"))) throw new Exception("Unresolved save was allowed.");
            Accept(MergeChoice.Both); if (!save.Enabled || !preview.Text.Contains("name=local") || !preview.Text.Contains("name=remote")) throw new Exception("Accept Both failed.");
            UndoChoice(); if (save.Enabled) throw new Exception("Undo did not restore unresolved state.");
            Accept(MergeChoice.Remote);
            paths[0].Text += ".edited"; if (plan == null || choices.Count != 1 || !unsavedReview || save.Enabled || undo.Count == 0) throw new Exception("Path editing discarded an unsaved review.");
            bool changedPathBlocked = false; try { SaveTo(Path.Combine(root, "wrong-source.txt"), false); } catch (InvalidOperationException) { changedPathBlocked = true; } if (!changedPathBlocked) throw new Exception("Changed source paths did not block save.");
            paths[0].Text = Path.Combine(root, "0.txt"); await CompareAsync(false); Accept(MergeChoice.Remote);
            for (int i = 0; i < 120; i++) Accept(i % 2 == 0 ? MergeChoice.Local : MergeChoice.Remote); if (undo.Count != 100) throw new Exception("Choice undo exceeded its cap.");
            Accept(MergeChoice.Remote); string output = Path.Combine(root, "merged.txt"); SaveTo(output, false); var merged = TextDocument.Load(output); if (merged.Text != text[2] || merged.Preamble.Length != 3) throw new Exception("Three-way output encoding/content mismatch.");
            File.WriteAllText(paths[0].Text, "external change"); bool staleBlocked = false; try { SaveTo(output, true); } catch (IOException) { staleBlocked = true; } if (!staleBlocked || save.Enabled) throw new Exception("Stale source save was allowed.");
            File.WriteAllText(paths[0].Text, text[0], new UTF8Encoding(true)); await CompareAsync(false); var pending = CompareAsync(false); paths[0].Text += ".missing"; await pending; if (plan != null || choices.Count != 0 || save.Enabled) throw new Exception("Stale async plan survived a path change.");
            paths[0].Text = Path.Combine(root, "0.txt"); await CompareAsync(false);
            int? previousSourceDistance = null, previousReviewDistance = null;
            await ((DarkWindow)FindForm()!).SmokeResponsive(this, screenshot, () =>
            {
                var review = conflicts.Parent!.Parent as SplitContainer ?? throw new Exception("Conflict pane missing.");
                var lower = review.Parent!;
                var content = (SplitContainer)lower.Parent!;
                var tools = lower.Controls.OfType<FlowLayoutPanel>().Single();
                if (previousSourceDistance.HasValue && content.SplitterDistance != Math.Clamp(previousSourceDistance.Value, content.Panel1MinSize, content.Height - content.SplitterWidth - content.Panel2MinSize)) throw new Exception("Resize reset a feasible source splitter position.");
                if (previousReviewDistance.HasValue && review.SplitterDistance != Math.Clamp(previousReviewDistance.Value, review.Panel1MinSize, review.Width - review.SplitterWidth - review.Panel2MinSize)) throw new Exception("Resize reset a feasible review splitter position.");
                int distance = review.SplitterDistance;
                int sourceDistance = Math.Clamp(120, content.Panel1MinSize, content.Height - content.SplitterWidth - content.Panel2MinSize);
                content.SplitterDistance = sourceDistance;
                review.SplitterDistance = review.Panel1MinSize; PerformLayout(); DarkWindow.CheckCommandBounds(this);
                if (tools.Parent != lower || tools.Left != 0 || tools.Width != lower.ClientSize.Width || tools.Bottom != lower.ClientSize.Height || review.Bottom > tools.Top) throw new Exception("Review commands do not span the lower workspace beneath both panes.");
                if (conflicts.ClientSize.Height < 2 * conflicts.ItemHeight + SystemInformation.HorizontalScrollBarHeight || preview.ClientSize.Height < 2 * preview.Font.Height + SystemInformation.HorizontalScrollBarHeight) throw new Exception($"Review panes need two text rows: list {conflicts.Bounds}, preview {preview.Bounds}, tools {tools.Bounds}, workspace {lower.Size}.");
                content.PerformLayout(); tools.PerformLayout(); PerformLayout();
                if (content.SplitterDistance != sourceDistance || review.SplitterDistance != review.Panel1MinSize) throw new Exception("Layout reset a feasible user splitter position.");
                if (sources.Any(source => source.ClientSize.Height < source.Font.Height + SystemInformation.HorizontalScrollBarHeight)) throw new Exception("Source pane needs its header and one text row.");
                review.SplitterDistance = distance;
                if (content.SplitterDistance != sourceDistance) throw new Exception("Conflict width reset the source splitter position.");
                previousSourceDistance = sourceDistance; previousReviewDistance = distance; return Task.CompletedTask;
            });
            var form = FindForm()!; form.Width = 1440; form.Height = 980; await Task.Delay(150); ((DarkWindow)form).CapturePreview(Path.GetFullPath(screenshot));
        }
        finally { unsavedReview = false; Directory.Delete(root, true); }
    }
    protected override void Dispose(bool disposing) { if (disposing) { disposed = true; cancellation?.Cancel(); } base.Dispose(disposing); }
}
