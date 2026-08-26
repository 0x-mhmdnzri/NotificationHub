using BenchmarkDotNet.Running;
using NotificationHub.Benchmarks;
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
