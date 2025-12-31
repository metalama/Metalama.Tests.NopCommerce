using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var roslynVersion = typeof(SyntaxNode).Assembly.GetName().Version!.ToString();
Console.WriteLine($"Roslyn Version: {roslynVersion}");

var config = DefaultConfig.Instance
    .AddJob(Job.Default
        .WithId($"Roslyn_{roslynVersion.Replace('.', '_')}")
        .WithIterationCount(5)
        .WithWarmupCount(1));

BenchmarkRunner.Run<NopCommerceBenchmarks>(config, args);

[MemoryDiagnoser]
public class NopCommerceBenchmarks
{
    // Use absolute path since BenchmarkDotNet runs in a subprocess with different working directory
    private static readonly string SourceRoot = @"C:\src\Metalama-2026.0\Metalama.Tests.NopCommerce\src";

    private Compilation _compilation = null!;
    private List<(SyntaxTree Tree, SemanticModel Model)> _allTreesWithModels = null!;
    private List<SyntaxTree> _allTrees = null!;
    private int _totalNodes;

    [GlobalSetup]
    public void GlobalSetup()
    {
        Console.WriteLine($"Source root: {SourceRoot}");

        // Find all .cs files in Nop.Core and Nop.Services
        var sourceFiles = new List<string>();
        var coreDir = Path.Combine(SourceRoot, "Libraries", "Nop.Core");
        var servicesDir = Path.Combine(SourceRoot, "Libraries", "Nop.Services");

        if (Directory.Exists(coreDir))
            sourceFiles.AddRange(Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories));
        if (Directory.Exists(servicesDir))
            sourceFiles.AddRange(Directory.GetFiles(servicesDir, "*.cs", SearchOption.AllDirectories));

        Console.WriteLine($"Found {sourceFiles.Count} source files");

        // Parse all files
        _allTrees = new List<SyntaxTree>();
        foreach (var file in sourceFiles)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            _allTrees.Add(tree);
        }

        Console.WriteLine($"Parsed {_allTrees.Count} syntax trees");

        // Get standard references
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        // Add netstandard and runtime references
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var dll in Directory.GetFiles(assemblyPath, "*.dll"))
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
            catch { /* Skip invalid assemblies */ }
        }

        Console.WriteLine($"Added {references.Count} references");

        // Create compilation
        _compilation = CSharpCompilation.Create(
            "NopCommerce.Test",
            _allTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Console.WriteLine($"Created compilation with {_compilation.SyntaxTrees.Count()} trees");

        // Preload ALL semantic models
        _allTreesWithModels = new List<(SyntaxTree, SemanticModel)>();
        _totalNodes = 0;
        foreach (var tree in _allTrees)
        {
            _allTreesWithModels.Add((tree, _compilation.GetSemanticModel(tree)));
            _totalNodes += tree.GetRoot().DescendantNodes().Count();
        }

        Console.WriteLine($"Preloaded {_allTreesWithModels.Count} semantic models, {_totalNodes} total nodes");
    }

    /// <summary>
    /// Simulates what TemplatingCodeValidator does - GetSymbolInfo on every node (ALL files)
    /// </summary>
    [Benchmark]
    public int ValidatorWalk_AllFiles()
    {
        int count = 0;

        foreach (var (tree, model) in _allTreesWithModels)
        {
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                var symbolInfo = model.GetSymbolInfo(node);
                if (symbolInfo.Symbol != null)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Optimized version - only identifiers (ALL files)
    /// </summary>
    [Benchmark]
    public int ValidatorWalk_IdentifiersOnly_AllFiles()
    {
        int count = 0;

        foreach (var (tree, model) in _allTreesWithModels)
        {
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbolInfo = model.GetSymbolInfo(node);
                if (symbolInfo.Symbol != null)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// GetDeclaredSymbol for all declarations (ALL files)
    /// </summary>
    [Benchmark]
    public int GetDeclaredSymbols_AllFiles()
    {
        int count = 0;

        foreach (var (tree, model) in _allTreesWithModels)
        {
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var symbol = model.GetDeclaredSymbol(node);
                if (symbol != null)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Simulates SafeSyntaxWalker - pure syntax walk without semantics (ALL files)
    /// </summary>
    [Benchmark]
    public int SyntaxWalk_AllFiles()
    {
        int count = 0;
        var walker = new CountingWalker();

        foreach (var tree in _allTrees)
        {
            walker.Reset();
            walker.Visit(tree.GetRoot());
            count += walker.Count;
        }

        return count;
    }

    private class CountingWalker : CSharpSyntaxWalker
    {
        public int Count { get; private set; }

        public void Reset() => Count = 0;

        public override void DefaultVisit(SyntaxNode node)
        {
            Count++;
            base.DefaultVisit(node);
        }
    }

    /// <summary>
    /// GetMembers pattern used by LinkerLinkingStep (ALL files)
    /// </summary>
    [Benchmark]
    public int GetMembers_Pattern_AllFiles()
    {
        int count = 0;

        foreach (var (tree, model) in _allTreesWithModels)
        {
            var root = tree.GetRoot();
            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbol = model.GetDeclaredSymbol(typeDecl);
                if (symbol is INamedTypeSymbol namedType)
                {
                    foreach (var member in namedType.GetMembers())
                    {
                        // Simulate GetBackingFieldName pattern
                        var fieldName = "_" + member.Name;
                        if (namedType.GetMembers(fieldName).Any())
                            count++;
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// SyntaxRewriter pattern (no-op) (ALL files)
    /// </summary>
    [Benchmark]
    public int SyntaxRewriter_NoOp_AllFiles()
    {
        int count = 0;
        var rewriter = new NoOpRewriter();

        foreach (var tree in _allTrees)
        {
            rewriter.Visit(tree.GetRoot());
            count++;
        }

        return count;
    }

    private class NoOpRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            return base.Visit(node);
        }
    }

    /// <summary>
    /// SyntaxRewriter with semantic checks (like SafeSyntaxRewriter) (ALL files)
    /// </summary>
    [Benchmark]
    public int SyntaxRewriter_WithSemantics_AllFiles()
    {
        int count = 0;

        foreach (var (tree, model) in _allTreesWithModels)
        {
            var rewriter = new SemanticRewriter(model);
            rewriter.Visit(tree.GetRoot());
            count++;
        }

        return count;
    }

    private class SemanticRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;

        public SemanticRewriter(SemanticModel model)
        {
            _model = model;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = _model.GetSymbolInfo(node).Symbol;
            // Just check the symbol but don't modify
            return base.VisitIdentifierName(node);
        }
    }

    /// <summary>
    /// TypeKind access pattern (from SymbolClassifier) (ALL files)
    /// </summary>
    [Benchmark]
    public int TypeKindAccess_Pattern_AllFiles()
    {
        int count = 0;

        foreach (var (tree, model) in _allTreesWithModels)
        {
            var root = tree.GetRoot();
            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbol = model.GetDeclaredSymbol(typeDecl);
                if (symbol is INamedTypeSymbol namedType)
                {
                    var baseType = namedType.BaseType;
                    while (baseType != null)
                    {
                        if (baseType.TypeKind == TypeKind.Class)
                            count++;
                        baseType = baseType.BaseType;
                    }
                }
            }
        }

        return count;
    }
}
