using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Locus.Desktop;

// Explicit W0 diagnostic entry point. It reads the test add-in; it never launches Word,
// enables an add-in, sends source or requests any document mutation.
internal sealed class WordProbeWindow : Window
{
    private readonly CancellationTokenSource lifetime=new();
    private readonly TextBlock status=new(){Margin=new Thickness(18),FontSize=16};
    private readonly string output;
    private string last="";
    private readonly List<object> observations=[];
    internal WordProbeWindow(string outputDirectory)
    {
        output=Path.GetFullPath(outputDirectory);Directory.CreateDirectory(output);
        Title="Locus — kiểm tra kết nối Word (W0)";Width=650;Height=270;
        Content=new StackPanel{Children={new TextBlock{Text="Kết nối Word — thử nghiệm W0",FontSize=23,Margin=new Thickness(18,18,18,0)},status,new TextBlock{Text="Cửa sổ này chỉ đọc trạng thái connector thử. Đóng/mở Word hoặc ngắt/kết nối lại add-in để kiểm tra vòng đời.",Margin=new Thickness(18)}}};
        Loaded+=async(_,_)=>await Observe();Closed+=(_,_)=>lifetime.Cancel();
    }
    private async Task Observe()
    {
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                var connections=new List<object>();
                foreach(var process in Process.GetProcessesByName("WINWORD"))
                using(process)
                {
                    try
                    {
                        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(700);
                        using var client=new NamedPipeClientStream(".","Locus.W0."+process.Id,PipeDirection.In,PipeOptions.Asynchronous);
                        await client.ConnectAsync(timeout.Token);
                        using var reader=new StreamReader(client,Encoding.UTF8);var buffer=new char[2048];var text=new StringBuilder();
                        while(true)
                        {
                            int count=await reader.ReadAsync(buffer.AsMemory(),timeout.Token);if(count==0)break;
                            int end=Array.IndexOf(buffer,'\n',0,count);text.Append(buffer,0,end<0?count:end);
                            if(text.Length>65536)throw new InvalidDataException("Oversized observer reply.");if(end>=0)break;
                        }
                        using var document=JsonDocument.Parse(text.ToString());var root=document.RootElement;
                        if(root.TryGetProperty("connected",out var connected)&&connected.GetBoolean())
                            connections.Add(new{pid=process.Id,session=root.GetProperty("session").GetString(),documents=root.TryGetProperty("documents",out var documents)?documents.GetInt32():0});
                    }
                    catch(Exception error)when(error is IOException or OperationCanceledException or JsonException or InvalidOperationException){ }
                }
                string stable=JsonSerializer.Serialize(connections);
                status.Text=connections.Count==0?"Chưa kết nối Word. Desktop vẫn hoạt động độc lập.":$"Đã kết nối {connections.Count} tiến trình Word. Đang đọc trạng thái cục bộ.";
                if(stable!=last)
                {
                    last=stable;observations.Add(new{utc=DateTime.UtcNow.ToString("o"),connections});if(observations.Count>100)observations.RemoveAt(0);
                    string report=JsonSerializer.Serialize(new{desktopPid=Environment.ProcessId,connected=connections.Count>0,connections,observations});
                    string temporary=Path.Combine(output,"desktop.tmp");await File.WriteAllTextAsync(temporary,report,lifetime.Token);File.Move(temporary,Path.Combine(output,"desktop.json"),true);
                }
                await Task.Delay(500,lifetime.Token);
            }
        }
        catch(OperationCanceledException){ }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){status.Text="Không ghi được báo cáo W0: "+error.Message;}
    }
}
