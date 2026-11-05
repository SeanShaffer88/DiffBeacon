using System.Runtime.InteropServices;

namespace DiffBeacon.App;

// Native RichEdit operations stay on the owning UI thread. No filesystem/network bridge.
sealed class ComparisonEditor : RichTextBox
{
    public event EventHandler? ViewportChanged;
    public int[] LogicalStarts { get; private set; } = [0];
    public string Snapshot { get; private set; } = "";
    bool decorating;
    int undoSuspensionDepth;
    public bool ChangingText { get; private set; }
    internal int LastSuspendHResult { get; private set; }
    internal int LastSuspendActual { get; private set; }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern nint Send(nint window, int message, nint wparam, nint lparam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern nint SendPoint(nint window, int message, nint wparam, ref Point point);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern nint SendOle(nint window, int message, nint wparam, out nint value);
    [DllImport("user32.dll")] static extern bool GetScrollInfo(nint window, int bar, ref ScrollInfo info);
    [StructLayout(LayoutKind.Sequential)] struct ScrollInfo { public uint Size, Mask; public int Min, Max; public uint Page; public int Position, Track; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int UndoCall(nint document, int count, out int actual);
    public int FirstDisplayLine => (int)Send(Handle, 0xCE, 0, 0);
    public int DisplayLineAt(int rawIndex) => (int)Send(Handle, 0x436, 0, rawIndex); // EM_EXLINEFROMCHAR
    public int RawAtDisplayLine(int line) => (int)Send(Handle, 0xBB, line, 0); // EM_LINEINDEX: visual only
    public int LogicalAt(int rawIndex) { int at = Array.BinarySearch(LogicalStarts, Math.Max(0, rawIndex)); return at >= 0 ? at : Math.Max(0, ~at - 1); }
    public int HorizontalPosition { get { var info = new ScrollInfo { Size = (uint)Marshal.SizeOf<ScrollInfo>(), Mask = 4 }; return GetScrollInfo(Handle, 0, ref info) ? info.Position : 0; } }
    public void ScrollDisplayTo(int line) => Send(Handle, 0xB6, 0, line - FirstDisplayLine);
    internal (int RawIndex, int Horizontal) CaptureViewport() => (Math.Max(0, RawAtDisplayLine(FirstDisplayLine)), HorizontalPosition);
    internal void RestoreViewport((int RawIndex, int Horizontal) anchor)
    {
        ScrollHorizontalTo(anchor.Horizontal);
        ScrollDisplayTo(DisplayLineAt(Math.Min(anchor.RawIndex, TextLength)));
    }
    public void ScrollHorizontalTo(int x)
    {
        var info = new ScrollInfo { Size = (uint)Marshal.SizeOf<ScrollInfo>(), Mask = 3 };
        if (!GetScrollInfo(Handle, 0, ref info)) return;
        // EM_GETSCROLLPOS Y is only 16 bits. Preserve vertical by display line instead.
        int first = FirstDisplayLine; Point point = default; SendPoint(Handle, 0x4DD, 0, ref point);
        point.X = Math.Clamp(x, info.Min, Math.Max(info.Min, info.Max - (int)info.Page + 1));
        SendPoint(Handle, 0x4DE, 0, ref point); ScrollDisplayTo(first);
    }
    protected override void OnTextChanged(EventArgs e)
    {
        if (!decorating)
        {
            Snapshot = Text; var starts = new List<int> { 0 };
            for (int i = 0; i < Snapshot.Length; i++) if (Snapshot[i] == '\n') starts.Add(i + 1);
            LogicalStarts = starts.ToArray();
        }
        // EN_CHANGE is synchronous, before RichEdit has closed its native edit action.
        // Subscribers may invalidate results here, but must defer formatting until it returns.
        ChangingText = true;
        try { base.OnTextChanged(e); }
        finally { ChangingText = false; }
    }
    protected override void WndProc(ref Message m)
    {
        int message = m.Msg;
        base.WndProc(ref m);
        if (!decorating && message is 0x115 or 0x114 or 0x20A or 0x20E or 0x100 or 0xB6 or 0x4DE)
            ViewportChanged?.Invoke(this, EventArgs.Empty);
    }
    internal void CheckSuspendedDecoration()
    {
        // Use a separate native editor so this regression cannot alter the caller's
        // typing fixture. All app-owned nested scopes use WithUndoSuspended.
        using var box = new ComparisonEditor { Text = "base" };
        _ = box.Handle; box.ClearUndo(); box.Select(box.TextLength, 0); box.SelectedText = " typed";
        if (!box.CanUndo) throw new Exception("Native suspension fixture did not record typing.");
        box.WithUndoSuspended(() =>
        {
            box.Decorate(() => { box.SelectAll(); box.SelectionBackColor = Color.Yellow; });
            box.Select(box.TextLength, 0); box.SelectedText = " unrecorded";
        });
        box.Undo();
        if (box.Text != "base unrecorded") throw new Exception("Decoration resumed a nested app-owned undo suspension.");
        box.Redo();
        if (box.Text != "base typed unrecorded") throw new Exception("Nested decoration changed native redo.");
        box.Select(box.TextLength, 0); box.SelectedText = " recorded"; box.Undo();
        if (box.Text != "base typed unrecorded") throw new Exception("The outer suspension did not restore recording.");
        box.Select(2, 3); bool modified = box.Modified; bool rejected = false;
        try { box.Decorate(() => { box.SelectAll(); box.SelectionBackColor = Color.Blue; throw new InvalidOperationException("Expected paint failure"); }); }
        catch (InvalidOperationException ex) when (ex.Message == "Expected paint failure") { rejected = true; }
        if (!rejected || box.SelectionStart != 2 || box.SelectionLength != 3 || box.Modified != modified) throw new Exception("Paint failure did not restore editor state.");
        box.Select(box.TextLength, 0); box.SelectedText = " after failure"; box.Undo();
        if (box.Text != "base typed unrecorded") throw new Exception("Paint failure left native undo suspended.");
        // A native owner can disable recording independently; balanced suspension
        // must leave it disabled. EM_SETUNDOLIMIT(0) also clears old undo records.
        Send(box.Handle, 0x452, 0, 0);
        box.Decorate(() => { box.SelectAll(); box.SelectionBackColor = Color.Blue; });
        box.Select(box.TextLength, 0); box.SelectedText = " disabled";
        if (box.CanUndo || box.CanRedo) throw new Exception("Decoration enabled externally disabled native undo.");
    }
    void WithUndoSuspended(Action action)
    {
        // TOM returns S_FALSE with actual=0 even when tomSuspend/tomResume succeed.
        // It exposes no prior-suspension query and native suspension is not nested.
        // Coordinate every app-owned scope here; unrelated raw COM suspension is
        // outside this editor's API. A native zero undo limit remains untouched.
        if (undoSuspensionDepth > 0)
        {
            undoSuspensionDepth++;
            try { action(); }
            finally { undoSuspensionDepth--; }
            return;
        }
        nint ole = 0, document = 0; UndoCall? undo = null; bool suspended = false;
        try
        {
            SendOle(Handle, 0x43C, 0, out ole); // EM_GETOLEINTERFACE
            if (ole == 0) throw new InvalidOperationException("RichEdit undo preservation is unavailable.");
            Guid id = new("8CC497C0-A1DF-11CE-8098-00AA0047BE5D");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(ole, in id, out document));
            // SDK 10.0.26100.0 um/TOM.h ITextDocumentVtbl: Undo is slot 22.
            undo = Marshal.GetDelegateForFunctionPointer<UndoCall>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(document), 22 * IntPtr.Size));
            int hr = undo(document, -9999995, out int actual);
            LastSuspendHResult = hr; LastSuspendActual = actual; Marshal.ThrowExceptionForHR(hr);
            suspended = true; undoSuspensionDepth = 1;
            action();
        }
        finally
        {
            undoSuspensionDepth = 0;
            try { if (suspended) Marshal.ThrowExceptionForHR(undo!(document, -9999994, out _)); }
            finally { if (document != 0) Marshal.Release(document); if (ole != 0) Marshal.Release(ole); }
        }
    }
    public void Decorate(Action paint)
    {
        if (!IsHandleCreated || IsDisposed) return;
        if (decorating || ChangingText) throw new InvalidOperationException("Decoration cannot reenter a native text edit.");
        int start = SelectionStart, length = SelectionLength, first = FirstDisplayLine, horizontal = HorizontalPosition;
        bool modified = Modified;
        decorating = true;
        try
        {
            WithUndoSuspended(() =>
            {
                Send(Handle, 0xB, 0, 0);
                try { paint(); }
                finally { Select(Math.Min(start, TextLength), Math.Min(length, Math.Max(0, TextLength - start))); Modified = modified; ScrollHorizontalTo(horizontal); ScrollDisplayTo(first); }
            });
        }
        finally
        {
            Send(Handle, 0xB, 1, 0); decorating = false; Invalidate();
        }
    }
}
