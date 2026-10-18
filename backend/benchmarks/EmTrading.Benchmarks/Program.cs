using BenchmarkDotNet.Running;

// dotnet run -c Release --project benchmarks/EmTrading.Benchmarks -- --filter "*MonteCarlo*"
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
