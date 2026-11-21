using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args is not [var root, var projectItems, var citedPathsFile]) return 2;
using var document = JsonDocument.Parse(File.ReadAllText(projectItems));
var items = document.RootElement.GetProperty("Items");
var sources = items.GetProperty("Compile").EnumerateArray()
    .Select(item => item.GetProperty("FullPath").GetString()!).Distinct(StringComparer.OrdinalIgnoreCase)
    .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
    .ToArray();
var references = items.GetProperty("ReferencePath").EnumerateArray()
    .Select(item => item.GetProperty("FullPath").GetString()!).Distinct(StringComparer.OrdinalIgnoreCase)
    .Select(path => MetadataReference.CreateFromFile(path));
var compilation = CSharpCompilation.Create("SignalAmpEvidence", sources, references,
    new CSharpCompilationOptions(OutputKind.ConsoleApplication));
var errors = compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
if (errors.Length != 0)
{
    foreach (var error in errors.Take(10)) Console.Error.WriteLine(error);
    return 3;
}

var byPath = sources.ToDictionary(tree => Relative(tree.FilePath), StringComparer.OrdinalIgnoreCase);
var cited = JsonSerializer.Deserialize<string[]>(File.ReadAllText(citedPathsFile))!;
var missing = new List<string>();
var modules = new List<object>();
foreach (var path in cited.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
{
    if (!byPath.TryGetValue(path, out var tree)) { missing.Add(path); continue; }
    var semantic = compilation.GetSemanticModel(tree);
    var imports = new Dictionary<(string Name, string Target), object>();
    foreach (var node in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
    {
        var symbol = semantic.GetSymbolInfo(node).Symbol;
        var type = symbol switch
        {
            INamedTypeSymbol named => named,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol evt => evt.ContainingType,
            IAliasSymbol alias when alias.Target is INamedTypeSymbol named => named,
            _ => null
        };
        if (type is null) continue;
        var locations = type.OriginalDefinition.Locations.Where(location => location.IsInSource).ToArray();
        var specifier = type.OriginalDefinition.ToDisplayString();
        if (locations.Length == 0)
        {
            imports.TryAdd((specifier, ""), new { specifier, resolved = (string?)null, external = true });
            continue;
        }
        if (locations.Any(location => location.SourceTree == tree)) continue;
        foreach (var location in locations)
        {
            var resolved = Relative(location.SourceTree!.FilePath);
            imports.TryAdd((specifier, resolved), new { specifier, resolved, external = false });
        }
    }
    modules.Add(new { path, imports = imports.OrderBy(pair => pair.Key).Select(pair => pair.Value), dynamic = Array.Empty<object>(), limitations = Array.Empty<object>() });
}
Console.WriteLine(JsonSerializer.Serialize(new { language = "csharp", modules, unreadable = missing, dynamic = 0, limitations = Array.Empty<object>() }));
return 0;

string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
