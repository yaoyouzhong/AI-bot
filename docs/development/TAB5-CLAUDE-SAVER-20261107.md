# TAB5 Claude 屏保的唤醒、动效与闪屏定位

## 实施前设计

- 屏保沿用 1280×720 的全屏布局。底色、时钟、宠物素材、原位返回和现有触控范围保持。手动预览保留原亮度，打开预览的同一次点击不会关闭它。下一次明确触屏点击或新的 Mac 输入应当退出。
- 空闲宠物每 3.6 秒平缓抬起 12 px，再落回并短暂停顿，峰值比原尺寸大约 3.3%。以 150 ms 采样，背景固定，每秒最多约 7 次动画属性更新。总览的轻微呼吸和工作、等待、完成等动作沿用现有逻辑。
- Mac 键盘与触控板继续使用当前版本的 pcInput 心跳。TAB5 预览和自动屏保共用原有的新时间戳及序列检查。迟到、重复心跳和新桥接会话首样本不能触发退出。没有协议字段变化。

## 源码与验证

本地离屏构建从 `0.2.318-ui` 的完整源码归档 `6cc8528608c7e7ceb88fe59aadc171e07a24b33f` 接入已有会话来源和界面补丁。在同一源码树上核对 [预览唤醒补丁](TAB5-SAVER-PREVIEW-WAKE-20261107.patch) 和 [空闲动效补丁](TAB5-CLAUDE-SAVER-MOTION-20261107.patch)。两份补丁可以分别验证和部署。桥接程序与 ESP8266 不需要同步变更。

- 旧逻辑明确排除手动预览。原 LVGL 夹具重现了新 Mac 输入不会退出预览。修正后，实际「预览屏保」按钮打开预览，开启手势不会立即关闭。同样的 LVGL 输入路径在左侧、宠物中央、右侧点击能返回设置。重复、旧序列和有效新输入断言通过，自动屏保同样通过。用户报告的实体触屏失败没有在夹具中重现。
- 实际查看 1280×720 的原生渲染以及空闲动画的静止点和峰值。宠物在中央缓慢起落，时钟和左侧背景保持稳定。对 378 个原素材帧的解码与边界回归通过。新的空闲阶段检查覆盖 24 个采样步、动画循环和原总览缩放。另用 96 帧对比了左侧 96 px，空闲、工作、等待与完成四种状态的左边像素都没有变化。离屏像素检查不覆盖 LCD 扫描链路。

- 完整 ESP32-P4 固件已用固定 ESP-IDF 5.4.2 编译通过。6601 个受管依赖文件与 9 个 SDK 修改文件通过归档摘要核验。保留待发布版本 `0.2.318-ui`，本地 BIN 为 6,933,936 字节，SHA-256 为 `15fc6b8be40204607008d6f10d591493e54a6e0ad11e0c867ea0d63ce7ed369a`。最小应用分区余量约 4%。本地构建记录和离屏预览位于 `artifacts/mac-native-alignment-20261107/saver-input-diagnosis/`，该编译结果尚未作为设备升级包验收。

## 真机闪动与显示欠载

在当前正常运行的 Mac 开发桥接上，经已配对的 USB 后台诊断读取。TAB5 未切换固件。时间为北京时间，计数变化时间是桥接收到样本的时间。

| 场景 | 真机观测 |
| --- | --- |
| 退出屏保后的 89.6 秒 | 68 份有效样本，欠载保持 61，刷新错误为 0。退出后电脑唤醒计数从此前的 2 增至 3。 |
| 手动预览 | 22:06:34 进入预览并维持 79% 亮度。约 22:08:22 欠载 61→62，约 22:08:49 欠载 62→63。用户未留意这两次是否肉眼可见。 |
| 用户两次报告闪动 | 首次报告后新鲜样本读到 64，第二次报告后读到 65。两次均处于同一手动预览，刷新错误为 0，亮度保持 79%。在此前后还有一次未收到视觉报告的欠载，计数到 66。 |

LCD 读仲裁及 QoS 始终为 15/15。详细样本中的帧间断、Flash guard 和看门狗计数没有变化。显示驱动的欠载中断和两次可见闪动落在相邻采样窗口，支持优先排查显示扫描供数。现有计数无法标出具体是哪次绘制、旋转或并发内存访问触发的欠载。降低空闲动画的更新频率已实现，闪动是否减少需要用新固件在同一设备和屏保下对照。

## English

The source patches allow verified host activity to dismiss the screensaver preview and make Claude's idle motion more visible at a lower update rate. Native LVGL pointer, frame and visual checks passed. Paired USB diagnostics recorded LCD underruns close to both user-reported flashes, with unchanged backlight and zero flush errors. The updated behavior and its effect on LCD underruns require separate physical acceptance.
