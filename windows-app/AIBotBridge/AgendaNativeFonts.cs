using System.Runtime.InteropServices;

namespace AIBotBridge;

// Native edit/common controls can keep their pre-parent 96-DPI HFONT while
// managed labels render at the monitor DPI. Keep point sizes unchanged and
// bind the actual native font to each control's current DPI/handle lifetime.
internal static class AgendaNativeFonts
{
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateFont(int height,int width,int escapement,int orientation,int weight,uint italic,uint underline,uint strikeout,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr value);
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    internal static void Attach(Form form,Control[] controls) {
        var fonts=new Dictionary<Control,(IntPtr Handle,int Height,string Face)>();
        void Apply(Control c) {
            if(c.IsDisposed||!c.IsHandleCreated)return;
            int height=-(int)Math.Round(c.Font.SizeInPoints*c.DeviceDpi/72f);string face=c.Font.Name;
            if(fonts.TryGetValue(c,out var old)&&old.Height==height&&old.Face==face){SendMessage(c.Handle,0x30,old.Handle,new(1));return;}
            var font=CreateFont(height,0,0,0,c.Font.Bold?700:400,0,0,0,1,0,0,5,0,face);if(font==IntPtr.Zero)return;
            SendMessage(c.Handle,0x30,font,new(1));fonts[c]=(font,height,face);if(old.Handle!=IntPtr.Zero)DeleteObject(old.Handle);
            int minimum=(int)Math.Ceiling(c.DeviceDpi/96f*(c.Font.SizeInPoints/9.5f)*28);c.MinimumSize=new(0,minimum);
            if(c is TextBox text){text.AutoSize=false;text.Height=minimum;}
            if(c is ComboBox combo)combo.ItemHeight=Math.Max(combo.ItemHeight,-height+6);
        }
        foreach(var c in controls){c.HandleCreated+=(_,_)=>Apply(c);c.FontChanged+=(_,_)=>Apply(c);c.DpiChangedAfterParent+=(_,_)=>Apply(c);}
        form.Shown+=(_,_)=>{foreach(var c in controls)Apply(c);};
        form.Disposed+=(_,_)=>{foreach(var font in fonts.Values)DeleteObject(font.Handle);fonts.Clear();};
    }
}
