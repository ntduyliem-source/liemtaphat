using Locus.WebProbe;
using System.Text.Json;
var output=Path.GetFullPath(args.Length>0?args[0]:"artifacts/web/native");Directory.CreateDirectory(output);
await File.WriteAllTextAsync(Path.Combine(output,"parity.json"),Probe.Parity());
var contracts = await Probe.Contracts();
await File.WriteAllTextAsync(Path.Combine(output,"contracts.json"),contracts);
await File.WriteAllTextAsync(Path.Combine(output,"timing.json"),Probe.Timing());
await File.WriteAllTextAsync(Path.Combine(output,"scheduling.json"),await Probe.Scheduling());
Console.WriteLine($"Native baseline: {Probe.Inputs().Count} parity sources; {output}");
using var report = JsonDocument.Parse(contracts);
if (report.RootElement.GetProperty("summary").GetProperty("failed").GetInt32() != 0)
    Environment.ExitCode = 1;
