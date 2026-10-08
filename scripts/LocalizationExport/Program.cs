using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
var root = Path.GetFullPath(args[0]);
var rows = new SortedDictionary<string, (HashSet<string> Locations, HashSet<string> Notes)>();
void Add(string text, string location, string note = "")
{
    if (!rows.TryGetValue(text, out var entry)) entry = ([], []);
    entry.Locations.Add(location); if (note.Length > 0) entry.Notes.Add(note); rows[text] = entry;
}
foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
{
    if (file.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" || p.EndsWith(".Tests"))) continue;
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
    foreach (var node in tree.GetRoot().DescendantNodes())
    {
        string text; string note = "";
        if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) text = literal.Token.ValueText;
        else if (node is InterpolatedStringExpressionSyntax interpolation)
        {
            var builder = new StringBuilder(); var parameters = new List<string>();
            foreach (var part in interpolation.Contents)
            {
                if (part is InterpolatedStringTextSyntax chunk) builder.Append(chunk.TextToken.ValueText);
                else if (part is InterpolationSyntax argument)
                {
                    builder.Append('{').Append(parameters.Count).Append(argument.AlignmentClause?.ToString()).Append(argument.FormatClause?.ToString()).Append('}');
                    parameters.Add("{" + parameters.Count + "}=" + argument.Expression.ToString());
                }
            }
            text = builder.ToString(); note = "保留占位符：" + string.Join("；", parameters);
        }
        else continue;
        if (!Regex.IsMatch(text, @"[\p{IsCJKUnifiedIdeographs}]")) continue;
        if (node.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => i.Expression.ToString().StartsWith("Log.") || i.Expression.ToString().StartsWith("Console."))) continue;
        if (node.Ancestors().OfType<InterpolatedStringExpressionSyntax>().Any()) continue;
        if (file.EndsWith("DiagnosticLog.cs")) continue;
        var relative = Path.GetRelativePath(root, file); var line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
        Add(text, relative + ":" + line, note);
    }
}
foreach (Match item in Regex.Matches(File.ReadAllText(Path.Combine(root, "packaging/webview/toolbar.html")), @">([^<>]+)</button>"))
    Add(item.Groups[1].Value, "官方页面窗口工具栏");
Add("FFXIV Super Launcher", "主标题及窗口标题", "产品名，建议保留原文");
Add("Dalamud", "关于及插件设置", "组件名，建议保留原文");
var result = rows.Select(r => new { id = "text_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(r.Key)))[..12].ToLowerInvariant(),
    context = string.Join("\n", r.Value.Locations), zhCN = r.Key, zhTW = "", en = "", ja = "", notes = string.Join("\n", r.Value.Notes) });
File.WriteAllText(args[1], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Exported {rows.Count} unique text entries.");
