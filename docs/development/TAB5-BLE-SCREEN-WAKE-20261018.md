# TAB5 蓝牙解锁：熄屏准备候选

状态：开发与本地验证完成，等待用户批准部署/刷机；未部署桥接、未刷机；设备仍为已验收的 `0.2.218-ble-cover`。新增熄屏场景仍待实机验收，不能将此前亮屏项目的通过扩大为整体完成。

## 现场现象与只读证据

用户最初报告：电脑 PIN 页闲置黑屏后，在 TAB5 提交 PIN 只唤醒，没有解锁，需要再输入。随后明确补充实际路径：黑屏后第一次空格先回到壁纸页，再按一次空格才到 PIN 页。采用后者作为修复的页面路径；不把亮屏通知等同于 PIN 就绪，也不把最初的“回 PIN 页”口述作为已确认页面检测证据。

限时普通用户探针实际收到本会话屏幕通知：17:12:30 On；17:13:03 会话锁定；17:13:38 Off 且锁定；17:13:43 On 且锁定；17:13:56 解锁。用户报告黑屏后约等 10 秒，通知时间仅表示进程收到事件的时间，不能据约 5 秒事件间隔推翻用户观察或声称物理熄屏时间已精确测量。探针未发送按键、未读取凭据，取样后已结束。

证据：`artifacts/development/ble-screen-off-20261018/observation-result.json` 与其 `probe/bin/Release/net8.0-windows/display-events.jsonl`。首个数字是否被唤醒消耗仍是推断，没有逐键安全桌面捕获；源码已确认 .218 没有显示电源检测，也不在提交前复查。

## 最小修复

- 复用原锁屏/解锁按钮与 PIN 面板，不另设按钮、不改变 Windows 登录方式，不保存 PIN。
- 桥接内增加一个隐藏通知窗口，使用普通用户 `RegisterPowerSettingNotification` / `GUID_SESSION_DISPLAY_STATUS`，记录内存中的 Off / On / Dim。启动未知、注册失败未知、休眠前清空；不改变电源策略、不阻止熄屏，不增加服务、驱动、账户或依赖包。
- 现有已认证的只读 `computer-control / lock-screen` 响应增加可空整数 `displayState`：0 Off、1 On、2 Dim，null 或缺失代表未知。认证、会话、时效及参数拒绝规则保持，RPC 不接受 PIN。
- 原设备锁屏壁纸检测及一次 Space 保留。只有本次新鲜响应明确 Off，才尝试一次蓝牙 Left Shift modifier 按下/松开；无数字、字符、Enter。不将 Shift 当作“已到登录页”的证据。
- 唤醒后重新读取屏幕与壁纸状态；只有明确壁纸证据允许一次 Space，随后再复查。亮屏但壁纸未知仍不是 PIN 焦点确认，面板保留用户确认 PIN 页的提示。
- 打开面板和用户明确确认提交分别执行准备。准备最多 8 秒，每次按键最多一次，仅等待亮屏的只读查询可以重复；未知、过期、失败或超时停止，不自动重发按键或 PIN。
- 点击确认后若刚好熄屏，本次 PIN 仅在设备易失内存中等待准备，未提前发送；确认新鲜 On / Dim 后才启动一次 PIN 流。取消、超时、离页、掉线或状态变化清空待提交内容。首个 PIN 按下前后端再次核对同一请求、连接及屏幕证据。
- 已亮屏的 PIN 页不发 Shift；Space 仍仅由明确壁纸证据允许。正常亮屏提交也复查一次，不靠旧面板状态直接发送。
- 新固件需匹配新桥接的屏幕字段；旧桥接或字段未知时停止准备，不退回无屏幕检查的输入。旧固件可忽略新增字段。

## 验证与边界

源码在独立 `artifacts/development/ble-screen-off-20261018/` 保存，以已部署 BLE 源为基线，不包含另一路未安装的 Wi-Fi 解锁候选。复用缓存构建时临时叠加差异文件，按哈希恢复原维护工程并使时间戳触发后续正确编译，不覆盖其中 Wi-Fi 开发。

本地验证记录需分别列出桥接构建/自测、普通用户只读通知、真实 worker/键盘后端模拟、LVGL 流程、固件构建及精确包；构建或模拟通过不等于 Shift 能在当前 Windows 真机完成唤醒。Windows 电源通知和蓝牙是异步链路，通知可能延迟，无法提供与电脑端同时操作的原子保证。此候选不声明电脑睡眠/休眠、蓝牙已断开、远程会话或其他凭据提供程序的覆盖。

完成候选后向用户展示具体 payload，再请求部署/刷机确认。首项实机仅验证“熄屏 → 唤醒 → 壁纸 → PIN 页，电脑输入为空”，不提交真实 PIN；之后按用户要求一次只验收一项。Wi-Fi 解锁组件仍不安装。

官方依据：[Power Setting GUIDs](https://learn.microsoft.com/en-us/windows/win32/power/power-setting-guids)、[RegisterPowerSettingNotification](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerpowersettingnotification)。

## 封存候选与验证结果

固件0.2.219-ble-wake，7,115,024字节（比.218增加1,456字节）；SHA-256 1d9a4a4eff3e7ef2f3b280635ebc1cd611efa72d7550f1c4094d468101a7008a，ELF 128712dd7683c8d4b75e901b226d09bd9870a98a63655485d489f8cf992db022。部署对象仅原固定运行目录的 DLL/PDB；DLL a25a2c70a8a7d7f8f09a1424a93efb394eec3ef1d826baabf73bf22959fff0ba，PDB cc0e486d21910e047bd148ec4068a2b4a7bdd2c03b38feb941465a4e677d84d4；EXE与deps/runtimeconfig相同。新增DLL体积2,560字节，无新依赖。

Windows构建、电脑控制与BLE回归、普通桌面--status-once和候选实际通知类读取、五项原生测试、完整LVGL与真实sidecar预览、固件构建及212主源码哈希一致性均通过。9个临时叠加源按哈希恢复并使时间戳失效；维护目录另一路Wi-Fi源码保留。只读探针已停止，正式latest和运行DLL仍是原.218哈希。

综合预览取样保留了静态控件/显示缓冲差异，尚未确证其根因；没有按这个现象改生产绘制代码。独立重复相同生产UI操作、直接读取显示缓冲，检查/唤醒/就绪三种状态的PIN区域、数字键和取消按钮均完整，九项像素检查与控件可见性断言通过。详见候选REVIEW.md、preview-unlock.log与visual-verification.json；完整模拟图以preview-unlock/为准，不能替代真机验证。

完整封存与回滚/验收计划：artifacts/development/ble-screen-off-20261018/REVIEW.md、candidate-review.json。当前只等待用户对该精确payload的部署与刷机批准。首项仍是不输入PIN、仅验证从黑屏自动到PIN页且两边为空；其他项目逐项进行。