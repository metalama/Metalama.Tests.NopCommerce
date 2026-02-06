using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

Console.WriteLine($"Command line: {string.Join(" ", args)}");

var preciseMode = args.Contains("--precise");
var filteredArgs = args.Where(a => a != "--precise").ToArray();

#if REGRESSION
Console.WriteLine("REGRESSION mode enabled - testing multiple Metalama versions");
#endif

BenchmarkRunner.Run<Benchmark>(new Benchmark.Config(preciseMode), filteredArgs);

[Config(typeof(Config))]
public class Benchmark
{
    public class Config : ManualConfig
    {
        public Config() : this(false) { }

        public Config(bool preciseMode)
        {
#if DOTNETSDK
            var maxRelativeError = preciseMode ? 0.0005 : 0.01; // 0.05% or 1% variance for SDK comparison
#else
            var maxRelativeError = preciseMode ? 0.0005 : 0.05; // 0.05% or 5% variance for Metalama benchmarks
#endif
            AddJob(Job.Default
                .WithMaxRelativeError(maxRelativeError)
                .WithWarmupCount(1));
            AddLogger(ConsoleLogger.Default);
            AddExporter(CsvExporter.Default);
            AddColumnProvider(DefaultColumnProviders.Instance);
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
#if DOTNETSDK
            // In DOTNETSDK mode, only run WithoutMetalama
            return benchmarkCase.Descriptor.WorkloadMethod.Name == "WithoutMetalama";
#else
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
#endif
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

    private static void SetGlobalJsonSdkVersion(string? version)
    {
        if (version == null)
        {
            return;
        }

        var globalJsonPath = Path.Combine(_repoRoot, "global.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(globalJsonPath))!;
        json["sdk"]!["version"] = version;
        json["sdk"]!["rollForward"] = "feature";
        File.WriteAllText(globalJsonPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

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

#if !DOTNETSDK
#if ALL
    [Params(100)]
#elif TYPICAL
    [Params(10)]
#else
    [Params(0, 1, 5, 10, 20, 50, 100)]
#endif
#endif
    public int BenchmarkedTypesPercentage { get; set; }

#if !DOTNETSDK
#if ALL
    [Params(100)]
#elif TYPICAL
    [Params(10)]
#else
    [Params(0, 10, 30, 60, 100)]
#endif
#endif
    public int BenchmarkedMembersPercentage { get; set; }

#if REGRESSION
    [Params("2026.0.16", "2026.1.1-preview", "2025.1.17")]
    public string? Version { get; set; }
#else
    public string? Version => null;
#endif

#if DAILY_BUILDS
    // CommitDate parameter for testing daily builds from the commits directory.
    // The dates should correspond to directories under ..\Metalama\commits\
    // Usage: dotnet run -c Release -p:DefineConstants=DAILY_BUILDS
    [Params("2025-09-10", "2025-09-16_01_cc978ebc")]
    public string? CommitDate { get; set; }
#else
    public string? CommitDate => null;
#endif

#if DOTNETSDK
    [Params("9.0.308", "8.0.416", "10.0.101")]
    public string DotNetSdkVersion { get; set; } = "9.0.308";
#else
    public string? DotNetSdkVersion => null;
#endif

    [IterationSetup(Target = nameof(WithoutMetalama))]
    public void SetupWithoutMetalama()
    {
        SetGlobalJsonSdkVersion(DotNetSdkVersion);
        RunDotnetClean(SOLUTION).Wait();
    }

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

        if (CommitDate != null)
        {
            properties["MetalamaCommitDate"] = CommitDate;
        }

        return RunDotnetBuild(SOLUTION, properties);
    }
}
