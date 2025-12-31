using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Get Roslyn version for display
var roslynVersion = typeof(CSharpCompilation).Assembly.GetName().Version;
Console.WriteLine($"Roslyn Version: {roslynVersion}");

BenchmarkRunner.Run<RoslynApiBenchmarks>();

[Config(typeof(Config))]
[MemoryDiagnoser]
public class RoslynApiBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            var roslynVersion = typeof(CSharpCompilation).Assembly.GetName().Version;

            AddJob(Job.Default
                .WithId($"Roslyn_{roslynVersion?.Major}.{roslynVersion?.Minor}.{roslynVersion?.Build}")
                .WithWarmupCount(2)
                .WithIterationCount(10));

            ArtifactsPath = GetArtifactsPath();
        }

        private static string GetArtifactsPath()
        {
            var baseDir = Path.Combine(FindRepoRoot(), "benchmarks", "roslyn-api-results");
            Directory.CreateDirectory(baseDir);

            var roslynVersion = typeof(CSharpCompilation).Assembly.GetName().Version;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm");
            var artifactsPath = Path.Combine(baseDir, $"{timestamp}-roslyn-{roslynVersion?.Major}.{roslynVersion?.Minor}");
            Directory.CreateDirectory(artifactsPath);
            return artifactsPath;
        }

        private static string FindRepoRoot()
        {
            var dir = Environment.CurrentDirectory;
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return Environment.CurrentDirectory;
        }
    }

    // Test code samples of varying complexity
    private const string SmallCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        private int _field;

        public int Property { get; set; }

        public void Method(int x)
        {
            var y = x + _field;
            Console.WriteLine(y);
        }
    }
}";

    private const string MediumCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    public interface IService
    {
        void Execute();
    }

    public class ServiceImpl : IService
    {
        private readonly List<int> _items = new();
        private readonly Dictionary<string, object> _cache = new();

        public string Name { get; set; } = string.Empty;
        public int Count => _items.Count;

        public void Execute()
        {
            foreach (var item in _items)
            {
                ProcessItem(item);
            }
        }

        private void ProcessItem(int item)
        {
            var result = item * 2;
            if (result > 100)
            {
                _cache[result.ToString()] = new object();
            }
        }

        public IEnumerable<int> GetFiltered(Func<int, bool> predicate)
        {
            return _items.Where(predicate).Select(x => x * 2);
        }
    }

    public class Consumer
    {
        private readonly IService _service;

        public Consumer(IService service)
        {
            _service = service;
        }

        public void Run()
        {
            _service.Execute();
        }
    }
}";

    private const string LargeCode = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace TestNamespace.Models
{
    public record Person(string FirstName, string LastName, int Age);

    public class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}

namespace TestNamespace.Services
{
    using TestNamespace.Models;

    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(int id);
    }

    public interface IPersonService
    {
        Task<Person?> GetPersonAsync(int id);
        Task<IEnumerable<Person>> SearchAsync(string query);
        Task<Person> CreatePersonAsync(string firstName, string lastName, int age);
    }

    public class PersonService : IPersonService
    {
        private readonly IRepository<Person> _repository;
        private readonly Dictionary<int, Person> _cache = new();
        private readonly object _lock = new();

        public PersonService(IRepository<Person> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<Person?> GetPersonAsync(int id)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(id, out var cached))
                {
                    return cached;
                }
            }

            var person = await _repository.GetByIdAsync(id);

            if (person != null)
            {
                lock (_lock)
                {
                    _cache[id] = person;
                }
            }

            return person;
        }

        public async Task<IEnumerable<Person>> SearchAsync(string query)
        {
            var all = await _repository.GetAllAsync();

            return all.Where(p =>
                p.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.LastName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Person> CreatePersonAsync(string firstName, string lastName, int age)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException(""First name is required"", nameof(firstName));

            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException(""Last name is required"", nameof(lastName));

            if (age < 0 || age > 150)
                throw new ArgumentOutOfRangeException(nameof(age));

            var person = new Person(firstName.Trim(), lastName.Trim(), age);
            return await _repository.AddAsync(person);
        }

        private static string BuildFullName(Person person)
        {
            var sb = new StringBuilder();
            sb.Append(person.FirstName);
            sb.Append(' ');
            sb.Append(person.LastName);
            return sb.ToString();
        }
    }

    public static class Extensions
    {
        public static string GetFullName(this Person person)
        {
            return $""{person.FirstName} {person.LastName}"";
        }

        public static bool IsAdult(this Person person)
        {
            return person.Age >= 18;
        }

        public static IEnumerable<Person> Adults(this IEnumerable<Person> people)
        {
            return people.Where(p => p.IsAdult());
        }
    }
}
";

    private Compilation _smallCompilation = null!;
    private Compilation _mediumCompilation = null!;
    private Compilation _largeCompilation = null!;

    private SemanticModel _smallSemanticModel = null!;
    private SemanticModel _mediumSemanticModel = null!;
    private SemanticModel _largeSemanticModel = null!;

    private SyntaxTree _smallTree = null!;
    private SyntaxTree _mediumTree = null!;
    private SyntaxTree _largeTree = null!;

    private List<SyntaxNode> _smallAllNodes = null!;
    private List<SyntaxNode> _mediumAllNodes = null!;
    private List<SyntaxNode> _largeAllNodes = null!;

    private List<IdentifierNameSyntax> _smallIdentifiers = null!;
    private List<IdentifierNameSyntax> _mediumIdentifiers = null!;
    private List<IdentifierNameSyntax> _largeIdentifiers = null!;

    private List<MemberDeclarationSyntax> _smallDeclarations = null!;
    private List<MemberDeclarationSyntax> _mediumDeclarations = null!;
    private List<MemberDeclarationSyntax> _largeDeclarations = null!;

    [GlobalSetup]
    public void Setup()
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Collections.dll")),
        };

        // Small compilation
        _smallTree = CSharpSyntaxTree.ParseText(SmallCode);
        _smallCompilation = CSharpCompilation.Create("SmallTest", [_smallTree], references);
        _smallSemanticModel = _smallCompilation.GetSemanticModel(_smallTree);
        _smallAllNodes = _smallTree.GetRoot().DescendantNodesAndSelf().ToList();
        _smallIdentifiers = _smallTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().ToList();
        _smallDeclarations = _smallTree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>().ToList();

        // Medium compilation
        _mediumTree = CSharpSyntaxTree.ParseText(MediumCode);
        _mediumCompilation = CSharpCompilation.Create("MediumTest", [_mediumTree], references);
        _mediumSemanticModel = _mediumCompilation.GetSemanticModel(_mediumTree);
        _mediumAllNodes = _mediumTree.GetRoot().DescendantNodesAndSelf().ToList();
        _mediumIdentifiers = _mediumTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().ToList();
        _mediumDeclarations = _mediumTree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>().ToList();

        // Large compilation
        _largeTree = CSharpSyntaxTree.ParseText(LargeCode);
        _largeCompilation = CSharpCompilation.Create("LargeTest", [_largeTree], references);
        _largeSemanticModel = _largeCompilation.GetSemanticModel(_largeTree);
        _largeAllNodes = _largeTree.GetRoot().DescendantNodesAndSelf().ToList();
        _largeIdentifiers = _largeTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().ToList();
        _largeDeclarations = _largeTree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>().ToList();

        Console.WriteLine($"Small: {_smallAllNodes.Count} nodes, {_smallIdentifiers.Count} identifiers, {_smallDeclarations.Count} declarations");
        Console.WriteLine($"Medium: {_mediumAllNodes.Count} nodes, {_mediumIdentifiers.Count} identifiers, {_mediumDeclarations.Count} declarations");
        Console.WriteLine($"Large: {_largeAllNodes.Count} nodes, {_largeIdentifiers.Count} identifiers, {_largeDeclarations.Count} declarations");
    }

    // ============================================
    // GetSymbolInfo on ALL nodes (like TemplatingCodeValidator does)
    // ============================================

    [Benchmark]
    public int GetSymbolInfo_AllNodes_Small()
    {
        int count = 0;
        foreach (var node in _smallAllNodes)
        {
            var info = _smallSemanticModel.GetSymbolInfo(node);
            if (info.Symbol != null) count++;
        }
        return count;
    }

    [Benchmark]
    public int GetSymbolInfo_AllNodes_Medium()
    {
        int count = 0;
        foreach (var node in _mediumAllNodes)
        {
            var info = _mediumSemanticModel.GetSymbolInfo(node);
            if (info.Symbol != null) count++;
        }
        return count;
    }

    [Benchmark]
    public int GetSymbolInfo_AllNodes_Large()
    {
        int count = 0;
        foreach (var node in _largeAllNodes)
        {
            var info = _largeSemanticModel.GetSymbolInfo(node);
            if (info.Symbol != null) count++;
        }
        return count;
    }

    // ============================================
    // GetSymbolInfo on identifiers only (optimized approach)
    // ============================================

    [Benchmark]
    public int GetSymbolInfo_IdentifiersOnly_Small()
    {
        int count = 0;
        foreach (var node in _smallIdentifiers)
        {
            var info = _smallSemanticModel.GetSymbolInfo(node);
            if (info.Symbol != null) count++;
        }
        return count;
    }

    [Benchmark]
    public int GetSymbolInfo_IdentifiersOnly_Medium()
    {
        int count = 0;
        foreach (var node in _mediumIdentifiers)
        {
            var info = _mediumSemanticModel.GetSymbolInfo(node);
            if (info.Symbol != null) count++;
        }
        return count;
    }

    [Benchmark]
    public int GetSymbolInfo_IdentifiersOnly_Large()
    {
        int count = 0;
        foreach (var node in _largeIdentifiers)
        {
            var info = _largeSemanticModel.GetSymbolInfo(node);
            if (info.Symbol != null) count++;
        }
        return count;
    }

    // ============================================
    // GetDeclaredSymbol benchmarks
    // ============================================

    [Benchmark]
    public int GetDeclaredSymbol_Small()
    {
        int count = 0;
        foreach (var decl in _smallDeclarations)
        {
            var symbol = _smallSemanticModel.GetDeclaredSymbol(decl);
            if (symbol != null) count++;
        }
        return count;
    }

    [Benchmark]
    public int GetDeclaredSymbol_Medium()
    {
        int count = 0;
        foreach (var decl in _mediumDeclarations)
        {
            var symbol = _mediumSemanticModel.GetDeclaredSymbol(decl);
            if (symbol != null) count++;
        }
        return count;
    }

    [Benchmark]
    public int GetDeclaredSymbol_Large()
    {
        int count = 0;
        foreach (var decl in _largeDeclarations)
        {
            var symbol = _largeSemanticModel.GetDeclaredSymbol(decl);
            if (symbol != null) count++;
        }
        return count;
    }

    // ============================================
    // GetTypeInfo benchmarks (used for type checking)
    // ============================================

    [Benchmark]
    public int GetTypeInfo_AllNodes_Medium()
    {
        int count = 0;
        foreach (var node in _mediumAllNodes)
        {
            var info = _mediumSemanticModel.GetTypeInfo(node);
            if (info.Type != null) count++;
        }
        return count;
    }

    // ============================================
    // Combined: GetSymbolInfo + GetDeclaredSymbol (simulates validator walk)
    // ============================================

    [Benchmark]
    public int SimulateValidatorWalk_Medium()
    {
        int count = 0;
        foreach (var node in _mediumAllNodes)
        {
            if (node is MemberDeclarationSyntax decl)
            {
                var declSymbol = _mediumSemanticModel.GetDeclaredSymbol(decl);
                if (declSymbol != null) count++;
            }

            var symbolInfo = _mediumSemanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol != null) count++;
        }
        return count;
    }

    [Benchmark]
    public int SimulateValidatorWalk_Large()
    {
        int count = 0;
        foreach (var node in _largeAllNodes)
        {
            if (node is MemberDeclarationSyntax decl)
            {
                var declSymbol = _largeSemanticModel.GetDeclaredSymbol(decl);
                if (declSymbol != null) count++;
            }

            var symbolInfo = _largeSemanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol != null) count++;
        }
        return count;
    }

    // ============================================
    // Compilation creation (one-time cost but important)
    // ============================================

    [Benchmark]
    public Compilation CreateCompilation_Medium()
    {
        var tree = CSharpSyntaxTree.ParseText(MediumCode);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };
        return CSharpCompilation.Create("Test", [tree], references);
    }

    // ============================================
    // SemanticModel creation (per-file cost)
    // ============================================

    [Benchmark]
    public SemanticModel GetSemanticModel_Medium()
    {
        return _mediumCompilation.GetSemanticModel(_mediumTree);
    }

    // ============================================
    // GetMembers benchmarks (used heavily by Linker)
    // ============================================

    [Benchmark]
    public int GetMembers_AllMembers_Medium()
    {
        int count = 0;
        var root = _mediumTree.GetRoot();
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var symbol = _mediumSemanticModel.GetDeclaredSymbol(typeDecl);
            if (symbol != null)
            {
                foreach (var member in symbol.GetMembers())
                {
                    count++;
                }
            }
        }
        return count;
    }

    [Benchmark]
    public int GetMembers_ByName_Medium()
    {
        int count = 0;
        var root = _mediumTree.GetRoot();
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var symbol = _mediumSemanticModel.GetDeclaredSymbol(typeDecl);
            if (symbol != null)
            {
                // Simulate the Linker's pattern of checking for name collisions
                foreach (var member in symbol.GetMembers())
                {
                    // Check if a variant name exists (like Linker does for backing fields)
                    var nameToCheck = "_" + member.Name;
                    if (symbol.GetMembers(nameToCheck).Any())
                        count++;
                    if (symbol.GetMembers(member.Name + "_source").Any())
                        count++;
                }
            }
        }
        return count;
    }

    [Benchmark]
    public int GetMembers_ByName_Repeated_Medium()
    {
        int count = 0;
        var root = _mediumTree.GetRoot();
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var symbol = _mediumSemanticModel.GetDeclaredSymbol(typeDecl);
            if (symbol != null)
            {
                // Simulate the Linker's FindUniqueName pattern - repeated calls
                for (int i = 0; i < 10; i++)
                {
                    var hint = $"_field{i}";
                    if (!symbol.GetMembers(hint).Any())
                        count++;
                }
            }
        }
        return count;
    }

    // ============================================
    // CSharpSyntaxWalker benchmarks
    // ============================================

    [Benchmark]
    public int SyntaxWalker_Medium()
    {
        var walker = new CountingWalker();
        walker.Visit(_mediumTree.GetRoot());
        return walker.Count;
    }

    [Benchmark]
    public int SyntaxWalker_Large()
    {
        var walker = new CountingWalker();
        walker.Visit(_largeTree.GetRoot());
        return walker.Count;
    }

    [Benchmark]
    public int SyntaxWalkerWithSemantics_Medium()
    {
        var walker = new SemanticWalker(_mediumSemanticModel);
        walker.Visit(_mediumTree.GetRoot());
        return walker.Count;
    }

    [Benchmark]
    public int SyntaxWalkerWithSemantics_Large()
    {
        var walker = new SemanticWalker(_largeSemanticModel);
        walker.Visit(_largeTree.GetRoot());
        return walker.Count;
    }

    // ============================================
    // CSharpSyntaxRewriter benchmarks
    // ============================================

    [Benchmark]
    public SyntaxNode SyntaxRewriter_NoOp_Medium()
    {
        var rewriter = new NoOpRewriter();
        return rewriter.Visit(_mediumTree.GetRoot());
    }

    [Benchmark]
    public SyntaxNode SyntaxRewriter_NoOp_Large()
    {
        var rewriter = new NoOpRewriter();
        return rewriter.Visit(_largeTree.GetRoot());
    }

    [Benchmark]
    public SyntaxNode SyntaxRewriter_WithChanges_Medium()
    {
        var rewriter = new AddTriviaRewriter();
        return rewriter.Visit(_mediumTree.GetRoot());
    }

    [Benchmark]
    public SyntaxNode SyntaxRewriter_WithChanges_Large()
    {
        var rewriter = new AddTriviaRewriter();
        return rewriter.Visit(_largeTree.GetRoot());
    }

    [Benchmark]
    public SyntaxNode SyntaxRewriter_WithSemantics_Medium()
    {
        var rewriter = new SemanticRewriter(_mediumSemanticModel);
        return rewriter.Visit(_mediumTree.GetRoot());
    }

    [Benchmark]
    public SyntaxNode SyntaxRewriter_WithSemantics_Large()
    {
        var rewriter = new SemanticRewriter(_largeSemanticModel);
        return rewriter.Visit(_largeTree.GetRoot());
    }

    // ============================================
    // Helper classes for walker/rewriter benchmarks
    // ============================================

    private class CountingWalker : CSharpSyntaxWalker
    {
        public int Count { get; private set; }

        public CountingWalker() : base(SyntaxWalkerDepth.Node) { }

        public override void DefaultVisit(SyntaxNode node)
        {
            Count++;
            base.DefaultVisit(node);
        }
    }

    private class SemanticWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        public int Count { get; private set; }

        public SemanticWalker(SemanticModel semanticModel) : base(SyntaxWalkerDepth.Node)
        {
            _semanticModel = semanticModel;
        }

        public override void DefaultVisit(SyntaxNode node)
        {
            // Simulate what TemplatingCodeValidator does
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol != null)
                Count++;

            if (node is MemberDeclarationSyntax decl)
            {
                var declSymbol = _semanticModel.GetDeclaredSymbol(decl);
                if (declSymbol != null)
                    Count++;
            }

            base.DefaultVisit(node);
        }
    }

    private class NoOpRewriter : CSharpSyntaxRewriter
    {
        public NoOpRewriter() : base(visitIntoStructuredTrivia: false) { }

        public override SyntaxNode? DefaultVisit(SyntaxNode node)
        {
            return base.DefaultVisit(node);
        }
    }

    private class AddTriviaRewriter : CSharpSyntaxRewriter
    {
        private int _counter;

        public AddTriviaRewriter() : base(visitIntoStructuredTrivia: false) { }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            _counter++;
            // Add a comment before each method - simulates actual code transformation
            var comment = SyntaxFactory.Comment($"// Method {_counter}\n");
            var newNode = node.WithLeadingTrivia(node.GetLeadingTrivia().Add(comment));
            return base.VisitMethodDeclaration(newNode);
        }
    }

    private class SemanticRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        public int SymbolsFound { get; private set; }

        public SemanticRewriter(SemanticModel semanticModel) : base(visitIntoStructuredTrivia: false)
        {
            _semanticModel = semanticModel;
        }

        public override SyntaxNode? DefaultVisit(SyntaxNode node)
        {
            // Check semantic info during rewrite (like Metalama's template expander)
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol != null)
                SymbolsFound++;

            return base.DefaultVisit(node);
        }
    }
}
