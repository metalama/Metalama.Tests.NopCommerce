// Cache Pattern Benchmark - Directly compares the two SymbolClassifier cache patterns
// OLD (fast): array[options].TryGetValue(symbol)
// NEW (slow): dict.TryGetValue(new CacheKey(symbol, options))

using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

[MemoryDiagnoser]
public class CachePatternBenchmarks
{
    private List<ISymbol> _symbols = null!;

    // OLD pattern: array of dictionaries
    private ConcurrentDictionary<ISymbol, int>[] _oldCache = null!;

    // NEW pattern: single dictionary with composite key
    private ConcurrentDictionary<CacheKey, int> _newCache = null!;

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        private readonly ISymbol _symbol;
        private readonly int _options;

        public CacheKey(ISymbol symbol, int options)
        {
            _symbol = symbol;
            _options = options;
        }

        public bool Equals(CacheKey other) =>
            SymbolEqualityComparer.Default.Equals(_symbol, other._symbol) && _options == other._options;

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(SymbolEqualityComparer.Default.GetHashCode(_symbol), _options);
    }

    [Params(10000, 50000, 100000)]
    public int SymbolCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Create mock symbols using a compilation
        var trees = new List<Microsoft.CodeAnalysis.SyntaxTree>();
        var code = @"
namespace TestNS {
    public class TestClass {
        public int Field1;
        public string? Field2;
        public void Method1() { }
        public int Property1 { get; set; }
    }
}";

        // Generate enough code to get many symbols
        for (int i = 0; i < SymbolCount / 10; i++)
        {
            var modifiedCode = code.Replace("TestClass", $"TestClass{i}");
            trees.Add(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(modifiedCode));
        }

        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };

        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "Test", trees, refs,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        _symbols = new List<ISymbol>();
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
                if (symbol != null)
                    _symbols.Add(symbol);
            }
        }

        // Limit to requested count
        if (_symbols.Count > SymbolCount)
            _symbols = _symbols.Take(SymbolCount).ToList();

        Console.WriteLine($"Using {_symbols.Count} symbols for benchmark");

        // Initialize caches
        _oldCache = new ConcurrentDictionary<ISymbol, int>[8];
        for (int i = 0; i < 8; i++)
            _oldCache[i] = new ConcurrentDictionary<ISymbol, int>(SymbolEqualityComparer.Default);

        _newCache = new ConcurrentDictionary<CacheKey, int>();

        // Pre-populate caches with half the symbols (simulating cache hits)
        var halfSymbols = _symbols.Take(_symbols.Count / 2);
        foreach (var symbol in halfSymbols)
        {
            _oldCache[0][symbol] = 1;
            _newCache[new CacheKey(symbol, 0)] = 1;
        }
    }

    /// <summary>
    /// OLD pattern: array[(int)options].TryGetValue(symbol)
    /// </summary>
    [Benchmark(Baseline = true)]
    public int OldPattern_ArrayOfDictionaries()
    {
        int count = 0;
        var options = 0;
        var cache = _oldCache[options];

        foreach (var symbol in _symbols)
        {
            if (cache.TryGetValue(symbol, out var value))
                count += value;
            else
                cache.TryAdd(symbol, 1);
        }

        return count;
    }

    /// <summary>
    /// NEW pattern: dict.TryGetValue(new CacheKey(symbol, options))
    /// </summary>
    [Benchmark]
    public int NewPattern_CompositeKey()
    {
        int count = 0;
        var options = 0;

        foreach (var symbol in _symbols)
        {
            var key = new CacheKey(symbol, options);
            if (_newCache.TryGetValue(key, out var value))
                count += value;
            else
                _newCache.TryAdd(key, 1);
        }

        return count;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Reset caches for next iteration
        foreach (var cache in _oldCache)
            cache.Clear();
        _newCache.Clear();
    }
}
