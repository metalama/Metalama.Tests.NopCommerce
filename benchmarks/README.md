# Metalama Build Overhead Benchmarks

Benchmarks measuring the build time overhead of Metalama compared to standard C# compilation, using NopCommerce as a real-world codebase.

## Overview

The benchmark measures `dotnet build` time with and without Metalama enabled, varying the density of aspects applied to the codebase. Results are used to fit a regression model predicting build overhead as a function of aspect coverage.

## Model

Build time overhead follows a bilinear model:

```
TimeRatio = β₀ + β₁×T + β₂×(T×M)
```

Where:
- **TimeRatio** = build time with Metalama / build time without
- **T** = fraction of types with aspects (0-1)
- **M** = fraction of methods within targeted types with aspects (0-1)
- **β₀** = base overhead (Metalama enabled, no aspects)
- **β₁** = coefficient for type coverage
- **β₂** = coefficient for method density within types

Typical R² > 99%, indicating the model explains nearly all variance.

## Running Benchmarks

### Prerequisites

- .NET 8.0 SDK
- BenchmarkDotNet (included in project)

### Build Configurations

The benchmark project supports three configurations via preprocessor directives:

| Configuration | Define | Description |
|---------------|--------|-------------|
| Default | (none) | 31-point matrix: T ∈ {0,1,5,10,20,50,100}%, M ∈ {0,10,30,60,100}% (T=0,M>0 skipped) |
| Typical | `TYPICAL` | Single point: T=10%, M=10% (realistic scenario) |
| All | `ALL` | Single point: T=100%, M=100% (max aspect density) |
| Regression | `REGRESSION` | Multi-version matrix comparing across Metalama versions |
| .NET SDK | `DOTNETSDK` | Compare .NET SDK versions (8.0.100, 9.0.100, 10.0.100), no Metalama |

### Running

```powershell
cd Metalama.NopCommerce.Benchmarks

# Default configuration (12 combinations)
dotnet run -c Release

# Typical (quick single-point test)
dotnet run -c Release -p:DefineConstants=TYPICAL

# Regression test (compare versions)
dotnet run -c Release -p:DefineConstants=REGRESSION

# .NET SDK comparison (no Metalama)
dotnet run -c Release -p:DefineConstants=DOTNETSDK
```

Results are saved to `benchmarks/results/YYYY-MM-DD-HH-mm/`.

## Analysis


### Basic Usage

```powershell
cd benchmarks

# Analyze a single run
python analyze_benchmarks.py results/16/results/Benchmark-report.csv

# Analyze multiple runs (merged)
python analyze_benchmarks.py "results/*/results/Benchmark-report.csv"

# Generate visualization plots
python analyze_benchmarks.py results/16/results/Benchmark-report.csv --plots

# Custom output directory
python analyze_benchmarks.py results/16/results/Benchmark-report.csv -o ./analysis
```

### Output

**Console output:**
```
================================================================================
METALAMA BUILD OVERHEAD REGRESSION ANALYSIS
================================================================================

Model: TimeRatio = β₀ + β₁×T + β₂×(T×M)
Where T = type%, M = method% (as fractions 0-1)

         Version  β₀ (Base)  β₀ ±95%CI  β₁ (Type)  β₁ ±95%CI  β₂ (Method)  β₂ ±95%CI     R²  Adj R²  Overhead@10%×10%  N
       2025.1.17     1.0716     0.0099    -0.0181     0.0282       0.2078     0.0452 0.8188  0.8059            1.0718 31
       2026.0.16     1.0610     0.0108    -0.0212     0.0308       0.2378     0.0492 0.8324  0.8204            1.0613 31
2026.1.1-preview     1.0580     0.0113    -0.0192     0.0320       0.2144     0.0513 0.7879  0.7728            1.0583 31


**Generated files:**
- `regression_results.csv` - Coefficients and R² per version
- `plot_2d_interaction.png` - TimeRatio vs T×M (with `--plots`)

### Interpreting Results

| Coefficient | Meaning |
|-------------|---------|
| β₀ (Base) | Overhead with Metalama enabled but 0% aspects. Values ~1.09 mean 9% slower. |
| β₁ (Type) | Additional overhead per 1% of types covered. Usually small. |
| β₂ (Method) | Additional overhead per 1% effective method coverage (T×M). |
| R² | Model fit quality. >0.95 indicates excellent fit. |
| Adj R² | Adjusted R², corrected for number of parameters. More reliable with few data points. |
| Overhead@10%×10% | Predicted overhead at typical aspect density (10% types, 10% methods). |

### Detecting Regressions

Compare β₀ across versions:
- **Constant regression**: β₀ increased significantly → pipeline initialization slower
- **Scaling regression**: β₂ increased → per-aspect processing slower
- **Both stable**: No performance regression

## Directory Structure

```
benchmarks/
├── README.md                           # This file
├── analyze_benchmarks.py               # Analysis script
├── results/                            # Benchmark outputs (gitignored)
│   ├── 2024-12-25-14-30/
│   │   └── results/
│   │       └── Benchmark-report.csv
│   └── ...
└── Metalama.NopCommerce.Benchmarks/    # Benchmark project
    ├── Program.cs
    └── README.md                       # Original benchmark docs
```

## Methodology

Based on [Metalama Performance Improvements Analysis](https://blog.postsharp.net/metalama-performance):

1. Fork NopCommerce and add aspects to methods at random
2. Control density via two parameters: type % and method %
3. Run `dotnet build /t:rebuild` via BenchmarkDotNet
4. Fit bilinear regression to TimeRatio vs (T, T×M)
5. Validate with R² (expect >0.95)

The bilinear model captures that overhead scales with both:
- Number of types containing aspects (pipeline overhead per type)
- Total methods with aspects (per-aspect processing cost)
