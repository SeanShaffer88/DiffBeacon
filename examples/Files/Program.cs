using DiffBeacon.Core;
var root = Path.Combine(Path.GetTempPath(), "DiffBeacon-Files-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var left = Path.Combine(root, "left"); var right = Path.Combine(root, "right");
    Directory.CreateDirectory(left); Directory.CreateDirectory(right);
    var a = Path.Combine(left, "sample.txt"); var b = Path.Combine(right, "sample.txt");
    File.WriteAllText(a, "before"); File.WriteAllText(b, "after");
    if (BinaryComparison.Compare(a, b).DifferentBytes == 0 || !FolderComparison.Compare(left, right).Complete) throw new Exception("Comparison failed");
    var document = TextDocument.Load(a);
    SafeFiles.Write(a, document.Encode("saved"), document.Stamp, allowOverwrite: true);
    if (File.ReadAllText(a) != "saved" || Directory.GetFiles(left, "*.diffbeacon-backup-*").Length != 1) throw new Exception("Save failed");
    Console.WriteLine("Safe save produced a backup in the unique temporary example directory.");
}
finally { Directory.Delete(root, recursive: true); }
