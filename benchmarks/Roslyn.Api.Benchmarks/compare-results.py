#!/usr/bin/env python3
"""
Compare Roslyn API benchmark results across different versions.
Usage: python compare-results.py <results-dir-1> <results-dir-2>
"""

import sys
import os
import glob
import pandas as pd
from pathlib import Path


def parse_time(time_str: str) -> float:
    """Parse BenchmarkDotNet time string to microseconds."""
    if pd.isna(time_str):
        return float('nan')

    time_str = str(time_str).strip()

    # Handle different units
    if 'ns' in time_str:
        return float(time_str.replace('ns', '').replace(',', '').strip()) / 1000
    elif 'μs' in time_str or 'us' in time_str:
        return float(time_str.replace('μs', '').replace('us', '').replace(',', '').strip())
    elif 'ms' in time_str:
        return float(time_str.replace('ms', '').replace(',', '').strip()) * 1000
    elif 's' in time_str:
        return float(time_str.replace('s', '').replace(',', '').strip()) * 1000000
    else:
        return float(time_str.replace(',', '').strip())


def load_results(results_dir: str) -> pd.DataFrame:
    """Load benchmark results from a directory."""
    csv_files = glob.glob(os.path.join(results_dir, '**/results/*.csv'), recursive=True)
    if not csv_files:
        csv_files = glob.glob(os.path.join(results_dir, '*.csv'))

    if not csv_files:
        raise FileNotFoundError(f"No CSV files found in {results_dir}")

    dfs = []
    for csv_file in csv_files:
        df = pd.read_csv(csv_file)
        df['SourceFile'] = os.path.basename(csv_file)
        dfs.append(df)

    return pd.concat(dfs, ignore_index=True)


def compare(dir1: str, dir2: str):
    """Compare benchmark results from two directories."""
    print(f"Loading results from: {dir1}")
    df1 = load_results(dir1)

    print(f"Loading results from: {dir2}")
    df2 = load_results(dir2)

    # Extract version from Job column or directory name
    def get_version(df, dirname):
        if 'Job' in df.columns:
            jobs = df['Job'].unique()
            for job in jobs:
                if 'Roslyn' in str(job):
                    return str(job)
        return os.path.basename(dirname)

    version1 = get_version(df1, dir1)
    version2 = get_version(df2, dir2)

    print(f"\n{'=' * 80}")
    print(f"ROSLYN API BENCHMARK COMPARISON")
    print(f"{'=' * 80}")
    print(f"\nVersion 1: {version1}")
    print(f"Version 2: {version2}")
    print()

    # Parse Mean times
    df1['MeanUs'] = df1['Mean'].apply(parse_time)
    df2['MeanUs'] = df2['Mean'].apply(parse_time)

    # Merge on Method name
    merged = pd.merge(
        df1[['Method', 'MeanUs']],
        df2[['Method', 'MeanUs']],
        on='Method',
        suffixes=('_v1', '_v2')
    )

    # Calculate difference
    merged['Diff_Us'] = merged['MeanUs_v2'] - merged['MeanUs_v1']
    merged['Diff_Pct'] = ((merged['MeanUs_v2'] - merged['MeanUs_v1']) / merged['MeanUs_v1']) * 100

    # Sort by percentage difference (worst first)
    merged = merged.sort_values('Diff_Pct', ascending=False)

    # Print results
    print(f"{'Method':<45} {'V1 (μs)':>12} {'V2 (μs)':>12} {'Diff':>10} {'Change':>10}")
    print("-" * 95)

    for _, row in merged.iterrows():
        method = row['Method']
        v1 = row['MeanUs_v1']
        v2 = row['MeanUs_v2']
        diff = row['Diff_Us']
        pct = row['Diff_Pct']

        # Color coding (ANSI)
        if pct > 10:
            color = '\033[91m'  # Red
        elif pct > 5:
            color = '\033[93m'  # Yellow
        elif pct < -5:
            color = '\033[92m'  # Green
        else:
            color = '\033[0m'   # Default

        print(f"{method:<45} {v1:>12.2f} {v2:>12.2f} {diff:>+10.2f} {color}{pct:>+9.1f}%\033[0m")

    print()
    print("-" * 95)

    # Summary statistics
    avg_change = merged['Diff_Pct'].mean()
    max_regression = merged['Diff_Pct'].max()
    max_improvement = merged['Diff_Pct'].min()

    print(f"\nSummary:")
    print(f"  Average change: {avg_change:+.1f}%")
    print(f"  Worst regression: {max_regression:+.1f}%")
    print(f"  Best improvement: {max_improvement:+.1f}%")

    # Highlight significant regressions
    significant = merged[merged['Diff_Pct'] > 10]
    if not significant.empty:
        print(f"\n{'!' * 60}")
        print("SIGNIFICANT REGRESSIONS (>10%):")
        for _, row in significant.iterrows():
            print(f"  - {row['Method']}: {row['Diff_Pct']:+.1f}%")
        print(f"{'!' * 60}")


def main():
    if len(sys.argv) < 3:
        print("Usage: python compare-results.py <results-dir-1> <results-dir-2>")
        print("\nExample:")
        print("  python compare-results.py roslyn-api-results/2025-01-01-roslyn-4.14 roslyn-api-results/2025-01-01-roslyn-5.0")
        return 1

    compare(sys.argv[1], sys.argv[2])
    return 0


if __name__ == '__main__':
    sys.exit(main())
