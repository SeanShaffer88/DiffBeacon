using System.Text.Json;
using Microsoft.Win32;

namespace DiffBeacon.App;

enum AppearanceTheme { Dark, Light, System }
enum AppearanceAccent { Crimson, Blue, Teal }
enum WorkspaceFinish { SquareTread, Flat }

readonly record struct AppearanceOptions(AppearanceTheme Theme, AppearanceAccent Accent, WorkspaceFinish Finish)
{
    internal static AppearanceOptions Default => new(AppearanceTheme.Dark, AppearanceAccent.Crimson, WorkspaceFinish.SquareTread);
}

static class VaporGridAppearance
{
    internal static AppearanceOptions Current { get; private set; } = AppearanceOptions.Default;
    internal static AppearanceStore? Store { get; private set; }
    static bool systemLight;
    internal static bool IsLight => IsLightFor(Current.Theme, systemLight);
    internal static bool IsLightFor(AppearanceTheme theme, bool systemUsesLight) =>
        theme == AppearanceTheme.Light || (theme == AppearanceTheme.System && systemUsesLight);

    internal static void Initialize(bool usePortablePreferences)
    {
        Store = usePortablePreferences ? new AppearanceStore(Path.Combine(AppContext.BaseDirectory, "data", "appearance.json")) : null;
        Current = Store?.Load() ?? AppearanceOptions.Default;
        systemLight = Current.Theme == AppearanceTheme.System && ReadSystemLight();
    }

    internal static bool TrySet(AppearanceOptions next, AppearanceStore? store, out string? failure)
    {
        failure = null;
        if (next == Current) return true;
        try { store?.Save(next); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            failure = "Could Not Save Appearance Beside DiffBeacon. Check That The Data Folder Is Writable And Not A Link.";
            return false;
        }
        Apply(next);
        return true;
    }

    internal static void Apply(AppearanceOptions next)
    {
        Current = next;
        if (Current.Theme == AppearanceTheme.System) systemLight = ReadSystemLight();
        DarkWindow.RefreshAppearance();
    }

    internal static void SystemSettingsChanged()
    {
        if (Current.Theme != AppearanceTheme.System) return;
        bool light = ReadSystemLight();
        if (light == systemLight) return;
        systemLight = light;
        DarkWindow.RefreshAppearance();
    }

    static bool ReadSystemLight()
    {
        // Microsoft Learn, Windows settings reference: AppsUseLightTheme is a user DWORD.
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}

sealed class AppearanceStore(string path)
{
    const int Version = 1;
    const int MaximumBytes = 2048;
    static string Id<T>(T value) where T : struct, Enum => JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
    static T Option<T>(JsonElement root, string name, T fallback) where T : struct, Enum
    {
        if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            string? id = property.GetString();
            foreach (T option in Enum.GetValues<T>())
                if (Id(option) == id) return option;
        }
        return fallback;
    }

    internal AppearanceOptions Load()
    {
        var fallback = AppearanceOptions.Default;
        try
        {
            if (IsLink(Path.GetDirectoryName(path)!) || IsLink(path)) return fallback;
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (input.Length is <= 0 or > MaximumBytes) return fallback;
            var bytes = new byte[(int)input.Length];
            input.ReadExactly(bytes);
            if (input.ReadByte() != -1) return fallback;
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int schemaVersion) || schemaVersion != Version)
                return fallback;
            return new(Option(root, "theme", fallback.Theme), Option(root, "accent", fallback.Accent), Option(root, "finish", fallback.Finish));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
        {
            return fallback;
        }
    }

    internal void Save(AppearanceOptions options)
    {
        if (!Enum.IsDefined(options.Theme) || !Enum.IsDefined(options.Accent) || !Enum.IsDefined(options.Finish))
            throw new ArgumentException("Invalid appearance preference.", nameof(options));
        var directory = Path.GetDirectoryName(path)!;
        if (IsLink(directory) || IsLink(path)) throw new IOException("The Appearance Data Path Is A Link.");
        Directory.CreateDirectory(directory);
        if (IsLink(directory))
            throw new IOException("The Appearance Data Folder Is A Link.");
        string temporary = Path.Combine(directory, $".appearance-{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                version = Version, theme = Id(options.Theme), accent = Id(options.Accent), finish = Id(options.Finish)
            });
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            created = false;
        }
        finally
        {
            if (created)
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            }
        }
    }

    static bool IsLink(string name)
    {
        try { return (File.GetAttributes(name) & FileAttributes.ReparsePoint) != 0; }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
    }
}

// Native sRGB mappings from VaporGrid Revision 5 docs/tokens.json, pinned at
// 729fe1ef4492f16e4f94c6198135fad5b6ba8385. Inline diffs keep their own amber fill.
sealed record VaporGridPalette(Color Background, Color Surface, Color Raised, Color Selected, Color Foreground,
    Color Muted, Color Border, Color Accent, Color AccentForeground, Color SelectionIndicator, Color Focus,
    Color Action, Color ActionHover, Color ActionPressed, Color Success, Color Danger, Color Rim, Color Shade)
{
    static Color Rgb(int value) => Color.FromArgb(value | unchecked((int)0xFF000000));

    internal static VaporGridPalette Dark { get; } = new(
        Rgb(0x1F1F21), Rgb(0x202123), Rgb(0x222225), Rgb(0x28292B), Rgb(0xF1F1F3), Rgb(0xB7B7C1),
        Rgb(0x92929C), Rgb(0x76172A), Rgb(0xF1F1F3), Rgb(0xDC2749), Rgb(0xFF4D73), Rgb(0x383840),
        Rgb(0x41414A), Rgb(0x303038), Rgb(0x8FD4AD), Rgb(0xFFB1A8), Rgb(0x404047), Rgb(0x111113));
    internal static VaporGridPalette Light { get; } = new(
        Rgb(0xEDEDF0), Rgb(0xFFFFFF), Rgb(0xE4E4E9), Rgb(0xDDDDE4), Rgb(0x202023), Rgb(0x50505B),
        Rgb(0x676772), Rgb(0x76172A), Rgb(0xF1F1F3), Rgb(0x9E1534), Rgb(0xD61F45), Rgb(0xD0D0D7),
        Rgb(0xCACAD1), Rgb(0xC3C3CA), Rgb(0x236340), Rgb(0x9D2B1F), Rgb(0xFFFFFF), Rgb(0xC2C2CA));

    internal static (Color Selection, Color Focus) Cues(bool light, AppearanceAccent accent) => (light, accent) switch
    {
        (false, AppearanceAccent.Blue) => (Rgb(0x5FA8FF), Rgb(0xACD1FF)),
        (true, AppearanceAccent.Blue) => (Rgb(0x1558A6), Rgb(0x327AD1)),
        (false, AppearanceAccent.Teal) => (Rgb(0x37B7AF), Rgb(0x8FE4D9)),
        (true, AppearanceAccent.Teal) => (Rgb(0x00665F), Rgb(0x13877F)),
        (true, _) => (Light.SelectionIndicator, Light.Focus),
        _ => (Dark.SelectionIndicator, Dark.Focus)
    };
}
