using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Locus.Word;

public static class BadgePlacement
{
    public static Rectangle? Place(Rectangle formula,Rectangle viewport,int dpi)
    {
        if(dpi<72||dpi>768||formula.Width<=0||formula.Height<=0||viewport.Width<=0||viewport.Height<=0||!viewport.Contains(formula))return null;
        int width=(int)Math.Ceiling(42*dpi/96d),height=(int)Math.Ceiling(26*dpi/96d),gap=(int)Math.Ceiling(6*dpi/96d);
        foreach(var point in new[]{new Point(formula.Right+gap,formula.Top),new Point(formula.Left-width-gap,formula.Top),new Point(formula.Left,formula.Bottom+gap)})
        {
            var bounds=new Rectangle(point,new Size(width,height));
            if(viewport.Contains(bounds))return bounds;
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct PointApi{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo
    {public uint Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public Rect CaretRect;}
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window,ref PointApi point);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent,IntPtr child);
    public static bool TryEditorViewport(IntPtr wordWindow,out Rectangle bounds,out int dpi)
    {
        bounds=Rectangle.Empty;dpi=0;
        var info=new GuiInfo{Size=(uint)Marshal.SizeOf<GuiInfo>()};
        if(!GetGUIThreadInfo(0,ref info)||!IsChild(wordWindow,info.Focus)||!GetClientRect(info.Focus,out var rect))return false;
        var origin=new PointApi();if(!ClientToScreen(info.Focus,ref origin))return false;
        dpi=(int)GetDpiForWindow(info.Focus);
        bounds=new Rectangle(origin.X,origin.Y,rect.Right-rect.Left,rect.Bottom-rect.Top);
        return dpi>0&&bounds.Width>0&&bounds.Height>0;
    }
}
