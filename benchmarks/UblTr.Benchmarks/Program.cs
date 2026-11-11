using BenchmarkDotNet.Running;
using UblTr.Benchmarks;

// throughput <outDir>   docs/s, MB/s and allocations per mode and invoice size (median of 5 timed rounds)
// scaling <outDir>      docs/s for 1/2/4/8 threads on 100-line invoices
// peak <file>           peak working set of a fresh process validating one file
// micro [BDN args]      BenchmarkDotNet micro-benchmarks
return args.FirstOrDefault() switch
{
    "throughput" => Harness.Throughput(args.ElementAtOrDefault(1) ?? "benchmark-results"),
    "scaling" => Harness.Scaling(args.ElementAtOrDefault(1) ?? "benchmark-results"),
    "peak" => Harness.Peak(args[1]),
    "micro" => Micro(args[1..]),
    _ => Usage(),
};

static int Micro(string[] rest)
{
    BenchmarkSwitcher.FromAssembly(typeof(Harness).Assembly).Run(rest);
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("usage: throughput [dir] | scaling [dir] | peak <file> | micro [BenchmarkDotNet args]");
    return 2;
}
