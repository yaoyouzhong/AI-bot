# TAB5 .250 照片处理与无线诊断 / Photo processing and wireless diagnostics

## 现场与范围

用户确认 .249 仍然整屏闪烁，且发生在照片仍显示时。不能把成功上传、零欠载或本地预览当成真机闪屏通过。上版八倍缓存同步范围修正保留，但它没有解决这次可见症状。

四向真实 LVGL 局部刷新检查显示：编码按钮状态、上传按钮文字及等待期间，按钮区域外照片像素没有变化。JPEG 编码与 DPI 显示拷贝使用同一 DMA2D 池；新候选在照片工作线程取得 LVGL 锁，等待未完成的显示拷贝，再执行 JPEG 编码并删除编码器后释放锁。面板持续扫描现有帧，不清屏、不切 framebuffer、不增加完整照片缓冲。等待/编码失败仍释放锁和照片引用，保留重试。**并发隔离是修复候选，真机因果与闪屏消失尚未确认。**

## 无线诊断

Windows 现有 `GET /diagnostics/tab5` 本地入口按需获取当前绑定设备的诊断；不增加 LAN 未鉴权入口或用户开关。新桥接和 .250 固件配套使用：

- USB ACK、Wi-Fi 和 BLE 报告进入同一格式化器，标记来源、接收时间、采样年龄；超过 10 秒标为过期，失败保留旧记录。
- 无线请求经已有加密状态的 `connectionHealth.runtimeDiagnosticCommand` 下发，设备经现有 HMAC/AES-GCM RPC 回复 `kind=runtime-diagnostics`。
- 严格匹配当前电脑、设备、会话、请求 ID、通道和有效期；主机代次变化终止发送。BLE 仅对暂时忙/无回复做有界重试。
- 只读取数字运行状态、内存、相机、编码、上传、显示、网络等现有诊断；不传照片、录音、对话文本或密钥。
- Mac 分支未改动。Mac 开发者可按 `docs/PROTOCOL.md` 的协议接入；仅刷固件不会给旧 Mac 桥接自动增加诊断入口。

## 验证与交付

日程语音不再整段拒绝超长结果：按剩余字符/UTF-8 字节容量截断新语音后插入，保留已有名称前后文，仍由用户点击保存。普通提示与名称框左边缘对齐，录音控件使用独立位置；键盘打开时停止按钮位置保持稳定。包括 32 字边界、中文、四字节字符、光标中间插入、满名称、取消、失败和会话串扰检查。

Windows 额度诊断补充每天首末采样时间、首末周额度值及相邻日期采样边界。当前生产诊断确认 10 月 21 日为 363 条采样、4 个百分点、`partial=true`；具体时段尚待部署后读取。采样数不等于覆盖时长，不能据此推算全天量。用户已说明 Mac 缺少旧记录属预期，不实施跨电脑历史合并；重置进度已恢复，不消耗重置卡。

候选、测试、精确哈希和实际更新说明的证据保存在 `artifacts/development/tab5-photo-wireless-250-20261022/`。本文件不代表已安装或真机验收；最终状态见 `PREPARED-250.json`。保留三个照片附件、绑定、配对、Wi-Fi 和显示设置。

## English

The user confirms .249 still flashes while the captured image remains visible. Four-pose partial LVGL rendering checks find no changed photo pixels outside controls. JPEG and DPI copies share DMA2D. The candidate drains the last display copy under the LVGL lock, holds it through JPEG encoding/deletion, and releases on every failure path. Scanout continues on the existing framebuffer; no screen clear, framebuffer switch, full-frame allocation or photo-quality change. This is a concurrency isolation candidate, not confirmed physical flicker acceptance.

The existing loopback Windows diagnostic endpoint requests an authenticated Wi-Fi/BLE runtime snapshot when needed, independently of OTA. Match device, selected host, session, transport, expiring request and replay rules; stop on host-generation change. Reuse numeric exporters and format with USB; label source/time/age, mark old data stale and retain it on failure. BLE retries only transient contention within bounds. No photos, audio, conversation content or secrets are collected. Mac source is unchanged; its developer must implement the documented host counterpart. Build/simulation and physical acceptance remain separate.

Agenda speech now inserts a bounded UTF-8 prefix instead of discarding an oversized result, retaining existing text around the cursor and requiring explicit Save. Feedback aligns with the name field; recording actions have their own row/column and the stop target remains stationary. Windows quota diagnostics add per-day sample bounds and weekly values without estimating missing consumption or merging computers. October 21 is confirmed partial (363 samples, 4 observed percentage points); precise coverage remains pending deployment.


## 已安装后的验收 / Installed acceptance

- `.250` 精确镜像启动核验通过，ota_1 VALID；配套 Windows DLL 与批准哈希一致，用户从正常桌面入口启动，原资料目录、配对和历史仍在。
- 日程超长语音截断回填及提示布局：用户实测通过。
- USB 拔出时 Wi-Fi 运行诊断：取得当前 .250 实时样本。仅蓝牙选择下、Wi-Fi 后台轮询抢先的诊断超时另行修复。
- 照片添加：用户明确反馈照片仍显示时会整屏蓝闪几下，未通过。零欠载计数不能证明没有可见显示供数问题。
- 10 月 21 日采样为 17:57:26–23:59:29，61→65%；前一条有效记录为 20 日 09:42:02 的 35%。用户要求接续前次有效采样，结合重置分段；后续候选将跨日可核实差额单独标注。

Exact .250 boot and the approved Windows DLL were verified, using the original desktop profile. The user accepted agenda truncation and layout. Live Wi-Fi diagnostics worked with USB unplugged. Photo addition still causes full-screen blue flashes while the photo remains visible, so flicker acceptance failed; zero underrun counters do not establish absence. The BLE-only/standby-Wi-Fi diagnostic fallback and requested cross-day quota attribution are separate follow-up changes.
