using System.Text.Json;

namespace AIBotBridge;

// Fixed metadata only. Request prompts, commands and answers never leave the owner.
internal static class CodexCompanionActivity
{
    internal static string WaitingKind(JsonElement requests)
    {
        if(requests.ValueKind!=JsonValueKind.Array||requests.GetArrayLength()==0)return "";
        string result="";
        foreach(var request in requests.EnumerateArray()) {
            string kind=Tab5CodexDesktop.Text(request,"method") switch {
                "item/tool/requestUserInput" or "item/tool/requestOptionPicker" => "answer",
                "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" or "item/permissions/requestApproval" => "confirm",
                _ => "input"
            };
            if(result.Length>0&&result!=kind)return "input";
            result=kind;
        }
        return result;
    }

    internal static int Priority(CodexLifecycleTracker.TaskActivity? value)
    {
        string state=value?.CompanionState is {Length:>0} actual?actual:value?.State??"unknown";
        if(state=="waiting")return 3;
        if(state is "failed" or "blocked"||state=="idle"&&value?.Outcome=="failed")return 2;
        return state=="working"?1:0;
    }

    internal static Tab5CodexTask Project(Tab5CodexTask task,CodexLifecycleTracker.TaskActivity? activity)=>task with {
        State=activity?.State??"unknown",
        CompanionState=activity?.CompanionState is {Length:>0} actual?actual:"unknown",
        WaitingKind=activity?.WaitingKind is {Length:>0} kind?kind:null,
        Outcome=activity?.Outcome is {Length:>0} outcome?outcome:null,
        TurnId=activity?.TurnId is {Length:>0} turn?turn:null
    };
}
