using DiffBeacon.Core;
var csv = DelimitedText.Parse("name,value\n海,\"a,b\"");
var tsv = DelimitedText.Parse("name\tvalue", '\t');
var html = Reports.Html("<title>", "&summary", csv);
if (csv[1][1] != "a,b" || tsv[0].Length != 2 || !html.Contains("&lt;title&gt;") || !html.Contains("&amp;summary")) throw new Exception("Formats example failed");
Console.WriteLine("Parsed CSV/TSV and generated escaped HTML.");
