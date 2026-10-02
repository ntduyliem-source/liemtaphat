using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Locus.Word;

// Instrumentation inside the Word UI thread. Never injects keys or changes another window.
internal sealed class NativeInputState : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo
    { public uint Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public Rect CaretRect; }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X,Y; public uint Private; }
    private delegate IntPtr Hook(int code,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent,IntPtr child);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int id,Hook hook,IntPtr module,uint thread);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr window);
    [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(IntPtr window,IntPtr context);
    [DllImport("imm32.dll",CharSet=CharSet.Unicode)] private static extern int ImmGetCompositionStringW(IntPtr context,uint index,IntPtr buffer,uint length);
    private readonly Hook callback;
    private IntPtr hook;
    public bool Composing {get;private set;}
    public int Starts {get;private set;}
    public int Ends {get;private set;}
    public int Keys {get;private set;}
    public int Spaces {get;private set;}
    public int Escapes {get;private set;}
    public int Returns {get;private set;}
    public DateTime LastInputUtc {get;private set;}=DateTime.MinValue;
    public NativeInputState()
    {
        callback=Observe; hook=SetWindowsHookEx(3,callback,IntPtr.Zero,GetCurrentThreadId());
    }
    internal static bool FocusWithin(IntPtr root)
    {
        var info=new GuiInfo{Size=(uint)Marshal.SizeOf<GuiInfo>()};
        if(!GetGUIThreadInfo(0,ref info))return false;
        GetWindowThreadProcessId(GetForegroundWindow(),out uint process);
        return process==System.Diagnostics.Process.GetCurrentProcess().Id && (info.Focus==root||IsChild(root,info.Focus));
    }
    internal static void RequireWordThread(IntPtr window)
    {
        uint thread=GetWindowThreadProcessId(window,out uint process);
        if(process!=System.Diagnostics.Process.GetCurrentProcess().Id||thread!=GetCurrentThreadId())throw new InvalidOperationException("run-on-word-ui-thread");
    }
    private IntPtr Observe(int code,IntPtr w,IntPtr l)
    {
        if(code>=0 && w!=IntPtr.Zero)
        {
            var message=Marshal.PtrToStructure<Message>(l);
            if(message.Id==0x10D){Composing=true;Starts++;}
            if(message.Id==0x10E){Composing=false;Ends++;}
            if(message.Id==0x100 || message.Id==0x102 || message.Id==0x10F)LastInputUtc=DateTime.UtcNow;
            if(message.Id==0x100)Keys++;
            if(message.Id==0x100&&message.WParam.ToUInt64()==27)Escapes++;
            if(message.Id==0x100&&message.WParam.ToUInt64()==13)Returns++;
            if(message.Id==0x102 && message.WParam.ToUInt64()==32)Spaces++;
        }
        return CallNextHookEx(hook,code,w,l);
    }
    public InputObservation Read(IntPtr wordWindow)
    {
        var info=new GuiInfo{Size=(uint)Marshal.SizeOf<GuiInfo>()};
        if(!GetGUIThreadInfo(0,ref info))return new InputObservation{Reason="focus-unavailable"};
        var cls=new StringBuilder(128); GetClassName(info.Focus,cls,cls.Capacity);
        GetWindowThreadProcessId(GetForegroundWindow(),out uint process);
        bool belongs=process==System.Diagnostics.Process.GetCurrentProcess().Id && (info.Focus==wordWindow||IsChild(wordWindow,info.Focus));
        int compositionBytes=-1; IntPtr context=belongs?ImmGetContext(info.Focus):IntPtr.Zero;
        if(context!=IntPtr.Zero){try{compositionBytes=ImmGetCompositionStringW(context,8,IntPtr.Zero,0);}finally{ImmReleaseContext(info.Focus,context);}}
        bool editor=belongs && cls.ToString()=="_WwG";
        return new InputObservation{EditorFocus=editor,FocusClass=cls.ToString(),CompositionBytes=compositionBytes,
            MessageComposition=Composing,ImeStarts=Starts,ImeEnds=Ends,KeyEvents=Keys,Spaces=Spaces,HookInstalled=hook!=IntPtr.Zero,
            QuietMilliseconds=(DateTime.UtcNow-LastInputUtc).TotalMilliseconds,Escapes=Escapes,Returns=Returns,Reason=editor?"editor":"outside-editor"};
    }
    public void Dispose(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
}
public sealed class InputObservation
{
    public bool EditorFocus{get;set;} public string FocusClass{get;set;}=""; public int CompositionBytes{get;set;}=-1;
    public bool MessageComposition{get;set;} public int ImeStarts{get;set;} public int ImeEnds{get;set;} public int KeyEvents{get;set;} public int Spaces{get;set;}
    public bool HookInstalled{get;set;} public double QuietMilliseconds{get;set;} public string Reason{get;set;}="";
    public int Escapes{get;set;} public int Returns{get;set;}
    public bool SafeExplicitTrigger => EditorFocus && HookInstalled && !MessageComposition && CompositionBytes==0 && QuietMilliseconds>=250;
    public bool AutoAllowed => false; // No generic composition proof for hook-based Vietnamese input methods yet.
}
