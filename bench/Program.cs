using BenchmarkDotNet.Running;
using Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(ScannerBenchmarks).Assembly)
                 .Run(args);
