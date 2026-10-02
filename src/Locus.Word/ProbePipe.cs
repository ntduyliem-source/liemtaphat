using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Locus.Word;

// Read-only W0 IPC. No incoming document, key, window or mutation command is accepted.
internal sealed class ProbePipe:IDisposable
{
    private readonly Thread thread;
    private volatile bool disposed;
    private NamedPipeServerStream? pipe;
    public ProbePipe(string name,Func<string> state,Control dispatcher)
    {
        thread=new Thread(()=>
        {
            while(!disposed)
            {
                try
                {
                    var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
                    security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!,PipeAccessRights.FullControl,AccessControlType.Allow));
                    using(var server=new NamedPipeServerStream(name,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,65536,security))
                    {
                        pipe=server;server.WaitForConnection();if(disposed)return;
                        string value=(string)dispatcher.Invoke(state);
                        byte[] bytes=Encoding.UTF8.GetBytes(value+"\n");server.Write(bytes,0,bytes.Length);server.Flush();
                    }
                }
                catch(Exception e) when(e is IOException || e is ObjectDisposedException || e is InvalidOperationException){if(!disposed)Thread.Sleep(150);}
            }
        }){IsBackground=true,Name="Locus W0 read-only IPC"};thread.Start();
    }
    public void Dispose(){disposed=true;pipe?.Dispose();}
}
