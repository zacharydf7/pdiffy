using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// In-process, so BenchmarkDotNet doesn't build a project of its own for each run. Pass --filter to pick benchmarks.
var config = DefaultConfig.Instance.AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance));
BenchmarkSwitcher.FromAssembly(typeof(PDiffy.Benchmarks.PdfBenchmarks).Assembly).Run(args, config);
