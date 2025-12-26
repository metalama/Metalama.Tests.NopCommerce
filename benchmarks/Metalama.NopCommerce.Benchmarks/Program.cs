using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<Benchmark>();

[Config(typeof(Config))]
public class Benchmark
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithMaxRelativeError(0.05) // Accept 5% variance
                .WithWarmupCount(1));
            ArtifactsPath = GetArtifactsPath();

            // Skip redundant combinations where T=0 and M>0
            // (when no types have aspects, method percentage is irrelevant)
            AddFilter(new SkipRedundantCombinationsFilter());
        }

        private static string GetArtifactsPath()
        {
            var baseDir = Path.Combine(Benchmark.FindRepoRoot(), "benchmarks", "results");
            Directory.CreateDirectory(baseDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm");
            var artifactsPath = Path.Combine(baseDir, timestamp);
            Directory.CreateDirectory(artifactsPath);
            return artifactsPath;
        }
    }

    private class SkipRedundantCombinationsFilter : IFilter
    {
        public bool Predicate(BenchmarkCase benchmarkCase)
        {
            var parameters = benchmarkCase.Parameters;
            var t = parameters["BenchmarkedTypesPercentage"] as int? ?? 0;
            var m = parameters["BenchmarkedMembersPercentage"] as int? ?? 0;

            // WithoutMetalama doesn't use T/M params, so only run it once (at T=0, M=0)
            if (benchmarkCase.Descriptor.WorkloadMethod.Name == "WithoutMetalama")
            {
                return t == 0 && m == 0;
            }

            // For WithMetalama: skip T=0,M>0 (redundant since no types means method % is irrelevant)
            if (t == 0 && m > 0)
            {
                return false;
            }

            return true;
        }
    }

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
    {
        static string EscapePropertyValue(string value) => value.Replace(",", "%2C").Replace(";", "%3B");
        return RunProcess("dotnet", ["build", Path.Combine(_repoRoot, project), .. properties.Select(p => $"-p:{p.Key}={EscapePropertyValue(p.Value)}")]);
    }

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

#if ALL
    [Params(100)]
#elif TYPICAL
    [Params(10)]
#else
    [Params(0, 1, 5, 10, 20, 50, 100)]
#endif
    public int BenchmarkedTypesPercentage { get; set; }

#if ALL
    [Params(100)]
#elif TYPICAL
    [Params(10)]
#else
    [Params(0, 10, 30, 60, 100)]
#endif
    public int BenchmarkedMembersPercentage { get; set; }

#if REGRESSION
    [Params("2025.1.17", "2025.2.5-rc", "2026.0.10-rc")]
    public string? Version { get; set; }
#else
    public string? Version => null;
#endif

    [IterationSetup(Target = nameof(WithoutMetalama))]
    public void SetupWithoutMetalama() => RunDotnetClean(SOLUTION).Wait();

    [Benchmark(Baseline = true)]
    public Task WithoutMetalama() => RunDotnetBuild(SOLUTION, new Dictionary<string, string>
    {
        ["MetalamaEnabled"] = "false",
        ["ExtraConstants"] = "BENCHMARK"
    });

    [IterationSetup(Target = nameof(WithMetalama))]
    public void SetupWithMetalama() => RunDotnetClean(SOLUTION).Wait();

    [Benchmark]
    public Task WithMetalama()
    {
        var properties = new Dictionary<string, string>
        {
            ["MetalamaEnabled"] = "true",
            ["ExtraConstants"] = "BENCHMARK",
            ["BenchmarkedTypesPercentage"] = BenchmarkedTypesPercentage.ToString(),
            ["BenchmarkedMembersPercentage"] = BenchmarkedMembersPercentage.ToString()
        };

        if (Version != null)
        {
            properties["MetalamaVersion"] = Version;
        }

        return RunDotnetBuild(SOLUTION, properties);
    }
}
