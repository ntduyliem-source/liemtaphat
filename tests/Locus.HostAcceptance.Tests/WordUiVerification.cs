using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

// Fixture setup and read-only observations are COM. All connector commands,
// input and badge clicks in this walkthrough come from the native UI driver.
internal static class WordUiVerification
{
    internal static int Run(string output)
    {
        output=Path.GetFullPath(output);
        if(Directory.Exists(output)||Process.GetProcessesByName("WINWORD").Length!=0)throw new InvalidOperationException("Existing Word or evidence directory.");
        Directory.CreateDirectory(output);
        dynamic word=MicrosoftWordHost.Create();
        dynamic doc=word.Documents.Add();
        doc.Content.Text=File.ReadAllText(Path.Combine(Path.GetDirectoryName(output)!,"word-source.txt"));
        string document=Path.Combine(output,"word-microsoft-native.docx");
        doc.SaveAs2(FileName:document,AddToRecentFiles:false);word.Visible=true;
        doc.Range(0,0).Select();word.ActiveWindow.View.Zoom.Percentage=100;
        var owned=Process.GetProcessesByName("WINWORD").Single();
        File.WriteAllText(Path.Combine(output,"word-receipt.json"),JsonSerializer.Serialize(new{pid=owned.Id,startTimeUtc=owned.StartTime.ToUniversalTime().ToString("o"),executable=owned.MainModule!.FileName}));
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
        void Snapshot(string name)
        {
            object? scan=null;
            string? connectorError=null;
            try{string state=word.COMAddIns.Item("Locus.Word.Manual").Object.GetScanState();scan=JsonSerializer.Deserialize<JsonElement>(state);}catch(Exception e){connectorError=e.Message;}
            File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(new{path=(string)word.Path,version=(string)word.Version,build=(string)word.Build,document,text=(string)doc.Content.Text,math=(int)doc.OMaths.Count,controls=(int)doc.ContentControls.Count,zoom=(int)word.ActiveWindow.View.Zoom.Percentage,selection=new{start=(int)word.Selection.Start,end=(int)word.Selection.End},scan,connectorError},new JsonSerializerOptions{WriteIndented=true}));
        }
        timer.Tick+=(_,_)=>
        {
            string commandPath=Path.Combine(output,"command.json");if(!File.Exists(commandPath))return;
            using var json=JsonDocument.Parse(File.ReadAllText(commandPath));var command=json.RootElement;File.Delete(commandPath);
            string action=command.GetProperty("action").GetString()!,name=command.GetProperty("name").GetString()!;
            if(Path.GetFileName(name)!=name)throw new ArgumentException("Report name must be a filename.");
            try
            {
                if((string)word.ActiveDocument.FullName!=document)throw new InvalidOperationException("Active document changed; refusing action.");
                if(action=="close")
                {
                    Snapshot(name);doc.Save();timer.Stop();doc.Close(false);
                    if((int)word.Documents.Count==0)word.Quit(false);
                    Marshal.FinalReleaseComObject(doc);Marshal.FinalReleaseComObject(word);app.Shutdown();return;
                }
                if(action=="zoom")word.ActiveWindow.View.Zoom.Percentage=command.GetProperty("value").GetInt32();
                else if(action=="save")doc.Save();
                else if(action!="snapshot")throw new ArgumentException("Unknown command.");
                Snapshot(name);
            }
            catch(Exception e){File.WriteAllText(Path.Combine(output,name+"-error.json"),JsonSerializer.Serialize(new{error=e.ToString()}));}
        };
        Snapshot("ready");timer.Start();return app.Run();
    }
}
