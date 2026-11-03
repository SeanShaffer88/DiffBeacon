using DiffBeacon.Core;
using System.Runtime.InteropServices;

namespace DiffBeacon.App;
internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
    private const uint AttachParentProcess = 0xFFFFFFFF;

    private static void AttachCliConsole()
    {
        if (!AttachConsole(AttachParentProcess)) AllocConsole();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0) AttachCliConsole();
            if (args.Length == 1 && args[0] is "--help" or "-h") { Console.WriteLine("DiffBeacon\n  DiffBeacon [LEFT RIGHT]\n  DiffBeacon --mode text|folder|binary|table LEFT RIGHT\n  DiffBeacon --check LEFT RIGHT\n  DiffBeacon --smoke SCREENSHOT.png\nExit: 0 equal/success; 1 different; 2 invalid input/error."); return 0; }
            if (args.Length > 0 && args[0] == "--check") { if (args.Length != 3) throw new ArgumentException("--check requires LEFT RIGHT"); var result = BinaryComparison.Compare(args[1], args[2]); Console.WriteLine(result.Summary); return result.DifferentBytes == 0 ? 0 : 1; }
            string mode = "Text", left = "", right = ""; string? smoke = null;
            if (args.Length == 2 && args[0] == "--smoke") smoke = args[1];
            else if (args.Length == 2) { left = args[0]; right = args[1]; mode = Directory.Exists(left) && Directory.Exists(right) ? "Folders" : "Text"; }
            else if (args.Length == 4 && args[0] == "--mode") { mode = args[1] switch { "text" => "Text", "folder" => "Folders", "binary" => "Binary", "table" => "Tables", _ => throw new ArgumentException("Unknown mode.") }; left = args[2]; right = args[3]; }
            else if (args.Length != 0) throw new ArgumentException("Invalid arguments. Use --help.");
            ApplicationConfiguration.Initialize(); using var form = new MainForm(mode, left, right, smoke); Application.Run(form); return form.SmokeFailed ? 2 : 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
    }
}
