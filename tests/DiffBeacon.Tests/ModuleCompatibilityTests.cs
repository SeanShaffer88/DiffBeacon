using System.Reflection;
using DiffBeacon.Core;

static class ModuleCompatibilityTests
{
    public static void Run(Action<string, Action> test)
    {
        test("Extracted public API matches pre-extraction surface", () =>
        {
            var facade = Assembly.Load("DiffBeacon.Core");
            var types = facade.GetForwardedTypes();
            var actual = types.SelectMany(type => new[] { "T " + type.FullName }.Concat(
                type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(member => "M " + type.FullName + " " + member.MemberType + " " + member)))
                .Order(StringComparer.Ordinal).ToArray();
            var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "public-api.txt")).Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected)) throw new Exception("Public API changed: " + string.Join("\n", expected.Except(actual).Concat(actual.Except(expected))));
        });
        test("Core forwards single type identities to all four modules", () =>
        {
            var facade = Assembly.Load("DiffBeacon.Core");
            if (facade.GetExportedTypes().Length != 0) throw new Exception("Facade defines duplicate public types");
            foreach (var (type, module) in new[] {
                (typeof(TextComparison), "Text"), (typeof(TextDocument), "Files"),
                (typeof(DelimitedText), "Formats"), (typeof(Session), "Sessions") })
            {
                var resolved = Type.GetType(type.FullName + ", DiffBeacon.Core", throwOnError: true);
                if (resolved != type || type.Assembly.GetName().Name != "DiffBeacon." + module)
                    throw new Exception("Forwarding identity mismatch: " + type);
            }
        });
    }
}
