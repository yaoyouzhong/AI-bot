using System.Runtime.InteropServices;

namespace AIBotBridge;

// A native IME/other in-process component can leave the UI thread with a null
// cursor. Repair only after real mouse movement inside our visible controls.
internal sealed class PointerVisibility : IMessageFilter,IDisposable
{
    private bool _queued,_disposed;
    internal PointerVisibility()=>Application.AddMessageFilter(this);
    public bool PreFilterMessage(ref Message message) {
        if(_disposed||_queued||message.Msg!=0x0200 /* WM_MOUSEMOVE */)return false;
        var control=Control.FromChildHandle(message.HWnd);
        if(control is null||control.IsDisposed||!control.IsHandleCreated)return false;
        _queued=true;
        // Run after control/native IME dispatch. Idle belongs to the application,
        // so a window closing cannot strand a callback and disable later repairs.
        Application.Idle+=AfterMouse;
        return false;
    }
    private void AfterMouse(object? sender,EventArgs args){Application.Idle-=AfterMouse;_queued=false;if(!_disposed)Recover();}
    private static void Recover() {
        var info=Read();if(info is null||info.Value.Flags!=0)return;
        var control=Control.FromChildHandle(WindowFromPoint(info.Value.Position));
        if(control is null||control.IsDisposed||control.InvokeRequired||!control.Visible||
           control.FindForm() is not {Visible:true,Opacity:>0}||
           !control.ClientRectangle.Contains(control.PointToClient(info.Value.Position)))return;
        var cursor=control.UseWaitCursor?Cursors.WaitCursor:control.Cursor;
        RestoreIfHidden(info.Value.Flags,()=>Cursor.Current=cursor,()=>Read()?.Flags??-1,()=>ShowCursor(true));
    }
    internal static void RestoreIfHidden(int flags,Action setCursor,Func<int> readFlags,Func<int> showCursor) {
        // Never override touch/pen suppression or inflate an already-visible
        // cursor's display count. Setting a valid shape normally suffices.
        if(flags!=0)return;
        setCursor();if(readFlags()!=0)return;
        for(int attempts=0;attempts<16;attempts++)if(showCursor()>=0)break;
    }
    private static Info? Read(){var value=new Info{Size=Marshal.SizeOf<Info>()};return GetCursorInfo(ref value)?value:null;}
    public void Dispose(){_disposed=true;Application.Idle-=AfterMouse;Application.RemoveMessageFilter(this);}
    [StructLayout(LayoutKind.Sequential)] private struct Info {public int Size,Flags;public IntPtr Cursor;public Point Position;}
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref Info info);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern int ShowCursor(bool show);
}
