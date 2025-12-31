#!/usr/bin/env python3
"""
Metalama Build Overhead Regression Analysis

Analyzes BenchmarkDotNet CSV results to predict build time overhead
as a function of aspect density (type % × method %).

Model: TimeRatio = B0 + B1*T + B2*(T*M)
Where:
  - TimeRatio = build time with Metalama / build time without
  - T = fraction of types with aspects (0-1)
  - M = fraction of methods within targeted types with aspects (0-1)
"""

import sys
import io

# Force UTF-8 output on Windows
if sys.platform == 'win32':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

import argparse
import glob
import re
from pathlib import Path

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


def parse_time(time_str: str) -> float:
    """Parse time string like '17.18 s' to float seconds."""
    if pd.isna(time_str) or time_str.strip().upper() == 'NA':
        return float('nan')
    match = re.match(r'([\d.]+)\s*s', time_str.strip())
    if match:
        return float(match.group(1))
    raise ValueError(f"Cannot parse time: {time_str}")


def get_column(df: pd.DataFrame, *names: str) -> pd.Series:
    """Get a column by trying multiple possible names (case-insensitive)."""
    df_cols_lower = {c.lower(): c for c in df.columns}
    for name in names:
        if name.lower() in df_cols_lower:
            return df[df_cols_lower[name.lower()]]
    raise KeyError(f"None of the columns found: {names}")


def load_csv(filepath: Path) -> pd.DataFrame:
    """Load and parse a BenchmarkDotNet CSV file."""
    df = pd.read_csv(filepath)

    # Parse Mean column
    df['MeanSeconds'] = df['Mean'].apply(parse_time)

    # Convert percentage columns to fractions (support both old param names and new property names)
    df['T'] = pd.to_numeric(get_column(df, 'BenchmarkedTypesPercentage', 'benchmarkedTypesPercentage'), errors='coerce') / 100.0
    df['M'] = pd.to_numeric(get_column(df, 'BenchmarkedMembersPercentage', 'benchmarkedMembersPercentage'), errors='coerce') / 100.0

    # Normalize version column
    try:
        df['version'] = get_column(df, 'Version', 'version')
    except KeyError:
        df['version'] = 'default'

    # Add source file
    df['SourceFile'] = filepath.name

    return df


def load_and_merge_csvs(patterns: list[str]) -> pd.DataFrame:
    """Load multiple CSV files matching patterns and merge them."""
    all_files = []
    for pattern in patterns:
        all_files.extend(glob.glob(pattern))

    if not all_files:
        raise FileNotFoundError(f"No files found matching: {patterns}")

    dfs = []
    for filepath in all_files:
        print(f"Loading: {filepath}")
        dfs.append(load_csv(Path(filepath)))

    return pd.concat(dfs, ignore_index=True)


def fit_regression(T: np.ndarray, M: np.ndarray, y: np.ndarray) -> tuple[np.ndarray, float, float, np.ndarray]:
    """
    Fit bilinear model: y = β₀ + β₁×T + β₂×(T×M)

    Returns:
        coefficients: [β₀, β₁, β₂]
        r_squared: coefficient of determination
        adj_r_squared: adjusted R² (corrected for number of predictors)
        std_errors: standard errors for each coefficient
    """
    from scipy import stats

    n = len(y)
    p = 3  # number of parameters (β₀, β₁, β₂)

    # Design matrix: [1, T, T*M]
    X = np.column_stack([
        np.ones_like(T),
        T,
        T * M
    ])

    # Least squares solution
    coeffs, residuals, rank, s = np.linalg.lstsq(X, y, rcond=None)

    # Compute R²
    y_pred = X @ coeffs
    ss_res = np.sum((y - y_pred) ** 2)
    ss_tot = np.sum((y - np.mean(y)) ** 2)
    r_squared = 1 - (ss_res / ss_tot) if ss_tot > 0 else 0.0

    # Compute adjusted R²: 1 - (1 - R²) × (n - 1) / (n - p)
    adj_r_squared = 1 - (1 - r_squared) * (n - 1) / (n - p) if n > p else 0.0

    # Compute standard errors of coefficients
    mse = ss_res / (n - p)  # mean squared error
    var_covar = mse * np.linalg.inv(X.T @ X)  # variance-covariance matrix
    std_errors = np.sqrt(np.diag(var_covar))

    return coeffs, r_squared, adj_r_squared, std_errors


def predict(coeffs: np.ndarray, T: float, M: float) -> float:
    """Predict TimeRatio given coefficients and T, M values."""
    return coeffs[0] + coeffs[1] * T + coeffs[2] * (T * M)


def analyze(df: pd.DataFrame) -> pd.DataFrame:
    """Perform regression analysis for each Metalama version."""

    # Filter out rows with missing Mean values
    df = df[df['MeanSeconds'].notna()].copy()

    # Get baseline (WithoutMetalama)
    baseline_df = df[df['Method'] == 'WithoutMetalama']
    if baseline_df.empty:
        raise ValueError("No 'WithoutMetalama' baseline found in data")

    baseline_time = baseline_df['MeanSeconds'].mean()
    print(f"\nBaseline build time (without Metalama): {baseline_time:.2f}s")

    # Get WithMetalama rows
    metalama_df = df[df['Method'] == 'WithMetalama'].copy()
    metalama_df['TimeRatio'] = metalama_df['MeanSeconds'] / baseline_time

    # Analyze each version
    results = []
    versions = metalama_df['version'].dropna().unique()

    for version in sorted(versions):
        version_df = metalama_df[metalama_df['version'] == version].dropna(subset=['TimeRatio'])

        T = version_df['T'].values
        M = version_df['M'].values
        y = version_df['TimeRatio'].values

        coeffs, r_squared, adj_r_squared, std_errors = fit_regression(T, M, y)

        # Predict at typical 10% × 10%
        overhead_10_10 = predict(coeffs, 0.1, 0.1)

        # 95% confidence interval half-width (t-value for 95% CI with n-p degrees of freedom)
        from scipy import stats
        n = len(version_df)
        p = 3
        t_val = stats.t.ppf(0.975, n - p)  # two-tailed 95%

        results.append({
            'Version': version,
            'β₀ (Base)': coeffs[0],
            'β₀ ±95%CI': t_val * std_errors[0],
            'β₁ (Type)': coeffs[1],
            'β₁ ±95%CI': t_val * std_errors[1],
            'β₂ (Method)': coeffs[2],
            'β₂ ±95%CI': t_val * std_errors[2],
            'R²': r_squared,
            'Adj R²': adj_r_squared,
            'Overhead@10%×10%': overhead_10_10,
            'N': n
        })

    return pd.DataFrame(results), metalama_df, baseline_time


def plot_2d_interaction(metalama_df: pd.DataFrame, results_df: pd.DataFrame, output_path: Path):
    """Generate 2D plot of TimeRatio vs T×M interaction term."""
    fig, ax = plt.subplots(figsize=(10, 6))

    versions = metalama_df['version'].dropna().unique()
    colors = plt.cm.tab10(np.linspace(0, 1, len(versions)))

    for i, version in enumerate(sorted(versions)):
        version_df = metalama_df[metalama_df['version'] == version]
        result = results_df[results_df['Version'] == version].iloc[0]

        # Compute T×M for x-axis
        TM = version_df['T'] * version_df['M']

        # Scatter actual data
        ax.scatter(
            TM,
            version_df['TimeRatio'],
            c=[colors[i]],
            label=f'{version}',
            s=50,
            alpha=0.7
        )

        # Fitted line (simplified: at average T)
        T_avg = version_df['T'].mean()
        TM_line = np.linspace(0, 1, 100)
        # For visualization, we show the trend at different T values
        # TimeRatio = β₀ + β₁×T + β₂×(T×M)
        # At fixed T: TimeRatio = (β₀ + β₁×T) + β₂×T×M
        coeffs = [result['β₀ (Base)'], result['β₁ (Type)'], result['β₂ (Method)']]

        # Plot lines for T=0.1, 0.5, 1.0
        for T_val, linestyle in [(0.1, ':'), (0.5, '--'), (1.0, '-')]:
            M_line = np.linspace(0, 1, 100)
            TM_vals = T_val * M_line
            y_line = coeffs[0] + coeffs[1] * T_val + coeffs[2] * TM_vals
            if i == 0:  # Only add to legend once
                ax.plot(TM_vals, y_line, color=colors[i], linestyle=linestyle,
                       alpha=0.5, label=f'T={T_val:.0%}' if i == 0 else None)
            else:
                ax.plot(TM_vals, y_line, color=colors[i], linestyle=linestyle, alpha=0.5)

    ax.set_xlabel('T × M (Effective Aspect Density)')
    ax.set_ylabel('TimeRatio (with/without Metalama)')
    ax.set_title('Build Time Overhead vs Aspect Density')
    ax.legend(loc='upper left')
    ax.grid(True, alpha=0.3)

    plt.tight_layout()
    plt.savefig(output_path / 'plot_2d_interaction.png', dpi=150)
    plt.close()
    print(f"Saved: {output_path / 'plot_2d_interaction.png'}")


def main():
    parser = argparse.ArgumentParser(
        description='Analyze Metalama benchmark results and predict build overhead.',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python analyze_benchmarks.py results/16/results/Benchmark-report.csv
  python analyze_benchmarks.py "results/*/results/Benchmark-report.csv"
  python analyze_benchmarks.py file1.csv file2.csv --output-dir ./analysis
        """
    )
    parser.add_argument(
        'csv_files',
        nargs='+',
        help='CSV file(s) or glob pattern(s) to analyze'
    )
    parser.add_argument(
        '--output-dir', '-o',
        type=Path,
        default=None,
        help='Directory for output plots (default: same as first CSV)'
    )
    parser.add_argument(
        '--plots',
        action='store_true',
        help='Generate visualization plots (requires display)'
    )

    args = parser.parse_args()

    # Load data
    try:
        df = load_and_merge_csvs(args.csv_files)
    except FileNotFoundError as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1

    # Determine output directory
    if args.output_dir:
        output_path = args.output_dir
    else:
        # Use directory of first matching file
        first_file = glob.glob(args.csv_files[0])[0]
        output_path = Path(first_file).parent

    output_path.mkdir(parents=True, exist_ok=True)

    # Analyze
    try:
        results_df, metalama_df, baseline_time = analyze(df)
    except ValueError as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1

    # Print results table
    print("\n" + "=" * 80)
    print("METALAMA BUILD OVERHEAD REGRESSION ANALYSIS")
    print("=" * 80)
    print(f"\nModel: TimeRatio = β₀ + β₁×T + β₂×(T×M)")
    print(f"Where T = type%, M = method% (as fractions 0-1)")
    print()

    # Format and print table
    print(results_df.to_string(index=False, float_format=lambda x: f'{x:.4f}'))

    print("\n" + "-" * 80)
    print("Interpretation:")
    for _, row in results_df.iterrows():
        base_overhead = (row['β₀ (Base)'] - 1) * 100
        overhead_10_10 = (row['Overhead@10%×10%'] - 1) * 100
        print(f"  {row['Version']}: Base overhead {base_overhead:+.1f}%, "
              f"at 10%×10% density: {overhead_10_10:+.1f}% "
              f"(R²={row['R²']:.2%})")

    # Generate plot
    if args.plots:
        print("\nGenerating plot...")
        plot_2d_interaction(metalama_df, results_df, output_path)

    # Save results CSV
    results_csv = output_path / 'regression_results.csv'
    results_df.to_csv(results_csv, index=False)
    print(f"\nSaved: {results_csv}")

    return 0


if __name__ == '__main__':
    sys.exit(main())
