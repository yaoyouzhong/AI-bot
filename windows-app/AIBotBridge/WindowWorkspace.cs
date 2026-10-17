namespace AIBotBridge;

// UI-thread owned windows. Keys include immutable device identity, never current selection.
internal sealed class WindowWorkspace
{
    private readonly Dictionary<string,Form> _windows=new();
    internal bool Activate(string key) {
        if(!_windows.TryGetValue(key,out var form)||form.IsDisposed)return false;
        SettingsWindow.Present(form);return true;
    }
    internal void Present(string key,Form form,Form owner) {
        if(_windows.TryGetValue(key,out var existing)&&!existing.IsDisposed&&existing!=form)throw new InvalidOperationException("窗口已打开。");
        if(!_windows.ContainsKey(key)||_windows[key]!=form) {
            _windows[key]=form;form.FormClosed+=(_,_)=>{if(_windows.GetValueOrDefault(key)==form)_windows.Remove(key);};
        }
        form.Owner=owner;SettingsWindow.Present(form);
    }
    internal Task<DialogResult> ShowAsync(string key,Form form,Form owner) {
        var result=new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        form.FormClosed+=(_,_)=>result.TrySetResult(form.DialogResult);
        Present(key,form,owner);return result.Task;
    }
    internal bool CloseAll() {
        foreach(var form in _windows.Values.ToArray()) {
            if(form.IsDisposed)continue;
            form.Close();
            if(!form.IsDisposed&&form.Visible){SettingsWindow.Present(form);return false;}
        }
        return true;
    }
}
