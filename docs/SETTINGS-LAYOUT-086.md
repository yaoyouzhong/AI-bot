# TAB5 086 设置布局修订 / Settings layout revision

2026-10-02。085 已完成真实启动核验，但用户认为设置页仍不协调，尤其是连接方式、下拉框、诊断入口及说明混排。086 为这一反馈的修订。17:21 用户确认已升级，只读诊断确认设备运行 086，USB/BLE/Wi-Fi 在线、亮度 75%、显示错误和欠载为 0，原自动轮播保持；启动指纹/分区核验及布局真机验收仍待完成。

## 变化

- 连接页：配对、Wi-Fi、蓝牙改为三列状态，使用独立标签和值，去除空格模拟列对齐。
- 状态区、连接方式区和诊断区分别排列；连接方式下拉框与“打开诊断”按钮统一靠右。自动选择的说明紧随连接方式，诊断说明紧随诊断入口；移除缺少对应对象的颜色图例。
- 屏保等待时间、显示模式和轮播间隔采用相同的 284 px 控件宽度与右边界；试听按钮与诊断按钮同列，文字垂直居中。亮度/音量数值继续保持在滑块前方。
- 保持现有模式选项、保存逻辑、诊断动作和升级说明渲染器。没有修改 Windows 桥接、配对或语音行为。

## 验证与交付

- 真机 086-L3：用户回复“L3通过”，内容与轮播页布局、下拉框选项、列表/上移按钮和滚动通过，原设置保持。至此 L1–L3 三页布局通过；其余设置功能和 USB 独立恢复仍按总验收清单继续。

- 真机 086-L2：用户回复“L2通过”，屏幕与声音页数值对齐、下拉框选项完整、一屏布局通过；未在本项改变设置或验收声音/屏保行为。下一项为内容与轮播布局。

- 真机 086-L1：用户回复“L1通过”，连接与网络页布局、连接方式选项完整性、诊断入口及返回通过。屏幕与声音、内容与轮播仍待逐项确认。

- ESP-IDF 构建成功；已有未使用函数 `weather_icon_create` 警告仍存在。
- 完整原生 LVGL 预览通过，涵盖诊断打开、网络删除确认/取消/保存失败、亮度/声音保存、轮播、保存反馈和升级说明。
- 检查三个设置页图片；使用真实 086 sidecar 验证更新说明分类与列表排版。
- 显示 IRAM 与 BLE 接收栈 ELF 检查通过；两个仓库 `git diff --check` 通过。
- 固定选择路径为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，准备流程见 [固件目录约定](TAB5-FIRMWARE-WORKFLOW.md)。085 归档保留，准备新固件没有操作设备。

镜像 SHA-256：`7c6999c24c5a7fe0fd29721b747057c00c921adc9a41840f8376ee2bca91e70c`。

ELF SHA-256：`21e04fb15169bf684327f48c360a83d22752bc96ab269a936444868a58c003f0`。

证据位于 `artifacts/firmware/tab5/previews/0.2.86-ui/` 和 `versions/0.2.86-ui/7c6999c24c5a-cd993bf6/`。待真机检查：启动指纹与 VALID 分区；三页视觉布局；连接方式选择与诊断入口；亮度/音量/屏保保存；显示模式和轮播控制。

## English

17:22 启动核验更新：用户确认通过，桥接返回 `verified`，上述 ELF 指纹匹配，`ota_0 / VALID`、previousError=0；显示错误/欠载为 0，USB/BLE 在线，原自动轮播保持。启动指纹/分区待验项已关闭，布局和设置交互仍待逐项确认。

The user rejected the 085 settings layout despite successful deployment. Revision 086 separates connection status, mode selection and diagnostics, gives each action its own nearby description, and aligns selectors and buttons along a common right edge. Existing behavior is preserved. The ESP-IDF build, native LVGL interactions, visual previews, actual release-notes rendering, ELF display/stack guards and whitespace checks pass. At 17:22 user confirmation and live diagnostics verified the exact 086 ELF fingerprint and VALID ota_0, with healthy links/display and the original display policy. Hardware layout and settings acceptance remain pending.
