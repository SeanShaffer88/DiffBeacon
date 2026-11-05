using System.Net;

namespace DiffBeacon.Core;

public static class Reports
{
    public static string Html(string title, string summary, IEnumerable<IEnumerable<string?>> rows)
    {
        string E(string? x) => WebUtility.HtmlEncode(x ?? "");
        return "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>" + E(title) + "</title><style>body{font:14px Segoe UI;margin:32px;color:#183238}table{border-collapse:collapse;width:100%}td{padding:6px;border:1px solid #ccd8da;white-space:pre-wrap}tr:nth-child(even){background:#f3f7f7}</style><h1>" + E(title) + "</h1><p>" + E(summary) + "</p><table>" + string.Concat(rows.Select(r => "<tr>" + string.Concat(r.Select(c => "<td>" + E(c) + "</td>")) + "</tr>")) + "</table></html>";
    }
}
