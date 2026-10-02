using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using Drawing = System.Drawing;

// User-authorized fallback for native acceptance. The receipt pins every action
// to a test-owned process. No terminal automation, private clipboard logging,
// injected DOM events, fake file dialogs, or altered product safety guards.
internal static class Program
{
    static nint target;
    static int processId;
    static bool noActivate;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2) throw new ArgumentException("receipt.json command [args]");
            SetProcessDpiAwarenessContext((nint)(-4));
            using var receipt = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(args[0])));
            processId = receipt.RootElement.GetProperty("pid").GetInt32();
            using var process = Process.GetProcessById(processId);
            var executable = receipt.RootElement.GetProperty("executable").GetString()!;
            string? windowTitle=receipt.RootElement.TryGetProperty("windowTitle",out var titleValue)?titleValue.GetString():null;
            string? windowClass=receipt.RootElement.TryGetProperty("windowClass",out var classValue)?classValue.GetString():null;
            noActivate=receipt.RootElement.TryGetProperty("noActivate",out var noActivateValue)&&noActivateValue.GetBoolean();
            if (!string.Equals(process.MainModule!.FileName, executable, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Process receipt path mismatch.");
            if (receipt.RootElement.TryGetProperty("startTimeUtc", out var started) && Math.Abs((process.StartTime.ToUniversalTime()-DateTime.Parse(started.GetString()!, null, DateTimeStyles.AdjustToUniversal)).TotalSeconds)>1) throw new InvalidOperationException("Process receipt start time mismatch.");
            var windows = new List<nint>();
            if(args[1]=="inventory")
            {
                EnumWindows((handle, _) => {GetWindowThreadProcessId(handle,out var id);if(id==processId&&IsWindowVisible(handle))windows.Add(handle);return true;},0);
                Print(windows.Select(Summarize));return 0;
            }
            if (args[1] == "show")
            {
                EnumWindows((handle, _) => { GetWindowThreadProcessId(handle, out var id); var title = new StringBuilder(512); GetWindowText(handle, title, title.Capacity); if(id == processId && title.ToString() == args[2]) windows.Add(handle); return true; }, 0);
                if(windows.Count != 1) throw new InvalidOperationException("Expected one receipt-owned window with exact title.");
                ShowWindow(windows[0], 9); Print(Summarize(windows[0])); return 0;
            }
            for(int attempt=0;attempt<40;attempt++)
            {
                EnumWindows((handle, _) => { GetWindowThreadProcessId(handle, out var id); GetWindowRect(handle,out var box); var caption=new StringBuilder(512);GetWindowText(handle,caption,caption.Capacity); if (id == processId && IsWindowVisible(handle) && IsWindowEnabled(handle) && (windowClass!=null ? AutomationElement.FromHandle(handle).Current.ClassName==windowClass : windowTitle!=null ? caption.ToString()==windowTitle : box.Right-box.Left>150 && box.Bottom-box.Top>100)) windows.Add(handle); return true; }, 0);
                if(windows.Count>0||args[1]=="windows")break;Thread.Sleep(100);
            }
            if (args[1] == "windows") { Print(windows.Select(Summarize)); return 0; }
            // Owned modal first. Never silently target a window from another process.
            target = windows.FirstOrDefault(w => GetWindow(w, 4) != 0);
            if (target == 0) target = windows.FirstOrDefault(w => w == process.MainWindowHandle);
            if (target == 0 && windows.Count == 1) target = windows[0];
            if (target == 0) throw new InvalidOperationException("No unique visible test window.");
            var root = AutomationElement.FromHandle(target);
            switch (args[1])
            {
                case "state": Print(new { window = Summarize(target), focus = Focus(), elements = Elements(root).Select((e,i) => Describe(e,i)).ToArray() }); break;
                case "capture": Activate(); Capture(Path.GetFullPath(args[2])); Print(new { path=Path.GetFullPath(args[2]),window=Summarize(target) }); break;
                case "focus": Activate(); Find(root,args[2]).SetFocus(); Pause(); Print(new { focus=Focus() }); break;
                case "click":
                    Activate(); var element=Find(root,args[2]); var r=element.Current.BoundingRectangle;
                    if (r.IsEmpty || !element.Current.IsEnabled) throw new InvalidOperationException("Target has no usable rectangle or is disabled.");
                    MouseClick(r.X+r.Width/2,r.Y+r.Height/2);
                    if(args.Length>3){if(args[3]!="Esc")throw new ArgumentException("Only an Escape cancellation may follow a click.");Thread.Sleep(100);Keys("Esc");}
                    Pause(); Print(new { action="physical-click",focus=Focus() }); break;
                case "invoke":
                    Activate(); var button=Find(root,args[2]);
                    if (!button.TryGetCurrentPattern(InvokePattern.Pattern,out var invoke)) throw new InvalidOperationException("No InvokePattern.");
                    ((InvokePattern)invoke).Invoke(); Pause(); Print(new { action="UIA-invoke",focus=Focus() }); break;
                case "set":
                    Activate(); var edit=Find(root,args[2]); if(!edit.TryGetCurrentPattern(ValuePattern.Pattern,out var value))throw new InvalidOperationException("No ValuePattern.");
                    ((ValuePattern)value).SetValue(args[3]); Pause(); Print(new { action="UIA-value",target=Describe(edit,0) }); break;
                case "input": Activate(); RequireFocus(root); foreach(char c in args[2]) { SendKey(0,c,4);SendKey(0,c,6);Thread.Sleep(20); } Pause(); Print(new { focus=Focus() }); break;
                case "ascii": Activate(); RequireFocus(root); foreach(char c in args[2]) { short scan=VkKeyScan(c);if(scan==-1)throw new ArgumentException("Use ASCII input for physical key tests.");if((scan&256)!=0)SendKey(16,'\0',0);SendKey((ushort)(scan&255),'\0',0);SendKey((ushort)(scan&255),'\0',2);if((scan&256)!=0)SendKey(16,'\0',2);Thread.Sleep(40); } Pause(); Print(new { focus=Focus() }); break;
                case "key": Activate(); Keys(args[2]); Pause(); Print(new { focus=Focus() }); break;
                case "point": Activate(); GetWindowRect(target,out var bounds); MouseClick(bounds.Left+double.Parse(args[2],CultureInfo.InvariantCulture),bounds.Top+double.Parse(args[3],CultureInfo.InvariantCulture));Pause();Print(new { action="physical-point",focus=Focus() });break;
                case "wheel": Activate();GetWindowRect(target,out var wheelBounds);double wx=wheelBounds.Left+double.Parse(args[2],CultureInfo.InvariantCulture),wy=wheelBounds.Top+double.Parse(args[3],CultureInfo.InvariantCulture);ValidatePoint(wx,wy);SetCursorPos((int)wx,(int)wy);var wheel=new INPUT{type=0,data=new(){mouse=new(){flags=0x800,data=unchecked((uint)int.Parse(args[4]))}}};SendInput(1,[wheel],Marshal.SizeOf<INPUT>());Pause();Print(new{action="physical-wheel"});break;
                case "drag":
                    Activate(); GetWindowRect(target,out var rect); double x1=rect.Left+double.Parse(args[2],CultureInfo.InvariantCulture), y1=rect.Top+double.Parse(args[3],CultureInfo.InvariantCulture),x2=rect.Left+double.Parse(args[4],CultureInfo.InvariantCulture), y2=rect.Top+double.Parse(args[5],CultureInfo.InvariantCulture);
                    ValidatePoint(x1,y1);ValidatePoint(x2,y2);SetCursorPos((int)x1,(int)y1);Mouse(2);try{for(int i=1;i<=24;i++){SetCursorPos((int)(x1+(x2-x1)*i/24),(int)(y1+(y2-y1)*i/24));Thread.Sleep(25);}if(args.Length>6){Pause();Capture(Path.GetFullPath(args[6]));}}finally{Mouse(4);}Pause();Print(new {action="physical-drag",from=new[]{x1,y1},to=new[]{x2,y2}});break;
                default: throw new ArgumentException("Unknown native test command.");
            }
            return 0;
        }
        catch(Exception e){Print(new {error=e.ToString()});return 1;}
    }
    static object Summarize(nint w){var title=new StringBuilder(512);GetWindowText(w,title,title.Capacity);GetWindowRect(w,out var r);return new {hwnd=w.ToInt64(),title=title.ToString(),windowClass=AutomationElement.FromHandle(w).Current.ClassName,rect=new[]{r.Left,r.Top,r.Right,r.Bottom},pid=processId};}
    static object? Focus(){if(GetForegroundWindow()!=target)return new {outsideTarget=true};try{return Describe(AutomationElement.FocusedElement,-1);}catch{return null;}}
    static List<AutomationElement> Elements(AutomationElement root){var all=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);return new[]{root}.Concat(all.Cast<AutomationElement>()).ToList();}
    static AutomationElement Find(AutomationElement root,string query)
    {
        var all=Elements(root);if(query.StartsWith("index:"))return all[int.Parse(query[6..])];
        var found=all.Where(e=>query.Split('|').All(q=>q.StartsWith("id:")?e.Current.AutomationId==q[3..]:q.StartsWith("type:")?e.Current.ControlType.ProgrammaticName=="ControlType."+q[5..]:e.Current.Name==q[5..])).ToArray();
        if(found.Length!=1)throw new InvalidOperationException($"Expected exactly one {query}; found {found.Length}.");return found[0];
    }
    static object Describe(AutomationElement e,int i)
    {
        try {var c=e.Current;var r=c.BoundingRectangle;string? value=null;if(e.TryGetCurrentPattern(ValuePattern.Pattern,out var v))value=((ValuePattern)v).Current.Value;
            return new {i,name=c.Name,id=c.AutomationId,type=c.ControlType.ProgrammaticName,enabled=c.IsEnabled,offscreen=c.IsOffscreen,focused=c.HasKeyboardFocus,rect=r.IsEmpty?null:new[]{r.X,r.Y,r.Width,r.Height},value,patterns=e.GetSupportedPatterns().Select(p=>p.ProgrammaticName).ToArray()};}
        catch{return new {i,unavailable=true};}
    }
    static void Activate()
    {
        var activationTarget=noActivate?GetWindow(target,4):target;
        GetWindowThreadProcessId(activationTarget,out var activationPid);
        if(activationTarget==0||activationPid!=processId)throw new InvalidOperationException("Activation target is outside receipt-owned process.");
        var current=GetForegroundWindow();var a=GetWindowThreadProcessId(current,out _);var b=GetCurrentThreadId();
        bool attached=a!=b&&AttachThreadInput(b,a,true);
        try{SetForegroundWindow(activationTarget);BringWindowToTop(activationTarget);}finally{if(attached)AttachThreadInput(b,a,false);}
        Thread.Sleep(120);if(GetForegroundWindow()!=activationTarget)throw new InvalidOperationException("Foreground is not the test window; refusing input.");
    }
    static void RequireFocus(AutomationElement root){var focus=AutomationElement.FocusedElement;var node=focus;for(int i=0;i<60&&node!=null;i++){if(Automation.Compare(node,root))return;node=TreeWalker.RawViewWalker.GetParent(node);}throw new InvalidOperationException("Keyboard focus is outside the test window.");}
    static void ValidatePoint(double x,double y){GetWindowRect(target,out var r);if(x<r.Left||x>=r.Right||y<r.Top||y>=r.Bottom)throw new InvalidOperationException("Point outside test window.");var at=WindowFromPoint(new(){X=(int)x,Y=(int)y});if(GetAncestor(at,2)!=target)throw new InvalidOperationException("Point belongs to a different window.");}
    static void MouseClick(double x,double y){ValidatePoint(x,y);SetCursorPos((int)x,(int)y);Mouse(2);Mouse(4);}
    static void Mouse(uint flags){var input=new INPUT{type=0,data=new(){mouse=new(){flags=flags}}};if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1)throw new InvalidOperationException("Mouse SendInput failed.");}
    static void SendKey(ushort key,char scan,uint flags){var input=new INPUT{type=1,data=new(){key=new(){vk=key,scan=scan,flags=flags}}};if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1)throw new InvalidOperationException("Keyboard SendInput failed.");}
    static void Keys(string chord){var parts=chord.Split('+');var modifiers=parts.Take(parts.Length-1).Select(KeyCode).ToArray();foreach(var key in modifiers)SendKey(key,'\0',0);var last=KeyCode(parts[^1]);SendKey(last,'\0',0);SendKey(last,'\0',2);foreach(var key in modifiers.Reverse())SendKey(key,'\0',2);}
    static ushort KeyCode(string key)=>key.ToUpperInvariant() switch {"CTRL"=>17,"SHIFT"=>16,"ALT"=>18,"ENTER"=>13,"ESC"=>27,"TAB"=>9,"SPACE"=>32,"LEFT"=>37,"UP"=>38,"RIGHT"=>39,"DOWN"=>40,"HOME"=>36,"END"=>35,"BACKSPACE"=>8,"DELETE"=>46,"F4"=>115,"F5"=>116,_ when key.Length==1=>(ushort)char.ToUpperInvariant(key[0]),_=>throw new ArgumentException("Unsupported key.")};
    static void Capture(string path){GetWindowRect(target,out var r);using var bitmap=new Drawing.Bitmap(r.Right-r.Left,r.Bottom-r.Top);using(var g=Drawing.Graphics.FromImage(bitmap)){var hdc=g.GetHdc();try{if(!PrintWindow(target,hdc,2))throw new InvalidOperationException("PrintWindow failed.");}finally{g.ReleaseHdc(hdc);}}Directory.CreateDirectory(Path.GetDirectoryName(path)!);bitmap.Save(path,Drawing.Imaging.ImageFormat.Png);}
    static void Pause()=>Thread.Sleep(300);
    static void Print(object? value)=>Console.WriteLine(JsonSerializer.Serialize(value,Json));
    [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct POINT{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct INPUT{public uint type;public UNION data;}
    [StructLayout(LayoutKind.Explicit)]struct UNION{[FieldOffset(0)]public MOUSEINPUT mouse;[FieldOffset(0)]public KEYBDINPUT key;}
    [StructLayout(LayoutKind.Sequential)]struct MOUSEINPUT{public int dx,dy;public uint data,flags,time;public nuint extra;}
    [StructLayout(LayoutKind.Sequential)]struct KEYBDINPUT{public ushort vk,scan;public uint flags,time;public nuint extra;}
    delegate bool EnumCallback(nint handle,nint param);
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumCallback callback,nint param);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(nint handle,out int pid);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")]static extern bool IsWindowEnabled(nint handle);
    [DllImport("user32.dll")]static extern bool ShowWindow(nint handle,int command);
    [DllImport("user32.dll")]static extern nint GetWindow(nint handle,uint relation);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(nint handle,StringBuilder text,int count);
    [DllImport("user32.dll")]static extern bool GetWindowRect(nint handle,out RECT rect);
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(nint handle);
    [DllImport("user32.dll")]static extern bool BringWindowToTop(nint handle);
    [DllImport("user32.dll")]static extern nint GetForegroundWindow();
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]static extern bool AttachThreadInput(uint from,uint to,bool attach);
    [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")]static extern nint WindowFromPoint(POINT point);
    [DllImport("user32.dll")]static extern nint GetAncestor(nint handle,uint flags);
    [DllImport("user32.dll")]static extern uint SendInput(uint count,INPUT[] inputs,int size);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern short VkKeyScan(char c);
    [DllImport("user32.dll")]static extern bool PrintWindow(nint handle,nint hdc,uint flags);
    [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(nint context);
}
