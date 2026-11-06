namespace DiffBeacon.App;

sealed class DarkPrompt : DarkWindow
{
    readonly Button primary = new() { AutoSize = true }, safe = new() { AutoSize = true };
    readonly RichTextBox message;
    readonly Panel content;
    readonly bool confirmation;
    public static DialogResult Show(Control owner, string body, string title, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    { using var prompt = new DarkPrompt(body, title, buttons == MessageBoxButtons.YesNo); return prompt.ShowDialog(owner.FindForm() ?? (IWin32Window)owner); }
    DarkPrompt(string body, string title, bool confirmation)
    {
        this.confirmation = confirmation; Text = title; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false; StartPosition = FormStartPosition.CenterParent;
        Width = 620; Height = 340; MinimumSize = new(430, 240); Font = new("Segoe UI", 10);
        message = new RichTextBox { Text = body, ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.None, TabStop = true, AccessibleName = title + " Message" };
        content = new Panel { Dock = DockStyle.Fill, Padding = new(16) }; content.Controls.Add(message);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, Padding = new(12), WrapContents = true };
        primary.Text = confirmation ? "Discard Changes" : "Close"; primary.DialogResult = confirmation ? DialogResult.Yes : DialogResult.OK; primary.AccessibleName = primary.Text;
        safe.Text = "Keep Editing"; safe.DialogResult = DialogResult.No; safe.AccessibleName = safe.Text;
        if (confirmation) { buttons.Controls.Add(safe); buttons.Controls.Add(primary); AcceptButton = safe; CancelButton = safe; }
        else { buttons.Controls.Add(primary); AcceptButton = primary; CancelButton = primary; }
        WindowContent.Controls.Add(content); WindowContent.Controls.Add(buttons);
        content.SizeChanged += (_, _) => FitMessage();
        message.FontChanged += (_, _) => FitMessage();
        Shown += (_, _) => { if (confirmation) safe.Focus(); else primary.Focus(); };
        DarkTheme.Apply(WindowContent);
        ApplyPromptSurface(WindowContent);
    }
    void FitMessage()
    {
        var bounds = content.DisplayRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var measured = TextRenderer.MeasureText(message.Text, message.Font, new Size(bounds.Width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
        // Exception diagnostics can exceed even the largest usable dialog.
        // Keep the full text selectable while preserving the action row.
        var scrollBars = measured.Height > bounds.Height ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None;
        if (message.ScrollBars != scrollBars) message.ScrollBars = scrollBars;
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); ApplyPromptSurface(WindowContent); FitMessage(); }
    protected override void OnSystemColorsChanged(EventArgs e) { base.OnSystemColorsChanged(e); ApplyPromptSurface(WindowContent); }
    static void ApplyPromptSurface(Control root)
    {
        if (root is Panel or RichTextBox) { root.BackColor = DarkTheme.Surface; root.ForeColor = DarkTheme.SurfaceText; }
        foreach (Control child in root.Controls) ApplyPromptSurface(child);
    }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (confirmation && DialogResult is not (DialogResult.Yes or DialogResult.No)) DialogResult = DialogResult.No; else if (!confirmation && DialogResult != DialogResult.OK) DialogResult = DialogResult.OK; base.OnFormClosing(e); }
    internal static void Smoke(Control owner, string screenshot)
    {
        using var prompt = new DarkPrompt("Discard Unsaved Editor Changes?\n\nYour comparison contains unsaved edits. Choose Keep Editing to continue reviewing.", "Unsaved Comparison", true);
        prompt.Shown += (_, _) =>
        {
            prompt.BeginInvoke(() =>
            {
                if (prompt.AcceptButton != prompt.safe || prompt.CancelButton != prompt.safe || prompt.ShowInTaskbar) throw new Exception("Unsafe prompt defaults.");
                if (prompt.safe.Text != "Keep Editing" || prompt.primary.Text != "Discard Changes" ||
                    prompt.message.ScrollBars != RichTextBoxScrollBars.None)
                    throw new Exception("The ordinary confirmation lost its clear labels or required scrolling.");
                prompt.CapturePreview(screenshot);
                Control? FindMessage(Control control)
                {
                    if (control is RichTextBox) return control;
                    foreach (Control child in control.Controls) if (FindMessage(child) is { } found) return found;
                    return null;
                }
                var message = FindMessage(prompt) ?? throw new Exception("Prompt message control was not found.");
                message.Focus();
                if (!message.Focused || prompt.safe.Focused || !DarkTheme.IsDefaultAction(prompt.safe) || DarkTheme.IsDefaultAction(prompt.primary)) throw new Exception("Prompt lost its safe default when the message received focus.");
                using (var buttonImage = new Bitmap(prompt.safe.Width, prompt.safe.Height))
                {
                    prompt.safe.DrawToBitmap(buttonImage, prompt.safe.ClientRectangle);
                    if (buttonImage.GetPixel(1, 1).ToArgb() != DarkTheme.Text.ToArgb()) throw new Exception("Prompt safe default indicator was not painted without focus.");
                }
                prompt.CapturePreview(Path.ChangeExtension(screenshot, ".default-button.png"));
                prompt.ProcessDialogKey(Keys.Escape);
            });
        };
        if (prompt.ShowDialog(owner.FindForm()) != DialogResult.No) throw new Exception("Prompt Escape did not preserve work.");
        using var closePrompt = new DarkPrompt("Keep changes?", "Close Prompt Check", true);
        closePrompt.Shown += (_, _) => closePrompt.BeginInvoke(() => closePrompt.Close());
        if (closePrompt.ShowDialog(owner.FindForm()) != DialogResult.No) throw new Exception("Prompt X did not default to No.");
        using var enterPrompt = new DarkPrompt("Keep changes?", "Enter Prompt Check", true);
        enterPrompt.Shown += (_, _) => enterPrompt.BeginInvoke(() =>
        {
            foreach (Control panel in enterPrompt.WindowContent.Controls)
                foreach (Control child in panel.Controls) if (child is RichTextBox) child.Focus();
            enterPrompt.ProcessDialogKey(Keys.Enter);
        });
        if (enterPrompt.ShowDialog(owner.FindForm()) != DialogResult.No) throw new Exception("Prompt Enter did not default to No.");
        using var yesPrompt = new DarkPrompt("Discard changes?", "Yes Prompt Check", true);
        yesPrompt.Shown += (_, _) => yesPrompt.BeginInvoke(() => yesPrompt.primary.PerformClick());
        if (yesPrompt.ShowDialog(owner.FindForm()) != DialogResult.Yes) throw new Exception("Explicit prompt Yes was lost.");
        var longMessage = string.Join('\n', Enumerable.Repeat("Could not open C:\\Long path with spaces\\Comparison input.txt. Check access and try again.", 45));
        using var error = new DarkPrompt(longMessage, "Could Not Compare", false);
        error.Shown += (_, _) => error.BeginInvoke(() =>
        {
            if (error.message.Text != longMessage || error.message.ScrollBars != RichTextBoxScrollBars.Vertical ||
                !error.primary.Visible || !error.primary.Enabled)
                throw new Exception("Long error details or their Close action were lost.");
            error.ProcessDialogKey(Keys.Escape);
        });
        if (error.ShowDialog(owner.FindForm()) != DialogResult.OK) throw new Exception("Long error could not close.");
    }
}
