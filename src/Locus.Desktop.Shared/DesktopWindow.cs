using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Locus.Application;
using Forms=System.Windows.Forms;

namespace Locus.Desktop.Shared;

/// <summary>One Window and WebView survive compact/expanded/tray transitions.</summary>
public sealed class DesktopWindow : IEditorWindow,IDisposable
{
    private readonly Window window;
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ContextMenuStrip menu;
    private bool expanded,closing,exiting,disposed;
    private long showVersion;
    private Rect? compactBounds;
    public bool Expanded=>expanded||window.WindowState==WindowState.Maximized;
    public bool Visible=>window.IsVisible;
    public event Action? Changed;
    public Func<bool,Task<DesktopClosePreparation>>? PrepareClose {get;set;}
    public DesktopWindow(Window window)
    {
        this.window=window;
        menu=new();menu.Items.Add("Mở Locus",null,(_,_)=>Show());
        menu.Items.Add("Bung / thu gọn",null,async(_,_)=>{Show();await ToggleSizeAsync();});
        menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("Thoát Locus",null,async(_,_)=>await ExitAsync());
        tray=new(){Text="Locus — bấm để mở",Icon=System.Drawing.SystemIcons.Application,ContextMenuStrip=menu,Visible=true};
        tray.MouseClick+=(_,e)=>{if(e.Button==Forms.MouseButtons.Left)Show();};
        window.Closing+=Closing;window.StateChanged+=StateChanged;
        window.SourceInitialized+=(_,_)=>SetBounds(false);
    }
    private void Closing(object? sender,CancelEventArgs e){if(exiting)return;e.Cancel=true;_=HideAsync();}
    private void StateChanged(object? sender,EventArgs e)
    {
        if(window.WindowState==WindowState.Minimized&&!closing)_=HideAsync();
        Changed?.Invoke();
    }
    public void Show()
    {
        if(disposed||exiting)return;
        if(!window.Dispatcher.CheckAccess()){window.Dispatcher.BeginInvoke(Show);return;}
        showVersion++;
        if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
        window.Show();window.Activate();Changed?.Invoke();
    }
    public Task ToggleSizeAsync()
    {
        if(disposed||exiting||closing)return Task.CompletedTask;
        if(!Expanded)compactBounds=new(window.Left,window.Top,window.ActualWidth,window.ActualHeight);
        bool next=!Expanded;window.WindowState=WindowState.Normal;expanded=next;SetBounds(next);Changed?.Invoke();return Task.CompletedTask;
    }
    private void SetBounds(bool large)
    {
        var pixels=Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
        var dpi=VisualTreeHelper.GetDpi(window);var area=new Rect(pixels.X/dpi.DpiScaleX,pixels.Y/dpi.DpiScaleY,pixels.Width/dpi.DpiScaleX,pixels.Height/dpi.DpiScaleY);
        double width=Math.Min(large?1040:compactBounds?.Width??440,area.Width),height=Math.Min(large?820:compactBounds?.Height??720,area.Height);
        double left=large?area.Left+(area.Width-width)/2:compactBounds?.Left??area.Right-width-16;
        double top=large?area.Top+(area.Height-height)/2:compactBounds?.Top??area.Bottom-height-16;
        window.Width=width;window.Height=height;window.Left=Math.Clamp(left,area.Left,area.Right-width);window.Top=Math.Clamp(top,area.Top,area.Bottom-height);
    }
    public async Task HideAsync()
    {
        if(disposed||closing||exiting)return;closing=true;long requestedAt=showVersion;
        try
        {
            if(PrepareClose!=null&&!(await PrepareClose(false)).Ready){Show();return;}
            if(requestedAt!=showVersion)return;
            window.WindowState=WindowState.Normal;window.Hide();Changed?.Invoke();
        }
        finally{closing=false;}
    }
    public async Task ExitAsync()
    {
        if(disposed||closing||exiting)return;closing=true;
        try
        {
            var prepared=PrepareClose==null?new DesktopClosePreparation(true):await PrepareClose(true);
            if(!prepared.Ready){Show();return;}
            if(prepared.ConfirmDiscard)
            {
                Show();if(MessageBox.Show(window,"Tự lưu nháp đang tắt. Thoát sẽ mất thay đổi chưa lưu thành tệp. Thoát Locus?","Thoát Locus",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            }
            exiting=true;tray.Visible=false;window.Close();
        }
        finally{closing=false;}
    }
    public void Dispose(){if(disposed)return;disposed=true;window.Closing-=Closing;window.StateChanged-=StateChanged;tray.Dispose();menu.Dispose();Changed=null;PrepareClose=null;}
}
