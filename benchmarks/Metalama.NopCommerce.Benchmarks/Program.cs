using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
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
                .WithWarmupCount(3));
            ArtifactsPath = GetArtifactsPath();
        }

        private static string GetArtifactsPath()
        {
            var baseDir = Path.Combine(Environment.CurrentDirectory, "results");
            Directory.CreateDirectory(baseDir);

            var runNumber = 1;
            while (Directory.Exists(Path.Combine(baseDir, runNumber.ToString("D2"))))
            {
                runNumber++;
            }

            var artifactsPath = Path.Combine(baseDir, runNumber.ToString("D2"));
            Directory.CreateDirectory(artifactsPath);
            return artifactsPath;
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

#if REGRESSION_TEST
    // 2026.0.10-rc
    [Arguments(1, 10, "2026.0.10-rc", null)]
    [Arguments(1, 50, "2026.0.10-rc", null)]
    [Arguments(1, 100, "2026.0.10-rc", null)]
    [Arguments(10, 10, "2026.0.10-rc", null)]
    [Arguments(10, 50, "2026.0.10-rc", null)]
    [Arguments(10, 100, "2026.0.10-rc", null)]
    [Arguments(50, 10, "2026.0.10-rc", null)]
    [Arguments(50, 50, "2026.0.10-rc", null)]
    [Arguments(50, 100, "2026.0.10-rc", null)]
    [Arguments(100, 10, "2026.0.10-rc", null)]
    [Arguments(100, 50, "2026.0.10-rc", null)]
    [Arguments(100, 100, "2026.0.10-rc", null)]
    // 2025.2.5-rc
    [Arguments(1, 10, "2025.2.5-rc", null)]
    [Arguments(1, 50, "2025.2.5-rc", null)]
    [Arguments(1, 100, "2025.2.5-rc", null)]
    [Arguments(10, 10, "2025.2.5-rc", null)]
    [Arguments(10, 50, "2025.2.5-rc", null)]
    [Arguments(10, 100, "2025.2.5-rc", null)]
    [Arguments(50, 10, "2025.2.5-rc", null)]
    [Arguments(50, 50, "2025.2.5-rc", null)]
    [Arguments(50, 100, "2025.2.5-rc", null)]
    [Arguments(100, 10, "2025.2.5-rc", null)]
    [Arguments(100, 50, "2025.2.5-rc", null)]
    [Arguments(100, 100, "2025.2.5-rc", null)]
    // 2025.1.17
    [Arguments(1, 10, "2025.1.17", null)]
    [Arguments(1, 50, "2025.1.17", null)]
    [Arguments(1, 100, "2025.1.17", null)]
    [Arguments(10, 10, "2025.1.17", null)]
    [Arguments(10, 50, "2025.1.17", null)]
    [Arguments(10, 100, "2025.1.17", null)]
    [Arguments(50, 10, "2025.1.17", null)]
    [Arguments(50, 50, "2025.1.17", null)]
    [Arguments(50, 100, "2025.1.17", null)]
    [Arguments(100, 10, "2025.1.17", null)]
    [Arguments(100, 50, "2025.1.17", null)]
    [Arguments(100, 100, "2025.1.17", null)]
#elif BALANCED
    [Arguments(10, 10, null, null)]
#else
    [Arguments(1, 10, null, null)]
    [Arguments(1, 50, null, null)]
    [Arguments(1, 100, null, null)]
    [Arguments(10, 10, null, null)]
    [Arguments(10, 50, null, null)]
    [Arguments(10, 100, null, null)]
    [Arguments(50, 10, null, null)]
    [Arguments(50, 50, null, null)]
    [Arguments(50, 100, null, null)]
    [Arguments(100, 10, null, null)]
    [Arguments(100, 50, null, null)]
    [Arguments(100, 100, null, null)]
#endif
    [Benchmark]
    public Task WithMetalama(int benchmarkedTypesPercentage, int benchmarkedMembersPercentage, string? version = null, string? extraConstants = null)
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

        var constants = "BENCHMARK";
        if (extraConstants != null)
        {
            constants += "," + extraConstants;
        }

        var properties = new Dictionary<string, string>
        {
            ["MetalamaEnabled"] = "true",
            ["ExtraConstants"] = constants,
            ["BenchmarkedTypesFractionInverse"] = benchmarkedTypesFractionInverse.ToString(),
            ["BenchmarkedMembersFractionInverse"] = benchmarkedMembersFractionInverse.ToString()
        };

        if (version != null)
        {
            properties["MetalamaVersion"] = version;
        }

        return RunDotnetBuild(SOLUTION, properties);
    }
}
