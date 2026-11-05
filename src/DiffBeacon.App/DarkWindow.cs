using System.Runtime.InteropServices;

namespace DiffBeacon.App;

/// <summary>Custom caption over the documented sizable-window/system-command contract.</summary>
public class DarkWindow : Form
{
    const int WmNcCalcSize = 0x83, WmNcHitTest = 0x84, WmGetMinMaxInfo = 0x24, WmNcLButtonDown = 0xa1, WmNcLButtonUp = 0xa2;
    const int HtClient = 1, HtCaption = 2, HtMaxButton = 9, HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13, HtTopRight = 14, HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17;
    readonly Panel captionBar = new() { Dock = DockStyle.Top, AccessibleName = "Window Title Bar", AccessibleRole = AccessibleRole.TitleBar };
    readonly Label captionText = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly PictureBox captionIcon = new() { Dock = DockStyle.Left, SizeMode = PictureBoxSizeMode.CenterImage };
    readonly Button minimize = new() { Dock = DockStyle.Right, Text = "−", AccessibleName = "Minimize" }, maximize = new() { Dock = DockStyle.Right, Text = "□", AccessibleName = "Maximize" }, close = new() { Dock = DockStyle.Right, Text = "×", AccessibleName = "Close" };
    readonly Dictionary<Control, ChildHitRouter> hitRouters = new();
    bool maxPressed;
    bool captionPressed;
    Point captionPress;
    Rectangle pressedCaption;
    Size normalSize;
    Bitmap? iconBitmap;
    protected Panel WindowContent { get; } = new() { Dock = DockStyle.Fill };
    public DarkWindow()
    {
        AutoScaleMode = AutoScaleMode.Dpi; FormBorderStyle = FormBorderStyle.Sizable; SetStyle(ControlStyles.ResizeRedraw, true);
        Controls.Add(WindowContent); Controls.Add(captionBar);
        captionBar.Controls.Add(captionText); captionBar.Controls.Add(captionIcon); captionBar.Controls.Add(minimize); captionBar.Controls.Add(maximize); captionBar.Controls.Add(close);
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximize.Click += (_, _) => ToggleMaximize();
        close.Click += (_, _) => Close();
        TextChanged += (_, _) => captionText.Text = Text;
        captionIcon.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) ShowSystemMenu(captionIcon.PointToScreen(new(0, captionIcon.Height))); };
        captionIcon.DoubleClick += (_, _) => Close();
        ScaleCaption(); ApplyChrome(); WatchChildren(this);
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); minimize.Visible = MinimizeBox; maximize.Visible = MaximizeBox; RefreshCaptionIcon(); ApplyChrome(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); int dark = DarkTheme.HighContrast ? 0 : 1; try { _ = DwmSetWindowAttribute(Handle, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)); } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { /* App-drawn chrome remains available without DWM. */ } }
    protected override void OnDpiChanged(DpiChangedEventArgs e) { base.OnDpiChanged(e); ScaleCaption(); RefreshCaptionIcon(); }
    protected override void OnResize(EventArgs e) { if (captionPressed && WindowState != FormWindowState.Maximized) CancelCaptionPress(); base.OnResize(e); if (maximize != null) { maximize.Text = WindowState == FormWindowState.Maximized ? "❐" : "□"; maximize.AccessibleName = WindowState == FormWindowState.Maximized ? "Restore" : "Maximize"; } }
    protected override void OnSystemColorsChanged(EventArgs e) { base.OnSystemColorsChanged(e); if (captionBar != null) ApplyChrome(); }
    void ScaleCaption() { int height = LogicalToDeviceUnits(46); captionBar.Height = height; captionIcon.Width = LogicalToDeviceUnits(42); foreach (var button in new[] { minimize, maximize, close }) { button.Width = LogicalToDeviceUnits(46); button.TabStop = true; button.Font = Font; } }
    void RefreshCaptionIcon() { iconBitmap?.Dispose(); iconBitmap = Icon?.ToBitmap(); captionIcon.Image = iconBitmap; }
    protected void ApplyChrome()
    {
        DarkTheme.Apply(this); captionBar.BackColor = captionText.BackColor = captionIcon.BackColor = DarkTheme.Caption;
        foreach (var button in new[] { minimize, maximize, close }) { button.BackColor = DarkTheme.Caption; button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = DarkTheme.ChromeSelection; }
        close.FlatAppearance.MouseOverBackColor = DarkTheme.AccentHover;
    }
    internal void CapturePreview(string filename, Control? overlay = null)
    {
        // Form.DrawToBitmap requests a stock nonclient caption even with custom NCCALCSIZE.
        // Render the real client controls instead, at their actual laid-out window bounds.
        using var bitmap = new Bitmap(Width, Height);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(DarkTheme.Window);
        WindowContent.DrawToBitmap(bitmap, WindowContent.Bounds); captionBar.DrawToBitmap(bitmap, captionBar.Bounds);
        if (overlay is { Visible: true })
        {
            var origin = overlay.PointToScreen(Point.Empty); var clientOrigin = PointToScreen(Point.Empty);
            overlay.DrawToBitmap(bitmap, new Rectangle(origin.X - clientOrigin.X, origin.Y - clientOrigin.Y, overlay.Width, overlay.Height));
        }
        bitmap.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
    }
    void ToggleMaximize() { if (MaximizeBox) WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
    internal static Point DecodeScreenPoint(nint parameter) => new(unchecked((short)((long)parameter & 0xffff)), unchecked((short)(((long)parameter >> 16) & 0xffff)));
    internal static int FrameHitTest(Point screen, Rectangle bounds, int captionHeight, int margin, bool maximized, Rectangle minimizeBounds, Rectangle maximizeBounds, Rectangle closeBounds)
    {
        if (!bounds.Contains(screen)) return 0;
        if (!maximized)
        {
            bool l = screen.X < bounds.Left + margin, r = screen.X >= bounds.Right - margin, t = screen.Y < bounds.Top + margin, b = screen.Y >= bounds.Bottom - margin;
            if (t && l) return HtTopLeft; if (t && r) return HtTopRight; if (b && l) return HtBottomLeft; if (b && r) return HtBottomRight;
            if (l) return HtLeft; if (r) return HtRight; if (t) return HtTop; if (b) return HtBottom;
        }
        if (minimizeBounds.Contains(screen) || closeBounds.Contains(screen)) return HtClient;
        if (maximizeBounds.Contains(screen)) return HtMaxButton;
        return screen.Y < bounds.Top + captionHeight ? HtCaption : HtClient;
    }
    internal int HitTestScreen(Point screen)
    {
        int hit = FrameHitTest(screen, Bounds, captionBar.RectangleToScreen(captionBar.ClientRectangle).Bottom - Bounds.Top, LogicalToDeviceUnits(7), WindowState == FormWindowState.Maximized,
            minimize.RectangleToScreen(minimize.ClientRectangle), maximize.RectangleToScreen(maximize.ClientRectangle), close.RectangleToScreen(close.ClientRectangle));
        if (hit == HtCaption && captionIcon.RectangleToScreen(captionIcon.ClientRectangle).Contains(screen)) return HtClient;
        if (hit == HtMaxButton && !MaximizeBox) return HtClient;
        return hit;
    }
    void WatchChildren(Control parent)
    {
        parent.ControlAdded += ChildAdded; parent.ControlRemoved += ChildRemoved;
        foreach (Control child in parent.Controls) { hitRouters.Add(child, new ChildHitRouter(this, child)); WatchChildren(child); }
    }
    void UnwatchChildren(Control parent)
    {
        parent.ControlAdded -= ChildAdded; parent.ControlRemoved -= ChildRemoved;
        foreach (Control child in parent.Controls) { UnwatchChildren(child); if (hitRouters.Remove(child, out var router)) router.Dispose(); }
    }
    void ChildAdded(object? sender, ControlEventArgs e) { if (e.Control is { } child) { hitRouters.Add(child, new ChildHitRouter(this, child)); WatchChildren(child); } }
    void ChildRemoved(object? sender, ControlEventArgs e) { if (e.Control is { } child) { UnwatchChildren(child); if (hitRouters.Remove(child, out var router)) router.Dispose(); } }
    void CancelMaximizePress() { maxPressed = false; if (Capture) Capture = false; }
    void CancelCaptionPress() { captionPressed = false; if (Capture) Capture = false; }
    internal static bool OutsideDragThreshold(Point origin, Point current, Size threshold) =>
        !new Rectangle(origin.X - threshold.Width / 2, origin.Y - threshold.Height / 2, threshold.Width, threshold.Height).Contains(current);
    internal static Rectangle RestoredCaptionBounds(Point origin, Point current, Rectangle caption, Size size, int captionHeight)
    {
        double fraction = Math.Clamp((double)(origin.X - caption.Left) / caption.Width, 0, 1);
        return new(current.X - (int)Math.Round(fraction * size.Width), current.Y - Math.Clamp(origin.Y - caption.Top, 0, captionHeight - 1), size.Width, size.Height);
    }
    bool RestoreCaptionDrag(Point screen)
    {
        if (!captionPressed || !OutsideDragThreshold(captionPress, screen, SystemInformation.DragSize)) return false;
        var target = RestoredCaptionBounds(captionPress, screen, pressedCaption,
            new(Math.Max(normalSize.Width, MinimumSize.Width), Math.Max(normalSize.Height, MinimumSize.Height)), captionBar.Height);
        CancelCaptionPress(); WindowState = FormWindowState.Normal; Bounds = target;
        return true;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0xa0) maximize.BackColor = HitTestScreen(DecodeScreenPoint(m.LParam)) == HtMaxButton ? DarkTheme.ChromeSelection : DarkTheme.Caption;
        if (m.Msg == 0x2a2) maximize.BackColor = DarkTheme.Caption;
        if (m.Msg is WmNcHitTest or 0xa0 or 0x2a2)
        {
            // Documented DWM first-refusal sequence supplies native hover/leave tracking.
            // Our real managed caption controls own their hit regions; only mouse tracking
            // results are returned directly. Hit-test geometry below includes resize edges.
            try { if (DwmDefWindowProc(Handle, m.Msg, m.WParam, m.LParam, out var dwmResult) && m.Msg != WmNcHitTest) { m.Result = dwmResult; return; } } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        }
        // Keep WS_THICKFRAME/system commands; remove only native nonclient geometry.
        // Maximized thick-frame bounds can extend beyond the visible work area.
        if (m.Msg == WmNcCalcSize && m.WParam != 0)
        {
            if (IsZoomed(Handle))
            {
                var proposed = Marshal.PtrToStructure<NativeRect>(m.LParam);
                var bounds = Rectangle.FromLTRB(proposed.Left, proposed.Top, proposed.Right, proposed.Bottom);
                var client = Rectangle.Intersect(bounds, Screen.FromRectangle(bounds).WorkingArea);
                Marshal.StructureToPtr(new NativeRect { Left = client.Left, Top = client.Top, Right = client.Right, Bottom = client.Bottom }, m.LParam, false);
            }
            m.Result = 0; return;
        }
        if (m.Msg == WmGetMinMaxInfo)
        {
            base.WndProc(ref m); var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam); var screen = Screen.FromHandle(Handle);
            info.MaxPosition = new(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top); info.MaxSize = new(screen.WorkingArea.Width, screen.WorkingArea.Height);
            info.MinTrackSize = new(Math.Max(info.MinTrackSize.X, MinimumSize.Width), Math.Max(info.MinTrackSize.Y, MinimumSize.Height)); Marshal.StructureToPtr(info, m.LParam, false); return;
        }
        if (m.Msg == WmNcHitTest) { m.Result = HitTestScreen(DecodeScreenPoint(m.LParam)); return; }
        if (m.Msg == WmNcLButtonDown && m.WParam == HtCaption && WindowState == FormWindowState.Maximized)
        {
            captionPress = DecodeScreenPoint(m.LParam); pressedCaption = captionBar.RectangleToScreen(captionBar.ClientRectangle);
            normalSize = RestoreBounds.Size; captionPressed = true; Capture = true; m.Result = 0; return;
        }
        if (captionPressed && m.Msg is 0x200 or 0xa0)
        {
            var screen = DecodeScreenPoint(m.LParam); if (m.Msg == 0x200) screen = PointToScreen(screen);
            if (RestoreCaptionDrag(screen))
            {
                // Queue the native move so synthetic threshold tests never enter a synchronous move loop.
                if ((MouseButtons & MouseButtons.Left) != 0) PostMessage(Handle, WmNcLButtonDown, HtCaption, PackScreenPoint(screen));
            }
            m.Result = 0; return;
        }
        if (captionPressed && m.Msg is WmNcLButtonUp or 0x202) { CancelCaptionPress(); m.Result = 0; return; }
        if (m.Msg == 0x215) captionPressed = false;
        if (captionPressed && (m.Msg == 0x1f || (m.Msg == 0x6 && ((long)m.WParam & 0xffff) == 0))) CancelCaptionPress();
        if (m.Msg == WmNcLButtonDown && m.WParam == HtMaxButton && MaximizeBox && maximize.Enabled) { maxPressed = true; Capture = true; m.Result = 0; return; }
        if ((m.Msg == WmNcLButtonUp || m.Msg == 0x202) && maxPressed)
        {
            Point screen = DecodeScreenPoint(m.LParam); if (m.Msg == 0x202) screen = PointToScreen(screen);
            bool activate = Capture && maximize.Enabled && HitTestScreen(screen) == HtMaxButton;
            CancelMaximizePress(); if (activate) maximize.PerformClick(); m.Result = 0; return;
        }
        if (m.Msg == 0x215) maxPressed = false;
        if (maxPressed && (m.Msg == 0x1f || (m.Msg == 0x6 && ((long)m.WParam & 0xffff) == 0))) CancelMaximizePress();
        if (m.Msg == 0xa3 && m.WParam == HtCaption) { CancelCaptionPress(); ToggleMaximize(); m.Result = 0; return; }
        if (m.Msg == 0xa5 && m.WParam == HtCaption) { ShowSystemMenu(DecodeScreenPoint(m.LParam)); m.Result = 0; return; }
        base.WndProc(ref m);
    }
    void ShowSystemMenu(Point screen)
    {
        nint menu = GetSystemMenu(Handle, false);
        EnableMenuItem(menu, 0xf030, (uint)(MaximizeBox && WindowState != FormWindowState.Maximized ? 0 : 1));
        EnableMenuItem(menu, 0xf120, (uint)(WindowState != FormWindowState.Normal ? 0 : 1));
        EnableMenuItem(menu, 0xf020, (uint)(MinimizeBox ? 0 : 1));
        uint command = TrackPopupMenuEx(menu, 0x0100 | 0x0002, screen.X, screen.Y, Handle, 0); if (command != 0) SendMessage(Handle, 0x112, (nint)command, 0);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    { if (keyData == (Keys.Alt | Keys.Space)) { ShowSystemMenu(captionBar.PointToScreen(new(0, captionBar.Height))); return true; } return base.ProcessCmdKey(ref msg, keyData); }
    internal async Task SmokeChrome()
    {
        static nint Pack(Point point) => unchecked((nint)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16)));
        void CheckHit(Control control, Point screen, int expected)
        {
            if (SendMessage(control.Handle, WmNcHitTest, 0, Pack(screen)) != expected) throw new Exception($"Native hit test failed: {control.GetType().Name}, {screen}, expected {expected}.");
        }
        void CheckEdges(DarkWindow? window = null)
        {
            window ??= this;
            var b = window.Bounds;
            foreach (var (point, expected) in new (Point, int)[] {
                (new(b.Left + 1, b.Top + 1), HtTopLeft), (new(b.Right - 2, b.Top + 1), HtTopRight),
                (new(b.Left + 1, b.Bottom - 2), HtBottomLeft), (new(b.Right - 2, b.Bottom - 2), HtBottomRight),
                (new(b.Left + 1, b.Top + b.Height / 2), HtLeft), (new(b.Right - 2, b.Top + b.Height / 2), HtRight),
                (new(b.Left + b.Width / 2, b.Top + 1), HtTop), (new(b.Left + b.Width / 2, b.Bottom - 2), HtBottom) })
            {
                CheckHit(window, point, expected);
                foreach (var control in window.hitRouters.Keys.Where(c => c.Visible && c.RectangleToScreen(c.ClientRectangle).Contains(point))) CheckHit(control, point, -1);
            }
        }
        CheckEdges();
        foreach (var control in new Control[] { captionIcon, minimize, maximize, close })
        {
            var point = control.PointToScreen(new(control.Width / 2, control.Height / 2));
            CheckHit(control, point, control == maximize ? -1 : HtClient);
            CheckHit(this, point, control == maximize ? HtMaxButton : HtClient);
            var top = new Point(point.X, Top + 1); CheckHit(control, top, -1); CheckHit(this, top, HtTop);
        }
        using (var child = new SmokePanel { Bounds = new(0, WindowContent.Height - 10, 20, 10), Anchor = AnchorStyles.Left | AnchorStyles.Bottom })
        {
            WindowContent.Controls.Add(child); child.BringToFront();
            var point = new Point(Left + 1, Bottom - 2); CheckHit(child, point, -1);
            child.RecreateHandleForSmoke();
            CheckHit(child, point, -1);
            WindowContent.Controls.Remove(child);
            if (hitRouters.ContainsKey(child)) throw new Exception("Removed child retained a hit router.");
        }
        using (var negativeWindow = new DarkWindow { StartPosition = FormStartPosition.Manual, Bounds = new(-400, -200, 600, 400), ShowInTaskbar = false })
        {
            negativeWindow.Show(this); CheckEdges(negativeWindow);
            var button = negativeWindow.maximize;
            SendMessage(negativeWindow.Handle, WmNcLButtonDown, HtMaxButton, Pack(button.PointToScreen(new(button.Width / 2, button.Height / 2))));
            if (!negativeWindow.Capture) throw new Exception("Disposal fixture did not acquire capture.");
            negativeWindow.Dispose();
            if (negativeWindow.maxPressed || negativeWindow.Capture || negativeWindow.hitRouters.Count != 0) throw new Exception("Disposal retained capture or child routers.");
        }
        Point MaxCenter() => maximize.PointToScreen(new(maximize.Width / 2, maximize.Height / 2));
        void Press()
        {
            SendMessage(Handle, WmNcLButtonDown, HtMaxButton, Pack(MaxCenter()));
            if (!maxPressed || !Capture) throw new Exception("Maximize press did not acquire capture.");
        }
        void Release(Point screen) => SendMessage(Handle, 0x202, 0, Pack(PointToClient(screen)));
        void CheckCancelled()
        {
            Release(MaxCenter());
            if (maxPressed || Capture || WindowState != FormWindowState.Normal) throw new Exception("Cancelled maximize press activated or retained capture.");
        }
        Press(); Release(new(Left - 20, Top - 20)); CheckCancelled();
        foreach (int message in new[] { 0x1f, 0x6 }) { Press(); SendMessage(Handle, message, 0, 0); CheckCancelled(); }
        Press(); captionIcon.Capture = true;
        if (maxPressed || Capture) throw new Exception("Capture loss retained maximize press.");
        captionIcon.Capture = false; CheckCancelled();
        Press(); maximize.Enabled = false; Release(MaxCenter()); maximize.Enabled = true; CheckCancelled();
        int activations = 0;
        EventHandler activated = (_, _) => { activations++; if (Capture || maxPressed) throw new Exception("Maximize activated before releasing capture."); };
        maximize.Click += activated;
        Press(); Release(MaxCenter());
        if (WindowState != FormWindowState.Maximized || maxPressed || Capture) throw new Exception("Captured maximize release failed.");
        Release(MaxCenter());
        maximize.Click -= activated;
        if (WindowState != FormWindowState.Maximized || activations != 1) throw new Exception("Duplicate release activated maximize twice.");
        var maxTop = new Point(Left + 1, Top + 1);
        if (SendMessage(Handle, WmNcHitTest, 0, Pack(maxTop)) is >= HtLeft and <= HtBottomRight) throw new Exception("Maximized window retained resize edge.");
        maximize.PerformClick();
        var original = Bounds; maximize.PerformClick(); await Task.Delay(60); if (WindowState != FormWindowState.Maximized || maximize.AccessibleName != "Restore") throw new Exception("Caption maximize failed.");
        var clientScreen = RectangleToScreen(ClientRectangle); var workArea = Screen.FromRectangle(Bounds).WorkingArea;
        if (!workArea.Contains(clientScreen) || clientScreen.Location != workArea.Location || captionBar.PointToScreen(Point.Empty) != clientScreen.Location) throw new Exception("Maximized client or caption origin is clipped outside the work area.");
        var captionBottom = captionText.PointToScreen(new(captionText.Width / 2, captionText.Height - 2));
        CheckHit(this, captionBottom, HtCaption); CheckHit(captionText, captionBottom, -1);
        maximize.PerformClick(); await Task.Delay(60); if (WindowState != FormWindowState.Normal || Bounds != original) throw new Exception("Caption restore bounds failed.");
        minimize.PerformClick(); if (WindowState != FormWindowState.Minimized) throw new Exception("Caption minimize failed."); WindowState = FormWindowState.Normal;
        int closing = 0; FormClosingEventHandler reject = (_, e) => { closing++; e.Cancel = true; }; FormClosing += reject; close.PerformClick(); FormClosing -= reject;
        if (closing != 1 || IsDisposed) throw new Exception("Caption close did not route through a single cancellable FormClosing.");
        var bounds = new Rectangle(-1600, -300, 1000, 700);
        if (FrameHitTest(new(-1200, -280), bounds, 46, 7, false, Rectangle.Empty, Rectangle.Empty, Rectangle.Empty) != HtCaption || FrameHitTest(new(-1599, -299), bounds, 46, 7, false, Rectangle.Empty, Rectangle.Empty, Rectangle.Empty) != HtTopLeft || FrameHitTest(new(-1000, 100), bounds, 46, 7, false, Rectangle.Empty, Rectangle.Empty, Rectangle.Empty) != HtClient) throw new Exception("Negative-coordinate frame hit testing failed.");
        if (DecodeScreenPoint(unchecked((nint)(((uint)(ushort)-280 << 16) | (ushort)-1200))) != new Point(-1200, -280)) throw new Exception("Signed nonclient coordinates failed.");
        var threshold = new Size(8, 6); var origin = new Point(-1200, -280);
        foreach (var inside in new[] { origin, new Point(-1204, -283), new Point(-1197, -278) })
            if (OutsideDragThreshold(origin, inside, threshold)) throw new Exception("Caption threshold rejected an inside point.");
        foreach (var outside in new[] { new Point(-1205, -280), new Point(-1196, -280), new Point(-1200, -284), new Point(-1200, -277) })
            if (!OutsideDragThreshold(origin, outside, threshold)) throw new Exception("Caption threshold missed an edge.");
        if (RestoredCaptionBounds(new(-1100, -290), new(-900, -200), new(-1600, -300, 1000, 46), new(900, 620), 46) != new Rectangle(-1350, -210, 900, 620)) throw new Exception("Negative-coordinate caption restore geometry failed.");
        var beforeDrag = Bounds;
        void CaptionPress()
        {
            WindowState = FormWindowState.Maximized;
            var point = captionText.PointToScreen(new(captionText.Width / 2, 12));
            SendMessage(Handle, WmNcLButtonDown, HtCaption, Pack(point));
            if (!captionPressed || !Capture) throw new Exception("Caption press did not acquire capture.");
        }
        void CheckCaptionCancelled()
        {
            if (captionPressed || Capture || WindowState != FormWindowState.Maximized) throw new Exception("Caption cancellation restored or retained capture.");
        }
        CaptionPress(); SendMessage(Handle, 0x200, 0, Pack(PointToClient(captionPress)));
        if (!captionPressed || WindowState != FormWindowState.Maximized) throw new Exception("Caption restored inside drag threshold.");
        SendMessage(Handle, 0x202, 0, Pack(PointToClient(captionPress))); CheckCaptionCancelled();
        foreach (int message in new[] { 0x1f, 0x6 }) { CaptionPress(); SendMessage(Handle, message, 0, 0); CheckCaptionCancelled(); }
        CaptionPress(); captionIcon.Capture = true; captionIcon.Capture = false; CheckCaptionCancelled();
        CaptionPress(); WindowState = FormWindowState.Normal;
        if (captionPressed || Capture) throw new Exception("State change retained caption capture.");
        Bounds = beforeDrag;
        CaptionPress(); var moved = new Point(captionPress.X + SystemInformation.DragSize.Width, captionPress.Y + SystemInformation.DragSize.Height);
        var expectedDrag = RestoredCaptionBounds(captionPress, moved, pressedCaption, beforeDrag.Size, captionBar.Height);
        SendMessage(Handle, 0x200, 0, Pack(PointToClient(moved)));
        if (captionPressed || Capture || WindowState != FormWindowState.Normal || Bounds != expectedDrag) throw new Exception("Caption threshold restore failed.");
        Bounds = beforeDrag;
        CaptionPress(); SendMessage(Handle, 0xa3, HtCaption, Pack(captionPress));
        if (captionPressed || Capture || WindowState != FormWindowState.Normal) throw new Exception("Caption double-click retained capture or failed to restore.");
        Bounds = beforeDrag;
        using (var pendingWindow = new DarkWindow { Bounds = beforeDrag, ShowInTaskbar = false })
        {
            pendingWindow.Show(this); pendingWindow.WindowState = FormWindowState.Maximized;
            var point = pendingWindow.captionText.PointToScreen(new(60, 12));
            SendMessage(pendingWindow.Handle, WmNcLButtonDown, HtCaption, Pack(point));
            if (!pendingWindow.captionPressed || !pendingWindow.Capture) throw new Exception("Caption disposal fixture did not acquire capture.");
            pendingWindow.Dispose();
            if (pendingWindow.captionPressed || pendingWindow.Capture) throw new Exception("Disposal retained caption capture.");
        }
    }
    internal static void CheckCommandBounds(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (!child.Visible) continue;
            if (child is FlowLayoutPanel panel && panel.AutoSize)
            {
                if (panel.HorizontalScroll.Visible || panel.VerticalScroll.Visible) throw new Exception("Command panel has a scrollbar.");
                var commands = panel.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
                foreach (var command in commands)
                {
                    if (!panel.ClientRectangle.Contains(command.Bounds)) throw new Exception($"Clipped command: {command.Text}, {command.Bounds}, panel {panel.ClientSize}.");
                    foreach (var other in commands) if (command != other && command.Bounds.IntersectsWith(other.Bounds)) throw new Exception($"Overlapping commands: {command.Text}, {other.Text}.");
                }
            }
            CheckCommandBounds(child);
        }
    }
    internal async Task SmokeResponsive(Control view, string screenshot, Func<Task>? modes = null)
    {
        foreach (var (width, suffix) in new[] { (1260, "wide"), (900, "narrow"), (1140, "medium"), (1260, "wide") })
        {
            Width = width; Height = 620; await Task.Delay(100); view.PerformLayout(); if (modes != null) await modes(); CheckCommandBounds(view);
            foreach (var editor in Descendants(view).OfType<RichTextBox>().Where(c => c.Visible))
                if (editor.Width <= 0 || editor.Height < Font.Height) throw new Exception("Responsive commands left no usable editor area.");
            CapturePreview(Path.ChangeExtension(Path.GetFullPath(screenshot), $".{suffix}.png"));
            Console.WriteLine($"Responsive layout: outer {Width}x{Height}, client {ClientSize.Width}x{ClientSize.Height}, {suffix}.");
        }
        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) { CancelCaptionPress(); CancelMaximizePress(); UnwatchChildren(this); captionIcon.Image = null; iconBitmap?.Dispose(); } base.Dispose(disposing); }
    static nint PackScreenPoint(Point point) => unchecked((nint)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16)));
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    enum DwmWindowAttribute { DWMWA_USE_IMMERSIVE_DARK_MODE = 20 }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint hwnd, DwmWindowAttribute attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool DwmDefWindowProc(nint hwnd, int message, nint wParam, nint lParam, out nint result);
    [DllImport("user32.dll")] static extern nint GetSystemMenu(nint hwnd, bool revert);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] static extern bool EnableMenuItem(nint menu, uint item, uint enable);
    [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);
    [DllImport("user32.dll")] static extern nint SendMessage(nint hwnd, int message, nint wParam, nint lParam);
    [DllImport("user32.dll")] static extern bool PostMessage(nint hwnd, int message, nint wParam, nint lParam);
    sealed class SmokePanel : Panel { internal void RecreateHandleForSmoke() => RecreateHandle(); }
    sealed class ChildHitRouter : NativeWindow, IDisposable
    {
        readonly DarkWindow owner;
        readonly Control control;
        public ChildHitRouter(DarkWindow owner, Control control)
        {
            this.owner = owner; this.control = control;
            control.HandleCreated += Created; control.HandleDestroyed += Destroyed;
            if (control.IsHandleCreated) AssignHandle(control.Handle);
        }
        void Created(object? sender, EventArgs e) => AssignHandle(control.Handle);
        void Destroyed(object? sender, EventArgs e) { if (Handle != 0) ReleaseHandle(); }
        public void Dispose() { control.HandleCreated -= Created; control.HandleDestroyed -= Destroyed; if (Handle != 0) ReleaseHandle(); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest)
            {
                int hit = owner.HitTestScreen(DecodeScreenPoint(m.LParam));
                if ((hit == HtCaption && (control == owner.captionBar || control == owner.captionText)) || hit == HtMaxButton || hit is >= HtLeft and <= HtBottomRight) { m.Result = -1; return; }
            }
            base.WndProc(ref m);
        }
    }
}
