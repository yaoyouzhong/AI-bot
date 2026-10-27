# TAB5 .252 合并候选 / Combined candidate

用户在 `.251` 上确认附件增删正常，但添加照片仍蓝闪、附件查看偏小；随后明确要求把另一聊天的屏保竖屏修复一并纳入。本次只修改 TAB5，沿用已安装的 Windows 桥接，Mac 不动。没有读取照片内容、修改草稿或展示设置。

## 合并来源与照片显示

屏保补丁来自 `AI-bot/artifacts/development/gallery-orientation-20261022/GALLERY-ORIENTATION-FIX.patch`，SHA-256 为 `b10ec7512b152f9334917ef30906da8a4745a0a2c5abfe510aa832ccf44a2a16`。原现场为 `.250` 竖屏解码停在输出帧申请阶段，方向传感器正常；原隔离固件构建被有意停止，不能沿用为构建成功。本次重新验证合并后的源码、原生测试和交叉编译。

- 图库最多借用相机空闲的两块整帧，排除保留照片；相机工作、编码租约或关闭失败时拒绝新借用。回收只归还所有权；重新启动相机前必须收齐全部借出帧。保持原堆分配回退，失败记录真实内存错误。
- 附件查看器按可用区域等比例放大，随四向握持布局；退出恢复原界面方向。横图预览最大 640×360，竖图最大 360×640，占用上限相同，不再把竖图先塞进带黑边的横画布。
- 删除中间附件再添加时优先复用被删除项的缓存，保留仍在草稿内的另外两张照片。照片尺寸、编码质量、三张附件上限不变。

## 蓝闪候选处理

`.251` 的三次优先级租约均成功恢复，用户仍看到蓝闪，故该措施不足以解决问题。新候选减少连续占用内存的操作：缩略图每 16 行让出一次执行时间，JPEG 输出使用同样对齐的非清零分配，首次 DMA 写入前逐段清理旧缓存，编码输入/输出缓存同步限制为 16 KiB 一段并在段间让出。保留屏幕时钟、刷新、JPEG quality 85 和原照片引用保护。

这仍是候选修复；本地测试能证明缓存范围、失败清理和画面布局，不能证明真机蓝闪已经消失。SDK 修改限于固定版本的 JPEG 补丁脚本，校验前一状态、重复应用和未知源码拒绝，不整体升级 SDK。

## 验证与交付

证据目录：`artifacts/development/tab5-followup-252-20261022/`。

- 完整 LVGL 回归通过；八幅横/竖附件四向预览生成，横屏横图与竖屏竖图已人工检查可用空间与返回按钮；三张增删、缓存保留和相机/图库往返检查通过。
- 原生测试覆盖 64 次真实内存池/备用帧交接、128 次模拟相机拍摄重开、72 次相机视口会话、196 次图库解码，以及四向传感器、旋转像素和失败重试。
- JPEG 范围和双向小段同步共 51 项检查通过；输出分配故障清理、全部 128 段、两种预览比例测试通过。图库流测试初次缺少夹具目录，补齐正确参数后 351 项跨语言和 7 项 HTTP 生产函数检查通过，原失败日志保留。
- 首次固件编译发现缓存对齐接口缺少声明，补充对应 SDK 头文件后重建；最终构建结果、镜像大小、精确哈希、补丁重放及实际更新说明预览登记在 `PREPARED-252.json`。
- 2026-10-22 经用户对精确 `.252` 镜像授权后，已通过 BLE 完成 7,175,360 字节传输，并核验运行 ELF 指纹一致、`ota_1 VALID` 和三次递增心跳。首次核验发生在启动早期，未通过；稳定后重新核验通过，原失败记录保留。未发布。
- 用户暂不在设备旁，添加照片蓝闪、附件查看大小及名画/书法横竖屏与相机切换仍待真机观察。连接方式“自动选择”暂无受支持的远程设置接口，本次未修改；绑定、配对、Wi-Fi、显示设置和草稿附件均未主动改动。启动证据见 `live-252-verified.txt`、`verify-252-stable.txt`。
- 额度历史的实际 10 月 21 日仍显示 4 个百分点问题独立保留，未将本地夹具的 30 当作现场修复结果；此候选不新增桥接修改。

## English

The user confirmed .251 add/delete works but blue flashes and an undersized attachment viewer remain, and explicitly requested integration of the adjacent gallery portrait patch. Its exact hash is recorded above. The earlier isolated firmware build was stopped and is not treated as passed; this candidate validates the combined source.

Gallery rendering borrows only idle camera frames, excludes the retained photograph and returns every loan before CSI starts. Attachment previews fit available space in all orientations, use the full portrait pixel budget and preserve retained cache entries after deletion/re-addition. Memory limits, photo dimensions/quality and three attachments remain unchanged.

The blue-flash candidate bounds cache operations to 16 KiB with yields, paces preview generation and avoids clearing a JPEG output buffer that DMA will overwrite. Existing display timing and photo ownership guards remain. Local range, cleanup and rendering tests do not establish physical flicker acceptance. Combined build/hash/patch replay and actual sidecar validation are recorded in PREPARED-252.json. On October 22, the explicitly authorized image was installed over BLE and verified against its exact ELF fingerprint, ota_1 VALID state and three advancing heartbeats. The initial early-startup verification failed; the stable retry passed and both records are retained. The user is away, so visual acceptance and the requested physical-only automatic connection setting remain pending. No publication, Mac change or new Windows installation was performed. Actual October 21 quota remains an unresolved independent issue.
