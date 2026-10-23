# TAB5 .256：照片静态保留路径的显示隔离

## 现状与本轮依据

用户确认精确 .255 仍在“添加照片”时蓝闪；首次 BLE 图片上传已成功。此反馈仍作为失败依据，显示错误计数为零和预览通过均不能改变结论。

本轮复核发现：.255 的静态保留复用了实时相机 camera_begin/end。开始和恢复均调用 camera_select，再经 esp_lcd_panel_draw_bitmap 的 panel-owned framebuffer 分支执行整帧 C2M 缓存同步。虽然照片没有变化，实际仍有两次 LCD 交接。因此此前的“冻结后仍蓝闪”没有真正排除这条路径。这只证明旧实现仍含该操作，不能证明它就是蓝闪的唯一根因。

## 修改范围与契约

- 专用 FROZEN 状态只排空旧普通 DMA，暂停当前显示的重绘；LCD 保持原扫描地址。入口不申请缓冲、不访问 panel-owned framebuffer、不复制像素、不调用 LCD 选帧或 cache sync。
- 恢复只释放该静态持有状态、恢复刷新计时器，沿用暂停期间已积累的失效区域；不强制三层整屏失效。调用方在同一 LVGL 轮次完成目标界面更新。
- 实时相机的双缓冲切换保持原路径。新 FROZEN 状态不能申请相机后缓冲或提交切换，也不能被 camera_end 或别的持有者误释放。
- 普通 DMA 超时或收到错误完成令牌时不接管，保留原 DMA 和 LVGL flushing 所有权；开始失败无需 camera_end 恢复，也就不会在失败清理中重新触发整屏同步。
- 仅照片静态处理接入新 API；分辨率、quality 85/YUV420 软件 JPEG、方向、三张附件、删除补拍及所有上传协议不变。Windows/Mac 桥接本轮均未修改，沿用已安装的停止升级和 BLE 修复。

## 验证与交付

| 项目 | 结果 |
| --- | --- |
| 固件内嵌版本 | 0.2.256-ui |
| 镜像 SHA-256 | 8b63889b63502eb50e655ea757a20fee2000ff7e2a43f696bbb0486bdc732b25 |
| ELF SHA-256 | 25540b571cd687a3ba8320502c66ba63ce9d18b3eeedd573e10c99b40b5ea688 |
| 镜像大小 | 7176896 字节 |
| 原槽剩余 | 32064 字节，分区未变 |
| 源码补丁 SHA-256 | b076bf54d87acae841e902f3fe0ceac604c4cb71a221c5258d266b87677d04ba |
| 精确基线 | 已安装的 .255 / f7e32ff3da799338017b039ca0565ab5249b0bd8d3e649e2add324ce4081f48d |
| 源码重放 | 10 文件 check/apply 和 UTF-8 内容比较通过 |
| 固定交付入口 | artifacts/firmware/tab5/latest/aibot_tab5.bin |
| 部署状态 | 已刷入、ota_1 VALID；蓝闪仍失败，附件增删通过 |

实际端口函数与真实 LVGL 测试覆盖四向、脏区积累、暂停期间触摸/业务计时继续、恢复重绘、DMA 超时/迟到或错误完成、错误持有者及重复释放。LCD/cache API 为测试中的禁止调用点；静态保留及恢复调用数为零，显示缓冲内容保持不变。

生产状态机覆盖 10,000 次静态保留及实时交接，拒绝 FROZEN 中的相机切换。真实 viewport 四向像素和持有测试确认静态过程不调用 camera_begin/end、不申请控制精灵或后缓冲；开始失败的新原子契约替代了旧的“选帧后失败清理”场景，实时相机的失败/恢复检查继续保留。

21 项相关 native 回归及完整 LVGL 回归通过，包含相机内存、三张增删、软件编码、分块、BLE ATT/HCI 和 OTA 清理。最终 ELF 的静态入口/恢复反汇编分别只出现 driver data、idle drain、refresh timer、pause/resume 的命名调用，没有直接 LCD 或 cache 调用；证据位于源码工程 artifacts/development/photo-freeze-256/。这是代码行为证据，不是屏幕蓝闪已经消失的证据。

基线中 8 个文件在修改前逐字节备份；display_refresh_gate_test.c 的基线是根据修改前读取内容，对当时唯一一处模式列表追加做精确逆向重建，没有宣称该文件来自独立 .255 归档。新增测试没有旧基线。

构建日志保留既有 BSP 未用函数、ROM gdbinit 环境及分区接近容量的警告；构建成功不写成零警告。实际 sidecar 由正式桥接加载器校验归档，真实首屏/滚动说明预览另留图片。保留绑定、配对、Wi-Fi、显示设置和附件，不提交、推送或发布。

## 真机验收目标

1. 横屏与竖屏分别拍照添加，确认照片仍显示阶段是否蓝闪，记录真实耗时。
2. 三张附件删除再补拍，关闭和重开相机；相机与名画/书法屏保往返并检查两个竖向。
3. 同一候选的 BLE 升级首段中停止，确认当前 .255 与空闲状态，重新提供再安装 .256；末段交出后禁止停止。该项必须分别记录，不能用禁用按钮替代成功取消。

## English

The exact installed .255 still flashes blue during photo addition. A successful first BLE upload and zero display-error counters do not change that failure.

The .255 still path reused live-camera begin/end, which each selected a panel framebuffer through draw_bitmap and performed a full-frame cache writeback. Thus it did not isolate that operation. This identifies a remaining path, not the proven unique cause of blue flashing.

The new FROZEN owner drains existing normal DMA and pauses rendering without selecting, copying, allocating or cache-syncing a scanout framebuffer. Release resumes accumulated LVGL invalidations without forcing a full-screen redraw. Failed acquisition preserves prior ownership and needs no camera-end cleanup. Live-camera leases cannot enter or release the still owner. Image quality, four directions, three attachments, transport protocols and .255 BLE fixes are retained; bridge programs are unchanged.

Real-port/real-LVGL four-orientation and failure tests, production state ownership, 21 related native regressions, full UI regressions and the firmware build passed. The final ELF's named direct calls match the no-LCD/cache contract. Ten-file source replay passed; the refresh-gate baseline reconstruction limitation is explicit above. Warnings are retained in build evidence. The exact image is 8b63889b63502eb50e655ea757a20fee2000ff7e2a43f696bbb0486bdc732b25, ELF 25540b571cd687a3ba8320502c66ba63ce9d18b3eeedd573e10c99b40b5ea688, with 7176896 bytes and 32064 bytes free in the unchanged slot. Prepared notes are validated and previewed separately. The device was flashed via Wi-Fi and exact ELF/ota_1/VALID startup verification passed. User still observes blue flashing; photo delete/add passed. Active BLE-transfer cancellation passed, while full BLE installation disconnected at 39%. No commit, push or publication was performed.

## 已完成的停止升级实测 / Completed active-transfer stop check

用户已授权精确 .256 镜像。通过 BLE 开始传输后，在已提供 393216/7176896 字节时点击桥接“停止升级”。桥接收到停止之后新发出的设备空闲确认，显示“升级已停止，继续使用原固件”。设备仍为 .255，运行时间从1950680增至2088109毫秒，没有重启。确认通过后，重新选择同一 SHA-256 的 .256 并恢复 BLE 传输；完整安装和照片真机结果另记。诊断确认通过 USB 取得，不写成无线诊断验收。

After approval of the exact .256 image, the BLE transfer was stopped after 393216 of 7176896 bytes. A distinct post-stop diagnostic confirmed the device idle on .255, with uptime increasing from 1950680 to 2088109 ms and no reboot. The exact same image was re-offered and BLE installation restarted. Full installation and physical photo results are recorded separately. The stop confirmation used USB diagnostics and is not a wireless-only diagnostic acceptance.
## 完整 BLE 安装的中断 / Full BLE installation interruption

停止后重试同一镜像，BLE 已提供2850816/7176896字节后连接断开，进度39%。新诊断记BLE连接次数从1增至2、断开原因531，桥接最后错误为RPC特征发现COMException 0x80650008。设备运行时间持续增加至2482933毫秒，仍为.255，未发生重启。再次点击停止并取得设备空闲确认。完整BLE升级稳定性未通过，不以停止功能通过或设备未重启替代该项。当前等待用户将本机连接方式切回自动，以同一已授权镜像完成安装；未修改候选内容或哈希。

The full BLE retry disconnected after 2850816 of 7176896 bytes (39%). The connection count increased from 1 to 2, disconnect reason was 531, and the bridge recorded RPC characteristic discovery COMException 0x80650008. Uptime continued to 2482933 ms on .255 with no reboot. A second stop received a fresh idle confirmation. Full BLE installation stability did not pass; successful cancellation and absence of reboot are separate observations. Automatic connection selection is awaited to complete installation of the same approved image. Candidate bytes and hash are unchanged.