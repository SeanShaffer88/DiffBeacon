using DiffBeacon.Core;

namespace DiffBeacon.App;

/// <summary>Line-position overview. Retains bounded diff metadata; paints at most one marker per pixel.</summary>
sealed class DifferenceOverview : Control
{
    public sealed record Marker(int Row, Change Status);
    Marker[] markers = [];
    readonly List<(int X, Change Status)> pixels = new();
    int totalRows = 1, selectedIndex = -1;
    bool dragging;
    public event Action<int>? DifferenceRequested;
    public int MarkerCount => markers.Length;
    public int SelectedIndex => selectedIndex;
    public Change? SelectedStatus => selectedIndex >= 0 ? markers[selectedIndex].Status : null;
    int TrackLeft => 12;
    int TrackWidth => Math.Max(1, ClientSize.Width - 25);
    public DifferenceOverview()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = 42; Dock = DockStyle.Top; TabStop = true;
        BackColor = DarkTheme.Surface; ForeColor = DarkTheme.Muted;
        AccessibleName = "Difference Overview"; AccessibleRole = AccessibleRole.Slider;
        AccessibleDescription = "Differences positioned across all aligned rows. Click or drag to jump; Left and Right move; Home and End select first and last.";
        Enabled = false;
    }
    public void SetRows(IReadOnlyList<DiffRow>? rows)
    {
        totalRows = Math.Max(1, rows?.Count ?? 0);
        markers = rows == null ? [] : rows.Select((r, i) => new Marker(i, r.Status)).Where(m => m.Status != Change.Equal).ToArray();
        selectedIndex = -1; dragging = false; Capture = false; Enabled = markers.Length > 0; RebuildPixels(); Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }
    public void SetSelected(int index)
    {
        int next = index >= 0 && index < markers.Length ? index : -1;
        if (selectedIndex == next) return;
        selectedIndex = next; Invalidate(); AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }
    int XForRow(int row) => TrackLeft + (int)Math.Round(row * (double)TrackWidth / Math.Max(1, totalRows - 1));
    void RebuildPixels()
    {
        pixels.Clear(); int previous = -1;
        foreach (var marker in markers) { int x = XForRow(marker.Row); if (x != previous) { pixels.Add((x, marker.Status)); previous = x; } }
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); RebuildPixels(); }
    public void ActivateAt(int x)
    {
        if (!Enabled || markers.Length == 0) return;
        double row = Math.Clamp((x - TrackLeft) / (double)TrackWidth, 0, 1) * Math.Max(1, totalRows - 1);
        int low = 0, high = markers.Length;
        while (low < high) { int mid = low + (high - low) / 2; if (markers[mid].Row < row) low = mid + 1; else high = mid; }
        int index = low >= markers.Length ? markers.Length - 1 : low;
        if (index > 0 && row - markers[index - 1].Row <= markers[index].Row - row) index--;
        DifferenceRequested?.Invoke(index);
    }
    public int MarkerX(int index) => XForRow(markers[index].Row);
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left && Enabled) { Focus(); dragging = true; Capture = true; ActivateAt(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) ActivateAt(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) { dragging = false; Capture = false; } }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (!Enabled || markers.Length == 0) return;
        int next = e.KeyCode switch { Keys.Home => 0, Keys.End => markers.Length - 1, Keys.Left => Math.Max(0, selectedIndex - 1), Keys.Right => Math.Min(markers.Length - 1, selectedIndex + 1), _ => -1 };
        if (next >= 0) { DifferenceRequested?.Invoke(next); e.Handled = true; e.SuppressKeyPress = true; }
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Color background = SystemInformation.HighContrast ? SystemColors.Window : BackColor;
        e.Graphics.Clear(background);
        using var rail = new Pen(DarkTheme.DecorativeBorder);
        e.Graphics.DrawLine(rail, TrackLeft, 14, TrackLeft + TrackWidth, 14);
        foreach (var pixel in pixels)
        {
            Color color = SystemInformation.HighContrast ? SystemColors.WindowText : pixel.Status switch { Change.LeftOnly or Change.RightOnly => Color.FromArgb(79, 185, 177), _ => DarkTheme.Amber };
            using var brush = new SolidBrush(color); e.Graphics.FillRectangle(brush, pixel.X - 2, 6, 4, 16);
        }
        if (selectedIndex >= 0)
        {
            int x = XForRow(markers[selectedIndex].Row);
            using var selected = new Pen(DarkTheme.Focus, 2);
            e.Graphics.DrawRectangle(selected, x - 4, 3, 8, 22);
        }
        string label = markers.Length == 0 ? "Difference Overview · Compare Text To Show Markers" : $"Difference Overview · {markers.Length:N0} Markers · {(selectedIndex >= 0 ? $"Selected {selectedIndex + 1}: {markers[selectedIndex].Status}" : "Click Or Use Arrow Keys")}";
        TextRenderer.DrawText(e.Graphics, label, Font, new Rectangle(12, 25, Math.Max(1, Width - 24), 16), DarkTheme.HighContrast ? SystemColors.WindowText : ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(1, 1, Math.Max(0, Width - 3), Math.Max(0, Height - 3)), SystemColors.Highlight, background);
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new OverviewAccessibility(this);
    sealed class OverviewAccessibility(DifferenceOverview owner) : ControlAccessibleObject(owner)
    {
        public override string? Value => owner.selectedIndex < 0 ? "No Difference Selected" : $"Difference {owner.selectedIndex + 1} Of {owner.markers.Length}, {owner.markers[owner.selectedIndex].Status}, Aligned Row {owner.markers[owner.selectedIndex].Row + 1}";
    }
}
