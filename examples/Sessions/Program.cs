using DiffBeacon.Core;
var root = Path.Combine(Path.GetTempPath(), "DiffBeacon-Sessions-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var path = Path.Combine(root, "session.json");
    var session = new Session(3, "Example", "Text", "left.txt", "right.txt", new(), Granularity: HighlightGranularity.Character);
    session.Save(path);
    if (Session.Load(path) != session) throw new Exception("Session round trip failed");
    Console.WriteLine("Version 3 session round trip passed.");
}
finally { Directory.Delete(root, recursive: true); }
