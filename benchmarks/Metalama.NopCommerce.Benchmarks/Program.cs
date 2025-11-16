using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Mathematics;
using BenchmarkDotNet.Running;
using Perfolizer.Mathematics.Common;

IConfig? config = null;
Job? job = null;

// Parse command-line arguments
foreach (var arg in args)
{
    if (arg.StartsWith("-lc", StringComparison.OrdinalIgnoreCase))
    {
        job = (job ?? Job.Default).UnfreezeCopy();
        job.Accuracy.MaxRelativeError = 0.05;  // 5% threshold allows fewer iterations
        job.Run.WarmupCount = 1;
        job.Run.LaunchCount = 1;
        job.Run.MinIterationCount = 5;         // Minimum 5 iterations for basic statistics
        job.Run.MaxIterationCount = 20;        // Cap at 20 to prevent excessive runs
        job.Run.InvocationCount = 1;           // Call benchmark once per iteration (no overhead calculation)
        job.Run.UnrollFactor = 1;              // Don't unroll loops
    }
    else if (arg.StartsWith("-hc", StringComparison.OrdinalIgnoreCase))
    {
        job = (job ?? Job.Default).UnfreezeCopy();
        job.Accuracy.MaxRelativeError = 0.005; // 0.5% threshold for high confidence measurements
    }
}

// If configuration was provided, create config with the job
if (job != null)
{
    config = ManualConfig.CreateEmpty().WithOptions(ConfigOptions.Default).AddJob(job);
}

BenchmarkRunner.Run<Benchmark>(config);

public class Benchmark
{
    static async Task RunProcess(string fileName, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        var sb = new StringBuilder();

        process.OutputDataReceived += appendLine;
        process.ErrorDataReceived += appendLine;

        void appendLine(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                lock (sb)
                {
                    sb.AppendLine(e.Data);
                }
            }
        }

        process.Start();

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"""
                Build failed with code {process.ExitCode}:
                {sb}
                """);
        }
    }

    private static Task RunDotnetBuild(string project, Dictionary<string, string> properties)
        => RunProcess("dotnet", ["build", Path.Combine(_repoRoot, project), .. properties.Select(p => $"-p:{p.Key}={p.Value}")]);

    private static Task RunDotnetClean(string project)
        => RunProcess("dotnet", ["clean", Path.Combine(_repoRoot, project)]);

    private static string FindRepoRoot()
    {
        var dir = Environment.CurrentDirectory;

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException($"Could not find repo root when starting from '{Environment.CurrentDirectory}'.");
    }

    private static readonly string _repoRoot = FindRepoRoot();

    private const string SOLUTION = @"src\NopCommerce.sln";

    [IterationSetup(Target = nameof(WithoutMetalama))]
    public void SetupWithoutMetalama() => RunDotnetClean(SOLUTION).Wait();

    [Benchmark(Baseline = true)]
    public Task WithoutMetalama() => RunDotnetBuild(SOLUTION, new Dictionary<string, string>
    {
        ["MetalamaEnabled"] = "false",
        ["ExtraConstants"] = "BENCHMARK"
    });

    public Task WithBareMetalama() => RunDotnetBuild(SOLUTION, new Dictionary<string, string>
    {
        ["MetalamaEnabled"] = "true",
        ["ExtraConstants"] = "BENCHMARK;NO_BENCHMARK_FABRIC"
    });

    [IterationSetup(Target = nameof(WithMetalama))]
    public void SetupWithMetalama() => RunDotnetClean(SOLUTION).Wait();

    [Arguments(1, 10)]
    [Arguments(1, 50)]
    [Arguments(1, 100)]
    [Arguments(10, 10)]
    [Arguments(10, 50)]
    [Arguments(10, 100)]
    [Arguments(50, 10)]
    [Arguments(50, 50)]
    [Arguments(50, 100)]
    [Arguments(100, 10)]
    [Arguments(100, 50)]
    [Arguments(100, 100)]
    [Benchmark]
    public Task WithMetalama(int benchmarkedTypesPercentage, int benchmarkedMembersPercentage)
    {
        static int calculateFractionInverse(int percentage)
        {
            var fractionInverse = 1 / (percentage / 100.0);
            var fractionInverseInt = (int)fractionInverse;

            if (fractionInverse != fractionInverseInt)
            {
                throw new ArgumentException($"Invalid percentage: {percentage}");
            }

            return fractionInverseInt;
        }

        var benchmarkedTypesFractionInverse = calculateFractionInverse(benchmarkedTypesPercentage);
        var benchmarkedMembersFractionInverse = calculateFractionInverse(benchmarkedMembersPercentage);

        return RunDotnetBuild(SOLUTION, new Dictionary<string, string>
        {
            ["MetalamaEnabled"] = "true",
            ["ExtraConstants"] = "BENCHMARK",
            ["BenchmarkedTypesFractionInverse"] = benchmarkedTypesFractionInverse.ToString(),
            ["BenchmarkedMembersFractionInverse"] = benchmarkedMembersFractionInverse.ToString()
        });
    }
}