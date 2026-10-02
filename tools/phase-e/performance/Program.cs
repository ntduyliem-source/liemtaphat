using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Locus.Application;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;

if (args.Length != 1) throw new ArgumentException("Specify a new output JSON path.");
string output = Path.GetFullPath(args[0]);
if (File.Exists(output)) throw new IOException("Keep prior performance evidence immutable.");
var settings = new FormulaSettings(EnabledDomains: DetectionDomains.All, MarkerProfiles: MarkerPreferences.Default);
var process = Process.GetCurrentProcess();
var measurements = new List<object>();
var scheduler = new NativeAnalysisScheduler();

Task Single(string raw, string domain)
{
    var result = AnalysisWire.Analyze(new(raw, 1, settings));
    if (result.Source.Raw != raw || result.Regions.Count != 1 || result.Regions[0].Candidates[0].Document.Domain != domain)
        throw new InvalidOperationException("Measurement input did not produce its intended result.");
    return Task.CompletedTask;
}
Task Assist(string raw, string status, AssistanceCondition[] conditions)
{
    var result = ChemistryAssistanceWire.Analyze(new(new(raw, 1, settings), 0, conditions));
    if (result.Status != status) throw new InvalidOperationException(raw + ": " + result.Status);
    if (status == "available")
    {
        var proposal = AssistanceSerializer.Deserialize(result.Proposals.Single());
        if (!ReactionBalancer.VerifyConservation(proposal.Result.Candidates[0].Document.Root))
            throw new InvalidOperationException("Assistance did not conserve elements/charge.");
    }
    return Task.CompletedTask;
}
async Task Measure(string name, string input, Func<Task> operation, int count = 200)
{
    var first = Stopwatch.StartNew();
    await operation();
    double firstCallMs = first.Elapsed.TotalMilliseconds;
    for (int i = 0; i < 30; i++) await operation();
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long allocatedBefore = GC.GetTotalAllocatedBytes(true), managedBefore = GC.GetTotalMemory(false);
    long[] collectionsBefore = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
    var samples = new double[count];
    for (int i = 0; i < count; i++)
    {
        long start = Stopwatch.GetTimestamp();
        await operation();
        samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    long managedAfter = GC.GetTotalMemory(true);
    Array.Sort(samples);
    measurements.Add(new {
        name, utf16Length = input.Length, samples = count, warmups = 30, firstCallMs,
        p50Ms = samples[(int)Math.Ceiling(count * .50) - 1],
        p95Ms = samples[(int)Math.Ceiling(count * .95) - 1], maxMs = samples[^1],
        allocatedBytesPerOperation = allocated / (double)count, managedBefore, managedAfter,
        gcCollections = new[] { GC.CollectionCount(0)-collectionsBefore[0], GC.CollectionCount(1)-collectionsBefore[1], GC.CollectionCount(2)-collectionsBefore[2] }
    });
    Console.WriteLine(name + " p95=" + samples[(int)Math.Ceiling(count * .95) - 1].ToString("F3") + " ms");
}

await Measure("math-analysis", "toan-[x mũ 2+1]", () => Single("toan-[x mũ 2+1]", "math"));
await Measure("physics-analysis", "ly-[v=10 m/s]", () => Single("ly-[v=10 m/s]", "physics"));
await Measure("chemistry-analysis", "hoa-[Ca(OH)2+HCl=CaCl2+H2O]", () => Single("hoa-[Ca(OH)2+HCl=CaCl2+H2O]", "chemistry"));
await Measure("balance-with-proposal-validation", "hoa-[Ca(OH)2+HCl=CaCl2+H2O]", () => Assist("hoa-[Ca(OH)2+HCl=CaCl2+H2O]", "available", []));
await Measure("products-with-proposal-validation", "hoa-[3H2+O2=]", () => Assist("hoa-[3H2+O2=]", "available", [new("activation", "ignition")]));
await Measure("missing-conditions", "hoa-[3H2+O2=]", () => Assist("hoa-[3H2+O2=]", "needs-conditions", []));
string paragraph = string.Join("\r\n", Enumerable.Range(0, 100).Select(i => $"Dòng {i + 1}: hoa-[H2+O2=H2O]. Giữ nguyên chữ Việt."));
await Measure("mixed-paragraph-100-equations", paragraph, async () => {
    var result = await ContentAnalyzer.AnalyzeAsync(new(paragraph, 1, settings), scheduler, null, CancellationToken.None);
    if (result.Raw != paragraph || result.Regions.Count != 100)
        throw new InvalidOperationException("Paragraph mismatch: " + result.Regions.Count + " regions; " + string.Join(" | ", result.Regions.Take(6).Select(r => paragraph[r.Start..r.End])) + "; " + string.Join(" | ", result.Notices));
}, 100);
string oversized = new('x', FormulaSession.MaxSourceLength + 1);
await Measure("oversized-assistance-refusal", oversized, () => {
    try { ChemistryAssistanceWire.Analyze(new(new(oversized, 1, settings), 0, [])); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new InvalidOperationException("Oversized input accepted.");
});

process.Refresh();
var binaries = new[] { typeof(FormulaSession).Assembly, typeof(SourceSnapshot).Assembly }.Select(a => new {
    name = a.GetName().Name, path = a.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))
}).ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
    capturedAtUtc = DateTime.UtcNow, status = "MEASURED", scope = "Published native DLLs; includes result validation. No browser, Word, rendering, debounce, startup UI or keyboard latency.",
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessors = Environment.ProcessorCount,
    cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), process.PrivateMemorySize64, process.WorkingSet64, process.PeakWorkingSet64,
    firstCallNote = "First call per case includes any remaining lazy initialization/JIT. Cases share one fresh process; not an independent cold-start distribution.",
    binaries, measurements
}, new JsonSerializerOptions { WriteIndented = true }));
