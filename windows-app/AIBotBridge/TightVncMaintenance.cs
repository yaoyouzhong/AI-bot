namespace AIBotBridge;

internal static class TightVncMaintenance
{
    internal static int UninstallPrompt() {
        var state=TightVncMachine.Inspect(TightVncPackage.Sid);
        if(!state.CanRemove)return 0;
        if(MessageBox.Show("是否同时卸载由 AI-bot 安装的 TightVNC Server？\n\n选择“否”将保留组件。Windows PIN 和设备配对均会保留。","卸载 Wi-Fi 解锁组件",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return 0;
        var result=Task.Run(()=>new TightVncController().ChangeAsync("uninstall",state.Instance,CancellationToken.None)).GetAwaiter().GetResult();
        if(!result.Ok||result.RestartRequired)MessageBox.Show(result.Message,"Wi-Fi 解锁组件",MessageBoxButtons.OK,result.Ok?MessageBoxIcon.Information:MessageBoxIcon.Warning);
        return result.Ok?0:1;
    }
}
