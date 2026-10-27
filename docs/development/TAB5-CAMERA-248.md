# TAB5 .248 添加照片成功路径去除中间页

## 现场与边界

用户已安装 .247 并纠正反馈为“全部画面的闪屏”。精确启动核验通过：`ota_0 VALID`，ELF 一致，三次实时健康采样。USB 接回后获得同一启动的 .247 记录：运行 254270 ms，显示欠载 0、刷新错误 0、超过 50 ms 的扫描间隔 0，最大扫描间隔 31781 us；整屏 LVGL 绘制峰值 954324 us。照片编码成功 84468 字节，Wi-Fi 上传 2298 ms / HTTP 200。

这些记录不证明硬件绝不会闪烁；它们没有支持修改显示 DMA/背光的证据。源码确认 .247 编码完成后仍会先显示全屏添加页，等待上传，再隐藏该页返回会话。新上传阶段回归在 .247 上失败：确认到达前已退出拍后画面。这是本次修正的可复现界面行为；唯一硬件根因尚未确认。

## 实现

- 在编码和上传两个阶段都保留拍后画面。原按钮显示“处理中…”或“添加中…”，不增加提示、动画或页面。
- 照片添加成功且草稿/缩略图更新后，同一回调恢复普通界面方向、隐藏相机，直接返回会话。
- 编码失败、连接未就绪、上传失败或附件已满时才显示保留照片的原错误页，重试须手动点击，不自动重复上传。
- 仅调整 `ui_photos.h` 的流程及 `preview.c` 验证。JPEG、三帧内存、显示驱动、网络协议、绑定和显示设置不变。

## 验证与交付

- 固件 `0.2.248-ui`，7,167,872 字节；SHA-256：`5d85c031208a76c21d9b061973e1fe510065440261b9ce8e3a58b15442a5b9d6`。

- 四向真实 LVGL 回调专项通过，覆盖等待确认、方向保持、离线、编码失败、上传失败、显式重试、成功后回会话且不发送会话。合成上传中竖屏已目视检查。
- 九项相机原生回归、固件构建及完整界面回归通过，覆盖六次带等待确认的上传、三张增删、九次重开、电脑选择和无线直达。实际更新说明已目视检查，源镜像、固定 latest 和历史归档哈希一致。证据：`artifacts/development/tab5-photo-add-248-20261022/PREPARED-248.json`。
- 增量补丁为 [TAB5-CAMERA-248.patch](TAB5-CAMERA-248.patch)，基于 .247；固定交付路径仍为 `AI-bot/artifacts/firmware/tab5/latest/aibot_tab5.bin`。
- 已授权且正常升级窗口显示已运行 .248，未重复发起安装。精确镜像启动核验通过：Wi-Fi、`ota_1 VALID`、三次实时健康采样；压缩传输 4,903,775 字节/14,500 ms。首轮核验时开机约 19.6 秒尚未通过，随后通过。证据：`BOOT-248.json`。整屏闪烁真机验收待用户反馈；未提交、推送或发布。

## 真机反馈补充

用户确认 .248 添加照片“还是闪了两下”，因此闪屏验收未通过。接回 USB 后取得同一启动的最新详细采样，见 [后续排查](TAB5-CAMERA-249.md)。此前安装和启动核验结果保留，不把本地回归或错误计数为零视为闪屏修复完成。

## English

Fresh .247 USB evidence shows zero LCD underruns, flush errors and scanout gaps over 50 ms, a 31.8 ms maximum scanout interval, successful JPEG/upload, and a 954 ms maximum full-page UI render. This does not establish a unique hardware cause. The reproducible UI behavior is an unnecessary full-screen attachment page between captured review and composer. Keep the captured still through encoding and upload; after acknowledgement update the draft and restore the composer in one callback. Errors alone reveal the retained-photo retry dialog. No extra full-frame allocation, driver, codec, protocol, binding or setting change. Four-way callback/error/retry checks, nine native camera targets, firmware build and full UI regression pass. Matching actual notes were visually checked, with identical source/latest/archive image hashes. The authorized image is installed; exact live boot verification passed over Wi-Fi for ota_1 VALID, with three healthy samples. No duplicate installation was issued. Subsequent user feedback confirms two flashes remain, so physical flicker acceptance failed. Fresh same-boot USB evidence and the next investigation are recorded in TAB5-CAMERA-249.md.
