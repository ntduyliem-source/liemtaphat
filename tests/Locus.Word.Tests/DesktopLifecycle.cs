using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using WordApi=Microsoft.Office.Interop.Word;
using Office=Microsoft.Office.Core;

internal static class DesktopLifecycle
{
    private static readonly JavaScriptSerializer Json=new(){MaxJsonLength=1024*1024};
    private static WordApi.Application? word;
    private static WordApi.Document? doc;
    private static Office.COMAddIn? addin;
    private static Process? desktop;
    private static string directory="",clientDirectory="",desktopExe="";
    public static int Run(string[] args)
    {
        directory=Path.GetFullPath(args.Length>0?args[0]:"artifacts/w0/desktop-lifecycle-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));Directory.CreateDirectory(directory);
        desktopExe=Path.GetFullPath("src/Locus.Desktop/bin/Release/net10.0-windows/Locus.Desktop.exe");
        var results=new List<object>();int failed=0;
        try
        {
            Stage("desktop-first");StartDesktop("desktop-first");var offline=Wait(false);StartWord();var connected=Wait(true);string oldSession=Session(connected);
            addin!.Connect=false;var disconnected=Wait(false);Thread.Sleep(700);Check(!addin.Connect,"Desktop enabled disabled add-in.");
            addin.Connect=true;var reconnected=Wait(true);Check(Session(reconnected)!=oldSession,"Old session persisted.");
            CloseWord();var closed=Wait(false);StopDesktop();results.Add(new{id="desktop-first",status="PASS",offline,connected,disconnected,reconnected,closed});
            Stage("word-first");StartWord();StartDesktop("word-first");var wordFirst=Wait(true);StopDesktop();Check(addin!.Connect,"Desktop close disabled Word add-in.");
            StartDesktop("desktop-restart");var restarted=Wait(true);Check(Session(restarted)==Session(wordFirst),"Desktop restart changed Word session.");
            CloseWord();Wait(false);StopDesktop();results.Add(new{id="word-first-and-desktop-restart",status="PASS",wordFirst,restarted});
        }
        catch(Exception error){failed++;results.Add(new{id="harness",status="FAIL",error=error.ToString()});}
        finally
        {
            try{CloseWord();StopDesktop();}catch(Exception error){failed++;results.Add(new{id="cleanup",status="FAIL",error=error.ToString()});}
            File.WriteAllText(Path.Combine(directory,"report.json"),Json.Serialize(new{passed=results.Count-failed,failed,results,limits="Actual Desktop diagnostic window and installed Word add-in; not production candidate IPC or visual UI acceptance."}));
        }
        Console.WriteLine($"Desktop lifecycle: {results.Count-failed} PASS / {failed} FAIL; {directory}");return failed==0?0:1;
    }
    private static void Stage(string stage){Console.WriteLine(stage);File.WriteAllText(Path.Combine(directory,"progress.json"),Json.Serialize(new{stage,utc=DateTime.UtcNow.ToString("o")}));}
    private static void Check(bool value,string error){if(!value)throw new Exception(error);}
    private static void StartDesktop(string name)
    {
        clientDirectory=Path.Combine(directory,name);
        desktop=Process.Start(new ProcessStartInfo(desktopExe,"--word-probe \""+clientDirectory+"\""){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})!;
    }
    private static Dictionary<string,object> Wait(bool connected)
    {
        var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<12)
        {
            string file=Path.Combine(clientDirectory,"desktop.json");
            if(File.Exists(file)){var state=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(file));if((bool)state["connected"]==connected)return state;}
            if(desktop!.HasExited)throw new Exception("Desktop diagnostic process exited.");Thread.Sleep(200);
        }
        throw new TimeoutException("Desktop connection did not become "+connected);
    }
    private static string Session(Dictionary<string,object> state)=> (string)((Dictionary<string,object>)((System.Collections.ArrayList)state["connections"])[0])["session"];
    private static void StartWord()
    {
        Check(Process.GetProcessesByName("WINWORD").Length==0,"Refusing existing Word.");word=new WordApi.Application();word.Visible=true;
        addin=word.COMAddIns.Item("Locus.Word.W0");Check(addin.Connect,"Add-in did not load at Word startup.");dynamic connector=addin.Object;connector.CreateSandbox("x^2");doc=word.ActiveDocument;
        File.AppendAllText(Path.Combine(directory,"owned-processes.jsonl"),Json.Serialize(new{utc=DateTime.UtcNow.ToString("o"),wordPid=Process.GetProcessesByName("WINWORD").Single().Id,desktopPid=desktop?.Id})+"\n");
    }
    private static void CloseWord()
    {
        if(doc!=null){doc.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges);doc=null;}
        if(word!=null){Check(word.Documents.Count==0,"Unexpected document; refusing Word shutdown.");word.Quit(WordApi.WdSaveOptions.wdDoNotSaveChanges);word=null;}
    }
    private static void StopDesktop(){if(desktop!=null){if(!desktop.HasExited)desktop.Kill();desktop.WaitForExit(5000);desktop.Dispose();desktop=null;}}
}
