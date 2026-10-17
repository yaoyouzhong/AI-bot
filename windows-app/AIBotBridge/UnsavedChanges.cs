using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AIBotBridge;

internal static class UnsavedChanges
{
    private sealed class State {internal string Baseline="";}
    private static readonly ConditionalWeakTable<Form,State> States=new();
    internal static Func<Form,bool> Confirm= form=>MessageBox.Show(form,"有尚未保存的修改。放弃修改并关闭？",form.Text,MessageBoxButtons.OKCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.OK;
    internal static void TrackKnown(Form form) {
        if(form is not (Tab5TaskPinsForm or DeviceDataForm or LegacyDeviceSettingsForm or BirthdaySettingsForm or AgendaForm or Tab5DisplaySettingsForm or SettingsForm or MigratedWeather.WeatherSettingsForm))return;
        Track(form);
    }
    internal static void Track(Form form) {
        if(States.TryGetValue(form,out _))return;
        States.Add(form,new(){Baseline=Capture(form)});
        form.Shown+=(_,_)=>MarkSaved(form);
        form.FormClosing+=(_,e)=>{
            if(e.Cancel||form.DialogResult==DialogResult.OK)return;
            if(States.TryGetValue(form,out var state)&&state.Baseline!=Capture(form)&&!Confirm(form)) {
                e.Cancel=true;form.DialogResult=DialogResult.None;
            }
        };
    }
    internal static void MarkSaved(Form form){if(States.TryGetValue(form,out var state))state.Baseline=Capture(form);}
    private static string Capture(Form form) {
        if(form is BirthdaySettingsForm birthdays)return birthdays.EditFingerprint;
        var values=new List<string>();
        void Read(Control parent){foreach(Control c in parent.Controls){
            switch(c){
                case TextBox t when !t.ReadOnly:values.Add(t.Text);break;
                case CheckBox cbox:values.Add(cbox.Checked.ToString());break;
                case RadioButton radio:values.Add(radio.Checked.ToString());break;
                case ComboBox combo:values.Add(combo.Text);break;
                case NumericUpDown number:values.Add(number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));break;
                case DateTimePicker date:values.Add(date.Value.Ticks.ToString());break;
                case CheckedListBox list:values.Add(string.Join(",",list.CheckedIndices.Cast<int>()));break;
                case ListView list:values.Add(string.Join("|",list.Items.Cast<ListViewItem>().Select(i=>i.Text+":"+i.Checked)));break;
            }
            Read(c);
        }}
        Read(form);return JsonSerializer.Serialize(values);
    }
}
