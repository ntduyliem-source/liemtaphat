using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;

namespace Locus.Desktop.Shared;

/// <summary>A second launch of the same profile requests the existing window; no second editor/Undo session.</summary>
internal sealed class DesktopInstance:IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle signal;
    private RegisteredWaitHandle? listener;
    public bool Primary {get;}
    public DesktopInstance(string profile)
    {
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName+"|"+profile.ToUpperInvariant())))[..24];
        mutex=new Mutex(false,"Local\\Locus.Desktop."+key);
        try{Primary=mutex.WaitOne(0);}catch(AbandonedMutexException){Primary=true;}
        signal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Locus.Desktop.Show."+key);
        if(!Primary)signal.Set();
    }
    public void Listen(Dispatcher dispatcher,Action show)=>listener=ThreadPool.RegisterWaitForSingleObject(signal,(_,_)=>dispatcher.BeginInvoke(show),null,Timeout.Infinite,false);
    public void Dispose(){listener?.Unregister(null);signal.Dispose();if(Primary)mutex.ReleaseMutex();mutex.Dispose();}
}
