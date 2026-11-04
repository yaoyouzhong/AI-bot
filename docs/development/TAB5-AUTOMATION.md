# TAB5 后台自动验收

仅用于已配对 USB 设备的显式本地诊断；不打开电脑前台窗口。设备须包含本轮自动操作实现，仅有同名 0.2.313 版本字符串不能证明镜像相同，应核对镜像 SHA-256。Windows 已接入相同命令/应答引擎与自检；下面的交互命令行入口目前在 Mac。

正常退出驻留桥接后，用本次签名的诊断 app 运行：

```sh
/path/to/AIBotBridge.app/Contents/MacOS/AIBotBridge --capture-tab5-screen /new/path/current.png
/path/to/AIBotBridge.app/Contents/MacOS/AIBotBridge --tab5-automation /new/path/capture-directory
```

自动操作入口从标准输入逐行读取 JSON，标准输出逐行返回结果。`ready` 返回设备逻辑宽高与 LVGL rotation；不得按缩略图或另一个方向猜坐标。输入目录及截图不能已存在。每次最多运行十分钟；输入结束或 `stop` 后关闭本次通道。

```json
{"op":"capture","file":"before.png"}
{"op":"probe"}
{"op":"tap","x":100,"y":100}
{"op":"swipe","x":1000,"y":520,"toX":1000,"toY":230,"durationMs":400}
{"op":"capture","file":"after.png"}
{"op":"stop"}
```

示例坐标仅演示格式，不能作为所有页面的测试脚本。手势省略尺寸/方向时使用最近一次 probe 的视口；可显式携带 `width`、`height`、`rotation`，变化或越界会拒绝。错误不自动重试点击。读取实际截图后再决定下一步，按原页面和配置安排返回；正常 stop 会核对展示设置前后是否一致。退出后正常启动原桥接，不对 TAB5 使用 `--restore-cycle-default`。

截图按用户的本次明确要求写入指定本地路径，可能包含设备当前公开显示内容；不写入常规诊断日志、Git 或发布包。只读阅读验收只操作阅读、分页、滚动和返回，不触发发送任务、账户操作、升级或重置。系统权限提示仍由用户处理，不能在无人值守时把等待或拒绝算作通过。

设计及协议：[自动验收设计](TAB5-AUTOMATION-DESIGN-20261104.md)、[共同协议](../PROTOCOL.md)。
