# Roslyn API Benchmarks

Benchmarks to measure the performance of Roslyn's semantic model APIs across different versions.

## Purpose

This benchmark suite tests the performance of APIs that Metalama heavily uses:
- `SemanticModel.GetSymbolInfo()` - called frequently during code validation
- `SemanticModel.GetDeclaredSymbol()` - called for every declaration
- `SemanticModel.GetTypeInfo()` - called for type checking
- `Compilation.GetSemanticModel()` - per-file cost

## Running Benchmarks

### Quick test with single Roslyn version:

```powershell
cd benchmarks/Roslyn.Api.Benchmarks
dotnet run -c Release
```

### Test with specific Roslyn version:

```powershell
dotnet run -c Release -p:RoslynVersion=4.14.0
dotnet run -c Release -p:RoslynVersion=5.0.0
```

### Run all versions (using script):

```powershell
.\run-benchmarks.ps1
```

### Quick mode (medium tests only):

```powershell
.\run-benchmarks.ps1 -Quick
```

### Custom versions:

```powershell
.\run-benchmarks.ps1 -Versions "4.12.0","4.14.0","5.0.0"
```

## Comparing Results

After running benchmarks with multiple versions:

```powershell
python compare-results.py roslyn-api-results/<dir1> roslyn-api-results/<dir2>
```

## Benchmark Categories

### GetSymbolInfo_AllNodes_*
Simulates what `TemplatingCodeValidator.VisitCore` does - calling `GetSymbolInfo` on every syntax node.
This is the current behavior and may include unnecessary calls on literals, keywords, etc.

### GetSymbolInfo_IdentifiersOnly_*
Optimized approach - only calling `GetSymbolInfo` on identifier nodes that can actually reference symbols.

### GetDeclaredSymbol_*
Measures the cost of getting declared symbols for type/method/property declarations.

### SimulateValidatorWalk_*
Combined benchmark that simulates the full validator walk pattern:
1. For declarations: call `GetDeclaredSymbol`
2. For all nodes: call `GetSymbolInfo`

## Interpreting Results

Key metrics to watch:
- **Mean**: Average time per operation
- **Allocated**: Memory allocated per operation
- **Diff %**: Percentage change between versions (compare-results.py output)

A regression of >10% in `SimulateValidatorWalk_*` or `GetSymbolInfo_AllNodes_*` would explain
observed Metalama performance issues.

## Expected Results

If Roslyn 5.0 has significant API regressions, we'd expect to see:
- `GetSymbolInfo_AllNodes_*` slower
- `SimulateValidatorWalk_*` proportionally slower
- Memory allocation changes

If the issue is not in Roslyn API performance:
- Results should be similar across versions
- The performance issue is in how Metalama uses the APIs
