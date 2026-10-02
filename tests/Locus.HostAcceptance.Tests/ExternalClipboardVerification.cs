using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Holds the pre-test clipboard in memory while a separate, authorized UI driver
// clicks the real editor buttons. Only explicit synthetic captures are persisted.
internal static class ExternalClipboardVerification
{
    internal static int Run(string output)
    {
        output=Path.GetFullPath(output);
        if(Directory.Exists(output)||Process.GetProcessesByName("WINWORD").Length!=0)throw new InvalidOperationException("Existing report directory or Word process.");
        Directory.CreateDirectory(output);
        var backup=new DataObject();var existing=Clipboard.GetDataObject();
        if(existing!=null)foreach(var format in existing.GetFormats(false))
        {
            object? value=existing.GetData(format,false);
            object clone=value switch{string s=>s,byte[] b=>b.ToArray(),MemoryStream m=>new MemoryStream(m.ToArray()),BitmapSource b=>b.Clone(),string[] a=>a.ToArray(),_=>throw new InvalidOperationException("Cannot preserve current clipboard.")};
            backup.SetData(format,clone,false);
        }
        dynamic word=MicrosoftWordHost.Create();word.Visible=false;
        var wordHost=new{path=(string)word.Path,version=(string)word.Version,build=(string)word.Build};
        dynamic anchor=word.Documents.Add();anchor.Content.Text="Locus external clipboard test anchor";
        var checks=new List<object>();int failed=0;bool restored=false;
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
        var commandPath=Path.Combine(output,"command.json");
        void Finish()
        {
            timer.Stop();
            try{if((int)word.Documents.Count==1)word.Quit(false);else throw new InvalidOperationException("Unexpected Word document, leaving it open.");Marshal.FinalReleaseComObject(anchor);Marshal.FinalReleaseComObject(word);}catch{failed++;}
            try{Clipboard.SetDataObject(backup,true);restored=true;}catch{failed++;}
            File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(new{status=failed==0?"PASSED":"FAILED",total=checks.Count,failed,clipboardRestored=restored,wordHost,checks,scope="Actual editor Copy UI invoked externally; Windows clipboard then consumed through verified Microsoft Word COM. No synthetic replacement before capture."},new JsonSerializerOptions{WriteIndented=true}));
            app.Shutdown(failed==0?0:1);
        }
        timer.Tick+=(_,_)=>
        {
            if(!File.Exists(commandPath))return;
            using var json=JsonDocument.Parse(File.ReadAllText(commandPath));var command=json.RootElement;File.Delete(commandPath);
            if(command.GetProperty("kind").GetString()=="stop"){Finish();return;}
            string kind=command.GetProperty("kind").GetString()!, name=command.GetProperty("name").GetString()!;
            if(Path.GetFileName(name)!=name)throw new ArgumentException("Capture name must be a filename.");
            try
            {
                if(kind=="text")
                {
                    string text=Clipboard.GetText();string expected=command.GetProperty("expected").GetString()!;
                    if(text!=expected)throw new InvalidOperationException("Clipboard text differs from the expected UI selection.");
                    File.WriteAllText(Path.Combine(output,name+".txt"),text);
                    dynamic doc=word.Documents.Add();try{doc.Content.Paste();string actual=doc.Content.Text;if(actual.Replace("\r","\n").TrimEnd('\n')!=expected.Replace("\r","\n").TrimEnd('\n'))throw new InvalidOperationException("Word pasted different text.");doc.SaveAs2(FileName:Path.Combine(output,name+".docx"));}finally{doc.Close(false);Marshal.FinalReleaseComObject(doc);}
                }
                else
                {
                    string format=kind=="png"?"PNG":"image/svg+xml";var data=Clipboard.GetDataObject()!;var payload=data.GetData(format,false);byte[] bytes;
                    if(payload is byte[] array)bytes=array;else if(payload is Stream stream){using var memory=new MemoryStream();stream.CopyTo(memory);bytes=memory.ToArray();}else throw new InvalidOperationException("Expected clipboard image format missing.");
                    if(kind=="svg"&&!Encoding.UTF8.GetString(bytes).Contains("<svg"))throw new InvalidOperationException("Invalid SVG.");
                    File.WriteAllBytes(Path.Combine(output,name+"."+kind),bytes);
                    if(kind=="png")
                    {
                        dynamic doc=word.Documents.Add();string path=Path.Combine(output,name+".docx");try{doc.Content.Paste();if((int)doc.InlineShapes.Count!=1)throw new InvalidOperationException("Word did not paste one image.");doc.SaveAs2(FileName:path);}finally{doc.Close(false);Marshal.FinalReleaseComObject(doc);}
                        dynamic reopened=word.Documents.Open(FileName:path,ReadOnly:true,AddToRecentFiles:false);try{if((int)reopened.InlineShapes.Count!=1)throw new InvalidOperationException("Image lost after reopen.");}finally{reopened.Close(false);Marshal.FinalReleaseComObject(reopened);}
                    }
                }
                checks.Add(new{name,kind,passed=true});
                File.WriteAllText(Path.Combine(output,name+"-check.json"),JsonSerializer.Serialize(new{name,kind,passed=true}));
            }
            catch(Exception e){failed++;checks.Add(new{name,kind,passed=false,error=e.Message});File.WriteAllText(Path.Combine(output,name+"-check.json"),JsonSerializer.Serialize(new{name,kind,passed=false,error=e.Message}));}
        };
        File.WriteAllText(Path.Combine(output,"ready.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,ready=true,wordHost}));timer.Start();
        return app.Run();
    }
}
