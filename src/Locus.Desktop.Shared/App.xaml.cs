using System.IO;
using System.Windows;
using Locus.Application;
using Locus.Editor;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace Locus.Desktop.Shared;

public partial class App : System.Windows.Application
{
    private DesktopInstance? instance;
    private DesktopWindow? desktopWindow;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        int profile=Array.IndexOf(e.Args,"--profile");
        string? folder=profile>=0&&profile+1<e.Args.Length?Path.GetFullPath(e.Args[profile+1]):null;
        instance=new DesktopInstance(folder??"default");
        if(!instance.Primary){Shutdown();return;}
        // Diagnostics is opt-in and restricted to loopback. Normal launches do not expose CDP.
        int debug=Array.IndexOf(e.Args,"--debug-port");
        if(debug>=0 && debug+1<e.Args.Length && int.TryParse(e.Args[debug+1],out int port) && port is >=1024 and <=65535)
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",$"--remote-debugging-port={port} --remote-debugging-address=127.0.0.1");
        var services=new ServiceCollection();services.AddWpfBlazorWebView();
        services.AddScoped<IAnalysisScheduler,NativeAnalysisScheduler>();services.AddScoped<FormulaWorkspace>();
        services.AddScoped<PlotSession>();
        services.AddScoped<GeometryWorkspaceModel>();
        services.AddScoped<IEditorFiles,NativeFiles>();services.AddScoped<IEditorClipboard,NativeClipboard>();
        var provider=services.BuildServiceProvider();
        var view=new BlazorWebView{HostPage="wwwroot/index.html",Services=provider};
        if(folder!=null)view.BlazorWebViewInitializing+=(_,args)=>args.UserDataFolder=folder;
        MainWindow=new Window{Title="Locus — Công thức",Width=440,Height=720,MinWidth=360,MinHeight=480,Content=view};
        desktopWindow=new DesktopWindow(MainWindow);instance.Listen(Dispatcher,desktopWindow.Show);
        view.RootComponents.Add(new RootComponent{Selector="#app",ComponentType=typeof(DesktopShell),Parameters=new Dictionary<string,object?>{{"DesktopWindow",desktopWindow}}});
        MainWindow.Closed+=async(_,_)=>
        {
            // PrepareClose has already saved the drafts. WebView teardown can wait
            // forever for a renderer that has just lost its native window; it must
            // not keep the profile mutex and prevent the next launch.
            try{await DisposeEditorAsync(view,provider).WaitAsync(TimeSpan.FromSeconds(2));}
            catch(Exception error){System.Diagnostics.Debug.WriteLine($"Locus editor shutdown: {error}");}
            finally{desktopWindow.Dispose();Shutdown();}
        };
        MainWindow.Show();
        int receipt=Array.IndexOf(e.Args,"--receipt");
        if(receipt>=0 && receipt+1<e.Args.Length){var path=Path.GetFullPath(e.Args[receipt+1]);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{pid=Environment.ProcessId,startTimeUtc=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("o"),executable=Environment.ProcessPath}));}
    }
    protected override void OnExit(ExitEventArgs e){desktopWindow?.Dispose();instance?.Dispose();base.OnExit(e);}
    private static async Task DisposeEditorAsync(BlazorWebView view,ServiceProvider provider)
    {
        try{await view.DisposeAsync();}
        finally{await provider.DisposeAsync();}
    }
}
