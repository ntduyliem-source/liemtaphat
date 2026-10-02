using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using Locus.Application;
using Locus.Core.Export;
using Locus.Desktop.Rendering;
using Locus.Desktop.Shared;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        var output=Path.GetFullPath(args.FirstOrDefault(a=>!a.StartsWith("--"))??"artifacts/sh");var results=new List<object>();int failed=0;
        void Assert(bool value,string message){if(!value)throw new Exception(message);}
        void Check(string name,Action action){try{action();results.Add(new{name,passed=true});}catch(Exception ex){failed++;results.Add(new{name,passed=false,error=ex.Message});}}
        if(args.Contains("--clipboard-svg"))
        {
            var data=Clipboard.GetDataObject()!;using var stream=(Stream)data.GetData("image/svg+xml")!;using var memory=new MemoryStream();stream.CopyTo(memory);var bytes=memory.ToArray();var expected=File.ReadAllBytes(Path.Combine(output,"runs/desktop/formula.svg"));
            var receipt=new{status=bytes.SequenceEqual(expected)&&data.GetData(DataFormats.UnicodeText)?.ToString()==System.Text.Encoding.UTF8.GetString(expected)?"PASSED":"FAILED",svgHash=Convert.ToHexStringLower(SHA256.HashData(bytes)),formats=data.GetFormats()};File.WriteAllText(Path.Combine(output,"clipboard-svg-os.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(receipt.status);return receipt.status=="PASSED"?0:1;
        }
        if(args.Contains("--clipboard"))
        {
            var data=Clipboard.GetDataObject()!;using var stream=(Stream)data.GetData("PNG")!;using var memory=new MemoryStream();stream.CopyTo(memory);var bytes=memory.ToArray();var expected=File.ReadAllBytes(Path.Combine(output,"runs/desktop/formula.png"));var bitmap=(BitmapSource)data.GetData(DataFormats.Bitmap)!;
            var receipt=new{status=bytes.SequenceEqual(expected)?"PASSED":"FAILED",pngHash=Convert.ToHexStringLower(SHA256.HashData(bytes)),bitmap.PixelWidth,bitmap.PixelHeight,formats=data.GetFormats()};File.WriteAllText(Path.Combine(output,"clipboard-os.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(receipt.status);return receipt.status=="PASSED"?0:1;
        }
        foreach(var host in new[]{"chromium","firefox","desktop"})
        Check("read-file-from/"+host,()=>{var json=File.ReadAllText(Path.Combine(output,$"runs/{host}/cross-host.locus"));var document=(FormulaDocument)DocumentCodec.Open(json).Document!;Assert(document.State.SourceRevision==9007199254740993,"Int64 truncated");Assert(document.State.Raw.Contains("mu\u0303"),"Unicode changed");Assert(document.State.RegionIndex==1&&CandidateExporter.ToLatex(document.State.Candidate!)=="\\frac{1}{2}","Selection changed");Assert(DocumentCodec.Serialize(document)==json,"Roundtrip bytes differ");});
        Check("png-clipboard-data-keeps-bytes-and-white-bitmap",()=>{var png=File.ReadAllBytes(Path.Combine(output,"runs/desktop/formula.png"));var data=NativeClipboard.CreatePngData(png);using var stream=(Stream)data.GetData("PNG")!;using var memory=new MemoryStream();stream.CopyTo(memory);Assert(png.SequenceEqual(memory.ToArray()),"PNG altered");var bitmap=(BitmapSource)data.GetData(DataFormats.Bitmap)!;var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);Assert(pixel.All(v=>v==255),"Fallback corner not opaque white");});
        Directory.CreateDirectory(Path.Combine(output,"m2-comparison"));using var scenes=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"runs/chromium/scenes.json")));
        int index=0;foreach(var scene in scenes.RootElement.EnumerateArray())
        {
            int i=index++;Check("m2-shared-semantics/"+i,()=>{string raw=scene.GetProperty("raw").GetString()!;var candidate=AnalysisWire.Analyze(new(raw,0,new())).Regions[0].Candidates[0];var m2=FormulaRenderer.Render(candidate,new(40,2,false));Assert(m2.Latex==scene.GetProperty("latex").GetString(),"Different mathematical result");File.WriteAllText(Path.Combine(output,$"m2-comparison/{i}.svg"),m2.ToSvg());File.WriteAllBytes(Path.Combine(output,$"m2-comparison/{i}.png"),m2.ToPng());});
        }
        var report=new{status=failed==0?"PASSED":"FAILED",total=results.Count,failed,results};File.WriteAllText(Path.Combine(output,"native-integration.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{report.status,report.total,report.failed}));return failed==0?0:1;
    }
}
