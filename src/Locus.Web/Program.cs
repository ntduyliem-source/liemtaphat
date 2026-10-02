using Locus.Application;
using Locus.Editor;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<EditorShell>("#app");
builder.Services.AddScoped<IAnalysisScheduler, BrowserAnalysisScheduler>();
builder.Services.AddScoped<FormulaWorkspace>();
builder.Services.AddScoped<PlotSession>();
builder.Services.AddScoped<GeometryWorkspaceModel>();
builder.Services.AddScoped<BrowserTransfers>();
builder.Services.AddScoped<IEditorFiles>(p => p.GetRequiredService<BrowserTransfers>());
builder.Services.AddScoped<IEditorClipboard>(p => p.GetRequiredService<BrowserTransfers>());
await builder.Build().RunAsync();
