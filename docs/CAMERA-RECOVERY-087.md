# TAB5 087 相机资源与提示修正

2026-10-02，086 的 A1 拍照/重拍/取消和 A2 添加/移除单次通过后，A3 再次打开相机报“相机缓冲区不足，请重新打开（6/12）”，关闭等待再开仍失败。输入框下方同时保留上次“照片已添加，Wi-Fi 0.6 秒，可与文字一起发送”。本次没有发送消息，A3 未通过。现场证据保存在 `artifacts/firmware/tab5/previews/0.2.86-ui/camera-a3-failure.json` 和 `camera-a3-reopen-failure.json`。

## 证据与修正

- stage=6/error=12 对应申请 V4L2 相机帧缓冲失败，不是上传失败。三块 1280×720 RGB565 缓冲合计 5,529,600 字节。现有绘图缓存释放逻辑保留。
- `esp_video_isp_pipeline.c` 会打开并持有相机设备；`esp_video_close()` 在仍有引用时直接返回，不销毁帧池。既有 `esp_video_setup_buffer()` 虽然下次申请时会释放旧池，但拍照间隙不能及时回收，其他长期分配可能占用或割裂其周围空闲空间。确认的是资源生命周期缺口；尚未以真机堆分布证明本次 ENOMEM 的全部来源。
- 在许可保留的 vendored `esp_video` 中补充停止状态的 `VIDIOC_REQBUFS(count=0)`，清理队列、信号量和池，不申请替代对象；采集中仍拒绝释放。相机在显示解绑、停止采集、解除映射后显式释放池，再关闭句柄。保留三缓冲、原画质、照片静态帧和已有草稿/附件，不通过降分辨率或删用户内容腾内存。
- 新一轮拍照更新底部提示；相机错误同步到输入框下方，关闭后仍保留本轮错误。正常关闭显示取消，移除附件显示“已移除照片”，新操作不再沿用上次上传成功提示。

## 验证范围

087-A3 收尾已获用户确认：设备显示会话完成状态，测试文字和照片自动清空，用户明确回复“A3收尾通过”。与实际图片/文字到达及桥接 accepted/202 证据一致，本次附件发送主链路通过。

真机 087-A3 实际送达通过：当前验收会话收到一条指定测试文字及一张可查看照片，截至核对没有重复消息。结合 C1/C2，拍照、连续添加/移除/重开及本次单张附件送达已获实机证据；设备发送回执与草稿清理仍待确认，不扩大为断线重发或长期稳定性通过。用户照片不复制进仓库或验收材料。

真机 087-C2 通过：用户确认三轮照片添加/移除/重新打开及最后一次再打开正常，移除提示正确，无 6/12。结合 C1，本次相机资源及旧提示问题在规定复测范围内通过，长期反复使用仍不作保证；A3 实际消息送达尚待确认。

真机 087-C1 通过：用户确认三轮拍照/重拍/关闭均正常，无 6/12，关闭后的提示为“已取消拍照”。尚不能据此覆盖添加照片后的重复打开；继续 C2 添加/移除循环，随后验收 A3 实际送达。

真机启动核验已通过：用户确认与桥接现场一致，087 ELF 指纹匹配、ota_1 为 VALID、previousError=0，显示错误及欠载为 0，原自动轮播保持。相机连续使用和附件实际送达仍待验，不以启动通过代替修复验收。

使用实际驱动函数的本地测试覆盖多引用下 20 次释放/重新申请、重复释放、采集时拒绝释放。相机任务测试模拟 ISP 引用使 close 不释放池，覆盖拍照、停止和各故障路径，确认显式回收且静态照片不访问已释放帧。LVGL 用真实界面逻辑覆盖旧成功 → 新相机失败 → 关闭保留错误，以及移除反馈和不发送消息。

候选版本 `0.2.87-ui`，固件统一放在 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，哈希及 ELF 指纹以同目录 manifest 为准。构建及最终镜像验证结果在候选归档中保存；安装需要本次候选的刷机授权。不能用本地验证替代 A1/A2 连续循环及 A3 真机发送验收。

最终 ESP-IDF 构建、完整 LVGL 预览、真实 sidecar 分类/列表预览、显示 IRAM 和 BLE 栈检查及两个仓库 diff 检查通过；已有 `weather_icon_create` 未使用警告保留。镜像 SHA-256 为 `5260c221680aa5164e71c62522636d7d2de10d067be445a0bc5cbb230a82e22f`，ELF 指纹为 `d76b25ed17f7f522cd65522e66e1185a80ca64f444b18048b683098743eaf711`。冻结归档 `versions/0.2.87-ui/5260c221680a-44e4f0af/` 保留镜像、ELF、构建日志及验证摘要；设备仍为 086，本次没有刷机。

待真机：启动核验 → 连续三轮拍照/重拍/关闭 → 连续三轮添加/移除/再打开 → 当前验收会话单张附件只发送一次，确认实际到达。屏幕与声音、连接模式及原循环策略保持；本次不改桥接或配对资料。

## English

18:11 部署进展：用户已自行完成升级，只读诊断确认设备运行 087，USB/BLE 在线、显示错误/欠载为 0，原展示策略保持。桥接启动核验仍显示 086，087 的 ELF/VALID 分区核验待执行；相机重复使用及 A3 实际送达也仍待验。以上“未刷机”描述属于候选准备阶段，不代表此刻现场。

Version 086 failed to reopen its camera with ENOMEM after attachment testing and retained an obsolete upload-success message. Revision 087 explicitly releases stopped V4L2 frame pools despite the ISP's retained device reference, and replaces composer feedback with the current camera/removal result. Native driver, ownership/fault-injection and LVGL tests cover resource lifetime and feedback behavior. The exact hardware allocation failure still requires post-installation verification; no device write or message send is performed by preparing this candidate.
