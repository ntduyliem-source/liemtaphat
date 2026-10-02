using System.Globalization;
using System.Net;
using System.Text.Json;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 512 };
try
{
    var cli = CliArguments.Parse(args);
    if (cli.Help)
    {
        Console.WriteLine("Locus M1\n  --input TEXT [--mode explicit|passive|marked] [--open TEXT --close TEXT]\n  --serve [PORT]    Local interactive preview; default port 4180.");
        return 0;
    }
    if (!cli.Serve)
    {
        if (cli.Input == null) throw new ArgumentException("Use --input TEXT or --serve [PORT].");
        var result = Analyze(new AnalyzeRequest { Input = cli.Input, Mode = cli.Mode, Open = cli.Open, Close = cli.Close });
        Console.WriteLine(JsonSerializer.Serialize(Present(result), jsonOptions));
        return 0;
    }

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(server =>
    {
        server.Listen(IPAddress.Loopback, cli.Port);
        server.Limits.MaxRequestBodySize = 262144;
        server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
    });
    var app = builder.Build();
    var assetDirectory = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    if (!Directory.Exists(assetDirectory)) assetDirectory = Path.Combine(Directory.GetCurrentDirectory(), "src", "Locus.Cli", "wwwroot");
    if (!Directory.Exists(assetDirectory)) throw new InvalidOperationException("Preview assets were not found; build or publish Locus.Cli first.");

    app.Use(async (context, next) =>
    {
        bool hostAllowed = context.Request.Host.Port == cli.Port &&
            (context.Request.Host.Host == "127.0.0.1" || string.Equals(context.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase));
        string origin = context.Request.Headers.Origin.ToString();
        bool originAllowed = origin.Length == 0 || origin == $"http://127.0.0.1:{cli.Port}" || origin == $"http://localhost:{cli.Port}";
        if (!hostAllowed || !originAllowed) { context.Response.StatusCode = 403; return; }
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next();
    });
    var assets = new Dictionary<string, (string File, string Type)>
    {
        ["/"] = ("index.html", "text/html; charset=utf-8"),
        ["/app.js"] = ("app.js", "text/javascript; charset=utf-8"),
        ["/app.css"] = ("app.css", "text/css; charset=utf-8")
    };
    foreach (var asset in assets)
    {
        string path = Path.Combine(assetDirectory, asset.Value.File);
        string type = asset.Value.Type;
        app.MapGet(asset.Key, () => Results.File(path, type));
    }
    app.MapPost("/analyze", async (HttpContext context) =>
    {
        // Browsers cannot invoke this JSON-only endpoint with a cross-origin form.
        if (!context.Request.HasJsonContentType()) return Results.StatusCode(415);
        if (context.Request.Headers.Origin.Count == 0) return Results.StatusCode(403);
        try
        {
            var request = await JsonSerializer.DeserializeAsync<AnalyzeRequest>(context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, MaxDepth = 16 }, context.RequestAborted);
            if (request == null) return Results.BadRequest(new { error = "Thiếu dữ liệu đầu vào." });
            return Results.Json(Present(Analyze(request, context.RequestAborted)), jsonOptions);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.StatusCode(499); }
        catch (BadHttpRequestException) { return Results.StatusCode(413); }
        catch (JsonException) { return Results.BadRequest(new { error = "Dữ liệu JSON không hợp lệ." }); }
        catch (ArgumentException) { return Results.BadRequest(new { error = "Nội dung, chế độ hoặc cặp dấu không hợp lệ." }); }
    });
    Console.WriteLine($"Locus M1: http://127.0.0.1:{cli.Port}/");
    Console.WriteLine("Loopback only. Input is processed in memory; no input logs or storage. Ctrl+C to stop.");
    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is IOException)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = ex.Message }, jsonOptions));
    return 2;
}

static AnalysisResult Analyze(AnalyzeRequest request, CancellationToken cancellationToken = default)
{
    if (request.Input == null || request.Input.Length > 65536 || request.Revision < 0) throw new ArgumentException("Input limit exceeded or revision invalid.");
    InputMode mode = request.Mode switch
    {
        "explicit" => InputMode.Explicit,
        "passive" => InputMode.Passive,
        "marked" => InputMode.Markers,
        _ => throw new ArgumentException("Unknown input mode.")
    };
    var markers = new MarkerConfiguration(request.Open ?? "lc[", request.Close ?? "]");
    if (!markers.IsValid) throw new ArgumentException("Invalid marker configuration.");
    return new AnalysisEngine().Analyze(new SourceSnapshot(request.Input, request.Revision), new AnalysisOptions(mode, markers: markers), cancellationToken);
}

static object Present(AnalysisResult result) => new
{
    source = new { result.Source.Id, result.Source.Raw, result.Source.Revision },
    detection = result.Detection.ToString().ToLowerInvariant(),
    result.IsIncomplete,
    result.ContentEligibility,
    result.Diagnostics,
    versions = new { core = Versions.Core, grammar = Versions.Grammar, contract = Versions.Contract },
    regions = result.Regions.Select(region => new
    {
        region.ContentSpan, region.ReplacementSpan, region.OriginalContent, region.OriginalReplacement,
        region.ContentEligibility, region.SelectedCandidateId, region.Markers, region.Diagnostics,
        candidates = region.Candidates.Select(candidate => new
        {
            candidate.Id, candidate.Kind, candidate.Provenance, candidate.Diagnostics, candidate.Edits,
            document = candidate.Document,
            exports = CandidateExporter.Export(candidate)
        }).ToArray()
    }).ToArray()
};

internal sealed class AnalyzeRequest
{
    public string? Input { get; set; }
    public string Mode { get; set; } = "explicit";
    public string? Open { get; set; }
    public string? Close { get; set; }
    public long Revision { get; set; }
}

internal sealed class CliArguments
{
    public bool Help { get; private set; }
    public bool Serve { get; private set; }
    public int Port { get; private set; } = 4180;
    public string? Input { get; private set; }
    public string Mode { get; private set; } = "explicit";
    public string Open { get; private set; } = "lc[";
    public string Close { get; private set; } = "]";

    public static CliArguments Parse(string[] args)
    {
        var result = new CliArguments();
        for (int index = 0; index < args.Length; index++)
        {
            string Value()
            {
                if (++index >= args.Length) throw new ArgumentException("Missing command-line value.");
                return args[index];
            }
            switch (args[index])
            {
                case "--help": case "-h": result.Help = true; break;
                case "--input": result.Input = Value(); break;
                case "--mode": result.Mode = Value(); break;
                case "--open": result.Open = Value(); break;
                case "--close": result.Close = Value(); break;
                case "--serve":
                    result.Serve = true;
                    if (index + 1 < args.Length && int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int port)) { result.Port = port; index++; }
                    break;
                default: throw new ArgumentException("Unknown command-line option: " + args[index]);
            }
        }
        if (result.Port < 1024 || result.Port > 65535) throw new ArgumentException("Port must be between 1024 and 65535.");
        if (result.Serve && result.Input != null) throw new ArgumentException("Choose --serve or --input.");
        return result;
    }
}
