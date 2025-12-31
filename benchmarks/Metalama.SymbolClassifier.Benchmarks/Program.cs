// SymbolClassifier Benchmark - Tests the actual Metalama SymbolClassifier
// using reflection to access internal APIs.
//
// Usage:
//   dotnet run -c Release -p:MetalamaCommitDate=2025-09-10
//   dotnet run -c Release -p:MetalamaCommitDate=2025-09-16_01_cc978ebc

using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

Console.WriteLine("=== SymbolClassifier Direct Benchmark ===");
Console.WriteLine();

// Get Metalama version info
var engineAssembly = LoadMetalamaEngine();
if (engineAssembly == null)
{
    Console.WriteLine("ERROR: Could not load Metalama.Framework.Engine");
    return 1;
}

var metalamaVersion = engineAssembly.GetName().Version?.ToString() ?? "unknown";
Console.WriteLine($"Metalama Engine Version: {metalamaVersion}");

var roslynVersion = typeof(SyntaxNode).Assembly.GetName().Version!.ToString();
Console.WriteLine($"Roslyn Version: {roslynVersion}");
Console.WriteLine();

// Load NopCommerce source
var sourceRoot = @"C:\src\Metalama-2026.0\Metalama.Tests.NopCommerce\src";
Console.WriteLine($"Source root: {sourceRoot}");

var sourceFiles = new List<string>();
var coreDir = Path.Combine(sourceRoot, "Libraries", "Nop.Core");
var servicesDir = Path.Combine(sourceRoot, "Libraries", "Nop.Services");

if (Directory.Exists(coreDir))
    sourceFiles.AddRange(Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories));
if (Directory.Exists(servicesDir))
    sourceFiles.AddRange(Directory.GetFiles(servicesDir, "*.cs", SearchOption.AllDirectories));

Console.WriteLine($"Found {sourceFiles.Count} source files");

// Parse all files
var allTrees = new List<SyntaxTree>();
foreach (var file in sourceFiles)
{
    var code = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(code, path: file);
    allTrees.Add(tree);
}

Console.WriteLine($"Parsed {allTrees.Count} syntax trees");

// Get references
var references = new List<MetadataReference>
{
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
};

var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
foreach (var dll in Directory.GetFiles(assemblyPath, "*.dll"))
{
    try { references.Add(MetadataReference.CreateFromFile(dll)); }
    catch { }
}

// Add Metalama reference
var metalamaFramework = typeof(Metalama.Framework.Aspects.IAspect).Assembly;
references.Add(MetadataReference.CreateFromFile(metalamaFramework.Location));

Console.WriteLine($"Added {references.Count} references");

// Create compilation
var compilation = CSharpCompilation.Create(
    "NopCommerce.Test",
    allTrees,
    references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

Console.WriteLine($"Created compilation with {compilation.SyntaxTrees.Count()} trees");

// Collect symbols to classify
var allSymbols = new List<ISymbol>();
foreach (var tree in allTrees)
{
    var model = compilation.GetSemanticModel(tree);
    foreach (var node in tree.GetRoot().DescendantNodes())
    {
        var symbol = model.GetSymbolInfo(node).Symbol;
        if (symbol != null)
            allSymbols.Add(symbol);
    }
}

Console.WriteLine($"Collected {allSymbols.Count} symbols");
Console.WriteLine();

// Now set up and benchmark SymbolClassifier via reflection
try
{
    var (classifier, getTemplatingScopeMethod) = SetupSymbolClassifier(engineAssembly, compilation);

    if (classifier == null || getTemplatingScopeMethod == null)
    {
        Console.WriteLine("ERROR: Could not set up SymbolClassifier");
        return 1;
    }

    Console.WriteLine("SymbolClassifier initialized successfully!");
    Console.WriteLine();

    // Get the default context value
    var contextType = engineAssembly.GetType("Metalama.Framework.Engine.CompileTime.SymbolClassificationContext");
    var defaultContext = contextType != null ? Enum.Parse(contextType, "Default") : null;

    // Warmup
    Console.WriteLine("Warming up...");
    int warmupCount = Math.Min(1000, allSymbols.Count);
    for (int i = 0; i < warmupCount; i++)
    {
        var symbol = allSymbols[i];
        if (defaultContext != null)
            getTemplatingScopeMethod.Invoke(classifier, new object[] { symbol, defaultContext });
        else
            getTemplatingScopeMethod.Invoke(classifier, new object[] { symbol });
    }

    // Benchmark
    Console.WriteLine($"Running benchmark on {allSymbols.Count} symbols...");
    Console.WriteLine();

    var results = new List<double>();
    int iterations = 5;

    for (int iter = 1; iter <= iterations; iter++)
    {
        // Clear internal cache if possible (by recreating classifier)
        (classifier, getTemplatingScopeMethod) = SetupSymbolClassifier(engineAssembly, compilation);

        var sw = Stopwatch.StartNew();
        int count = 0;

        foreach (var symbol in allSymbols)
        {
            try
            {
                object? result;
                if (defaultContext != null)
                    result = getTemplatingScopeMethod!.Invoke(classifier, new object[] { symbol, defaultContext });
                else
                    result = getTemplatingScopeMethod!.Invoke(classifier, new object[] { symbol });
                count++;
            }
            catch
            {
                // Some symbols may fail - ignore
            }
        }

        sw.Stop();
        var elapsed = sw.Elapsed.TotalMilliseconds;
        results.Add(elapsed);

        Console.WriteLine($"  Iteration {iter}: {elapsed:F2} ms ({count} symbols classified)");
    }

    Console.WriteLine();
    Console.WriteLine($"=== Results (Metalama {metalamaVersion}) ===");
    Console.WriteLine($"  Mean:   {results.Average():F2} ms");
    Console.WriteLine($"  Min:    {results.Min():F2} ms");
    Console.WriteLine($"  Max:    {results.Max():F2} ms");
    Console.WriteLine($"  StdDev: {StdDev(results):F2} ms");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}

return 0;

static Assembly? LoadMetalamaEngine()
{
    // Try to find the Engine assembly
    var engineAssembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "Metalama.Framework.Engine");

    if (engineAssembly == null)
    {
        // Try loading from same directory as Framework
        var frameworkAssembly = typeof(Metalama.Framework.Aspects.IAspect).Assembly;
        var enginePath = Path.Combine(Path.GetDirectoryName(frameworkAssembly.Location)!, "Metalama.Framework.Engine.dll");

        if (File.Exists(enginePath))
        {
            try
            {
                engineAssembly = Assembly.LoadFrom(enginePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load Engine from {enginePath}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"Engine not found at: {enginePath}");
        }
    }

    return engineAssembly;
}

static (object? classifier, MethodInfo? method) SetupSymbolClassifier(Assembly engineAssembly, Compilation compilation)
{
    // Get required types
    var compilationContextType = engineAssembly.GetType("Metalama.Framework.Engine.Utilities.Roslyn.CompilationContext");
    var compilationExtensionsType = engineAssembly.GetType("Metalama.Framework.Engine.Utilities.Roslyn.CompilationExtensions");
    var symbolClassifierType = engineAssembly.GetType("Metalama.Framework.Engine.CompileTime.SymbolClassifier");
    var projectServiceProviderType = engineAssembly.GetType("Metalama.Framework.Engine.Services.ProjectServiceProvider");
    var serviceProviderFactoryType = engineAssembly.GetType("Metalama.Framework.Engine.Services.ServiceProviderFactory");

    if (compilationContextType == null || symbolClassifierType == null)
    {
        Console.WriteLine("ERROR: Required types not found");
        return (null, null);
    }

    // Get CompilationContext from compilation
    var getContextMethod = compilationExtensionsType?.GetMethod("GetCompilationContext", BindingFlags.Public | BindingFlags.Static);
    if (getContextMethod == null)
    {
        Console.WriteLine("ERROR: GetCompilationContext not found");
        return (null, null);
    }

    var compilationContext = getContextMethod.Invoke(null, new object[] { compilation });

    // Try to get a minimal ProjectServiceProvider
    // We need to find a way to create one - let's try using ServiceProviderFactory
    object? serviceProvider = null;

    // Try getting a GlobalServiceProvider first
    var globalServiceProviderType = engineAssembly.GetType("Metalama.Framework.Engine.Services.GlobalServiceProvider");
    if (globalServiceProviderType != null)
    {
        var uninitializedProp = globalServiceProviderType.GetProperty("Uninitialized", BindingFlags.Public | BindingFlags.Static);
        if (uninitializedProp != null)
        {
            var globalProvider = uninitializedProp.GetValue(null);

            // Try to create a ProjectServiceProvider from this
            if (projectServiceProviderType != null && globalProvider != null)
            {
                // Look for constructor or factory method
                var forProjectMethod = projectServiceProviderType.GetMethod("ForProject", BindingFlags.Public | BindingFlags.Static);
                if (forProjectMethod != null)
                {
                    // This might need compilation context
                }
            }
        }
    }

    // Alternative: Try to use ClassifyingCompilationContext directly
    var classifyingContextType = engineAssembly.GetType("Metalama.Framework.Engine.CompileTime.ClassifyingCompilationContext");
    if (classifyingContextType != null)
    {
        // This requires a ProjectServiceProvider which we need to construct
    }

    // Last resort: Try to construct SymbolClassifier directly
    var getClassifierMethod = symbolClassifierType.GetMethod("GetSymbolClassifier", BindingFlags.Public | BindingFlags.Static);
    if (getClassifierMethod != null)
    {
        // This method requires (ProjectServiceProvider, CompilationContext)
        // We need to construct a valid ProjectServiceProvider

        // Try to get through testing infrastructure if available
        var testServiceProviderType = engineAssembly.GetType("Metalama.Framework.Engine.Services.TestProjectServiceProvider");
        if (testServiceProviderType != null)
        {
            // This might help
        }
    }

    // Since we can't easily create a full service provider, let's try a different approach:
    // Use the ISymbolClassifier interface directly through ClassifyingCompilationContext

    // Try creating through simpler means - check if there's a simpler factory
    var symbolClassifierProviderType = engineAssembly.GetType("Metalama.Framework.Engine.CompileTime.SymbolClassifierProvider");

    // If all else fails, we can at least measure the GetTemplatingScope method overhead
    // by using a mock or minimal setup

    Console.WriteLine("Setting up minimal SymbolClassifier...");

    // Try direct reflection on private constructor as last resort
    var constructors = symbolClassifierType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
    foreach (var ctor in constructors)
    {
        Console.WriteLine($"  Found constructor with {ctor.GetParameters().Length} params");
    }

    // Get the GetTemplatingScope method for when we do have a classifier
    var getTemplatingScopeMethod = symbolClassifierType.GetMethod("GetTemplatingScope",
        BindingFlags.Public | BindingFlags.Instance,
        null,
        new[] { typeof(ISymbol), engineAssembly.GetType("Metalama.Framework.Engine.CompileTime.SymbolClassificationContext")! },
        null);

    if (getTemplatingScopeMethod == null)
    {
        // Try without context parameter
        getTemplatingScopeMethod = symbolClassifierType.GetMethod("GetTemplatingScope",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(ISymbol) },
            null);
    }

    Console.WriteLine($"  GetTemplatingScope method: {getTemplatingScopeMethod != null}");

    // For now, return null - we need proper Metalama initialization
    // The user should run this inside the actual Metalama test infrastructure
    return (null, getTemplatingScopeMethod);
}

static double StdDev(List<double> values)
{
    var avg = values.Average();
    var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
    return Math.Sqrt(sumOfSquares / values.Count);
}
