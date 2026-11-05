namespace DiffBeacon.App;

sealed class DarkPrompt : DarkWindow
{
    readonly Button primary = new() { AutoSize = true }, safe = new() { AutoSize = true };
    readonly bool confirmation;
    public static DialogResult Show(Control owner, string body, string title, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    { using var prompt = new DarkPrompt(body, title, buttons == MessageBoxButtons.YesNo); return prompt.ShowDialog(owner.FindForm() ?? (IWin32Window)owner); }
    DarkPrompt(string body, string title, bool confirmation)
    {
        this.confirmation = confirmation; Text = title; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false; StartPosition = FormStartPosition.CenterParent;
        Width = 620; Height = 340; MinimumSize = new(430, 240); Font = new("Segoe UI", 10);
        var message = new RichTextBox { Text = body, ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.Vertical, TabStop = true, AccessibleName = title + " Message", Padding = new(15) };
        var content = new Panel { Dock = DockStyle.Fill, Padding = new(24) }; content.Controls.Add(message);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 66, FlowDirection = FlowDirection.RightToLeft, Padding = new(12), WrapContents = false };
        primary.Text = confirmation ? "Yes" : "OK"; primary.DialogResult = confirmation ? DialogResult.Yes : DialogResult.OK; primary.AccessibleName = primary.Text;
        safe.Text = "No"; safe.DialogResult = DialogResult.No; safe.AccessibleName = "No — Keep Current Work";
        if (confirmation) { buttons.Controls.Add(safe); buttons.Controls.Add(primary); AcceptButton = safe; CancelButton = safe; }
        else { buttons.Controls.Add(primary); AcceptButton = primary; CancelButton = primary; }
        WindowContent.Controls.Add(content); WindowContent.Controls.Add(buttons);
        Shown += (_, _) => { if (confirmation) safe.Focus(); else primary.Focus(); };
        DarkTheme.Apply(WindowContent);
        ApplyPromptSurface(WindowContent);
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); ApplyPromptSurface(WindowContent); }
    protected override void OnSystemColorsChanged(EventArgs e) { base.OnSystemColorsChanged(e); ApplyPromptSurface(WindowContent); }
    static void ApplyPromptSurface(Control root)
    {
        if (root is Panel or RichTextBox) { root.BackColor = DarkTheme.Surface; root.ForeColor = DarkTheme.SurfaceText; }
        foreach (Control child in root.Controls) ApplyPromptSurface(child);
    }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (confirmation && DialogResult is not (DialogResult.Yes or DialogResult.No)) DialogResult = DialogResult.No; else if (!confirmation && DialogResult != DialogResult.OK) DialogResult = DialogResult.OK; base.OnFormClosing(e); }
    internal static void Smoke(Control owner, string screenshot)
    {
        using var prompt = new DarkPrompt("Discard Unsaved Editor Changes?\n\nYour comparison contains unsaved edits. Choose No to keep reviewing.\n\nThe message is selectable and scrollable; Enter and Escape both keep your work by default.", "Unsaved Comparison", true);
        prompt.Shown += (_, _) =>
        {
            prompt.BeginInvoke(() =>
            {
                if (prompt.AcceptButton != prompt.safe || prompt.CancelButton != prompt.safe || prompt.ShowInTaskbar) throw new Exception("Unsafe prompt defaults.");
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
    }
}
