namespace DiffBeacon.App;

internal sealed record PaletteCommand(string Name, Func<bool> IsAvailable, Action Invoke)
{
    public static PaletteCommand ForButton(Button button, Action? reveal = null, string? name = null)
    {
        return new(name ?? button.Text, () => !button.IsDisposed && button.Enabled, () =>
        {
            reveal?.Invoke();
            if (button.CanSelect) button.PerformClick();
        });
    }

    public override string ToString() => Name;
}

internal sealed class CommandPalette : DarkWindow
{
    readonly PaletteCommand[] commands;
    readonly TextBox search = new() { Dock = DockStyle.Fill, AccessibleName = "Search Commands" };
    readonly ListBox matches = new DarkListBox { Dock = DockStyle.Fill, AccessibleName = "Matching Commands", IntegralHeight = false };
    readonly Label status = new() { AutoSize = true };
    readonly Button run = new() { Text = "Run Command", AutoSize = true };
    readonly Button close = new() { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
    public PaletteCommand? Chosen { get; private set; }

    public CommandPalette(IEnumerable<PaletteCommand> commands)
    {
        this.commands = commands.ToArray();
        Text = "Commands";
        Size = new(550, 415);
        MinimumSize = new(420, 330);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = MinimizeBox = MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(12), ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Search Current Comparison Commands", AutoSize = true }, 0, 0);
        layout.Controls.Add(search, 0, 1);
        layout.Controls.Add(matches, 0, 2);
        layout.Controls.Add(status, 0, 3);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        actions.Controls.AddRange([run, close]);
        layout.Controls.Add(actions, 0, 4);
        WindowContent.Controls.Add(layout);
        search.TextChanged += (_, _) => FilterCommands();
        matches.SelectedIndexChanged += (_, _) => run.Enabled = matches.SelectedItem is PaletteCommand command && command.IsAvailable();
        matches.DoubleClick += (_, _) => Choose();
        run.Click += (_, _) => Choose();
        AcceptButton = run;
        CancelButton = close;
        Shown += (_, _) => search.Focus();
        DarkTheme.Apply(WindowContent);
        FilterCommands();
    }

    void FilterCommands()
    {
        string[] words = search.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        matches.BeginUpdate();
        try
        {
            matches.Items.Clear();
            foreach (var command in commands)
                if (command.IsAvailable() && words.All(word => command.Name.Contains(word, StringComparison.CurrentCultureIgnoreCase)))
                    matches.Items.Add(command);
            if (matches.Items.Count > 0) matches.SelectedIndex = 0;
            run.Enabled = matches.Items.Count > 0;
            status.Text = matches.Items.Count == 0 ? "No Enabled Commands Match" : $"{matches.Items.Count} Matching Commands";
        }
        finally { matches.EndUpdate(); }
    }

    void Choose()
    {
        if (matches.SelectedItem is not PaletteCommand command || !command.IsAvailable())
        {
            FilterCommands();
            return;
        }
        Chosen = command;
        DialogResult = DialogResult.OK;
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (ActiveControl == search && keyData is Keys.Up or Keys.Down && matches.Items.Count > 0)
        {
            matches.SelectedIndex = Math.Clamp(matches.SelectedIndex + (keyData == Keys.Down ? 1 : -1), 0, matches.Items.Count - 1);
            return true;
        }
        if ((ActiveControl == search || ActiveControl == matches) && keyData == Keys.Enter)
        {
            Choose();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    internal void CheckKeyboard(string screenshot)
    {
        if (!search.Focused) throw new Exception("Command search did not receive initial focus.");
        search.Text = "swap";
        if (matches.Items.Count != 1 || matches.SelectedItem is not PaletteCommand { Name: "Swap" })
            throw new Exception("Command search did not filter the active comparison.");
        search.Text = "cancel";
        if (matches.Items.Count != 0 || run.Enabled) throw new Exception("A disabled command appeared in search.");
        search.Text = "difference";
        if (matches.Items.Count != 0) throw new Exception("Difference commands appeared before a comparison.");
        search.Text = "not a comparison command";
        if (matches.Items.Count != 0 || run.Enabled) throw new Exception("An empty command search could run.");
        var message = new Message();
        if (!ProcessCmdKey(ref message, Keys.Enter) || Chosen != null)
            throw new Exception("Enter accepted an empty command search.");
        search.Text = "save";
        if (matches.Items.Count < 2 || !ProcessCmdKey(ref message, Keys.Down) ||
            matches.SelectedIndex != 1 || !search.Focused)
            throw new Exception("Command search did not navigate matching commands.");
        CapturePreview(screenshot);
        ProcessDialogKey(Keys.Escape);
    }

    internal void ChooseForSmoke(string name)
    {
        search.Text = name;
        ActiveControl = search;
        var message = new Message();
        if (matches.Items.Count != 1 || !ProcessCmdKey(ref message, Keys.Enter) || Chosen?.Name != name)
            throw new Exception($"Enter did not choose {name}. Matches: {matches.Items.Count}. Chosen: {Chosen?.Name ?? "none"}.");
    }
}
