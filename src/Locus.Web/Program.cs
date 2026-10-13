using Locus.Web;
using Locus.Application;
using Locus.Editor;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<WebShell>("#app");
builder.Services.AddScoped<EditorAssetPaths>();
builder.Services.AddScoped<IAnalysisScheduler, BrowserAnalysisScheduler>();
builder.Services.AddScoped<FormulaWorkspace>();
builder.Services.AddScoped<PlotSession>();
builder.Services.AddScoped<GeometryWorkspaceModel>();
builder.Services.AddScoped<BrowserTransfers>();
builder.Services.AddScoped<IEditorFiles>(p => p.GetRequiredService<BrowserTransfers>());
builder.Services.AddScoped<IEditorClipboard>(p => p.GetRequiredService<BrowserTransfers>());
var host = builder.Build();
host.Services.GetRequiredService<EditorAssetPaths>().Initialize(
    await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string>("locusAssetRoot"));
await host.RunAsync();
