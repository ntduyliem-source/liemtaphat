using System.IO;
using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace Locus.WebProbe.Hybrid;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Optional, loopback-only CDP for this isolated verification host; never enables it in Desktop M2.
        int index=Array.IndexOf(e.Args,"--debug-port");
        if(index>=0){int port=int.Parse(e.Args[index+1]);if(port<1024||port>65535)throw new ArgumentOutOfRangeException(nameof(port));Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",$"--remote-debugging-port={port} --remote-debugging-address=127.0.0.1");}
        // Test-only shell for sending OS/UniKey input to the actual published WASM
        // app. This branch does not initialize Blazor Hybrid or call native core.
        if(e.Args.Contains("--wasm-input-probe"))
        {
            var browserView=new Microsoft.Web.WebView2.Wpf.WebView2 {Source=new Uri("http://127.0.0.1:4181/")};
            var browserWindow=new Window {Title="Locus — Browser WASM WEB0",Width=1240,Height=850,Content=browserView};
            browserWindow.Closed+=(_,_)=>browserView.Dispose();
            MainWindow=browserWindow;browserWindow.Show();WriteReceipt(e.Args);return;
        }
        var services=new ServiceCollection();services.AddWpfBlazorWebView();
        var provider=services.BuildServiceProvider();
        var view=new BlazorWebView {HostPage="wwwroot/index.html",Services=provider};
        view.RootComponents.Add(new RootComponent {Selector="#app",ComponentType=typeof(Editor),Parameters=new Dictionary<string,object?>{{"Host","hybrid"}}});
        var window=new Window {Title="Locus — Shared editor WEB0",Width=1240,Height=850,MinWidth=760,MinHeight=600,Content=view};
        window.Closed+=async (_,_)=>{await view.DisposeAsync();provider.Dispose();};
        MainWindow=window;window.Show();
        WriteReceipt(e.Args);
    }
    private static void WriteReceipt(string[] args)
    {
        int index=Array.IndexOf(args,"--receipt");
        if(index>=0){var path=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{pid=Environment.ProcessId,startTimeUtc=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("o"),executable=Environment.ProcessPath}));}
    }
}
