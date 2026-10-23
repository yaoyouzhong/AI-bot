# TAB5 拍照内存与 Windows 密码输入候选（2026-10-21）

范围：Windows 桥接及配套 TAB5 固件。此记录对应最初 .235 拍照/密码范围，Mac 密码扩展另行处理。Windows 已按后续授权安装；固件尚未写入。后续多主机适配位于独立分支，见 TAB5-MULTI-HOST-20261021.md。

## 用户可见行为

- 手动解锁每次默认数字键盘，可切换小写字母、大写字母和英文符号，再回到数字键盘。
- PIN / 密码支持 1–64 个可打印英文字符，包括单个数字、字母、符号或空格；保留大小写及首尾空格。
- 保留遮罩、按住显示、一次提交、逐键松开、取消与超时清除，以及电脑实际锁定状态确认。
- USB / 蓝牙使用电脑的英文美式键盘布局，并按主机 Caps Lock LED 调整字母；Wi-Fi 使用 Windows 本地键盘适配器。
- 仍须与 Windows 当前选择的登录方式一致，不自动切换 PIN / 密码登录方式。
- 按用户要求去掉设备面板与 Windows 设置页的键盘布局备注，协议文档保留技术约定。

## 拍照修复

相机 `1/12` 表示启动内存条件或分配失败，`6/12` 表示申请/映射 CSI 帧缓冲失败。
当前源码在相机启动时清理压缩缓存，却没有等待图库 worker 退出，也没有回收图库保留的
HTTP 连接及空闲解码帧。即使取消已经生效，下载缓冲、网络资源和 worker 的退出清理仍可与
相机申请三块全分辨率帧缓冲重叠。这是静态确认的资源交接缺口；本次没有取得设备失败时的完整堆快照。

候选在相机后台任务中等待图库退出，清压缩缓存、关闭空闲 HTTP 连接，并释放已脱离 LVGL 的备用
解码帧，然后才检查内存及申请相机缓冲。等待最多四秒，可取消；超时报告“图库仍在结束”，
不把未退出 worker 的内存强制释放。网络清理不持有共享 UI mutex。
保留 1280×720、三帧所有权、JPEG 质量、用户草稿、已添加附件、配对和展示设置。

## 来源与改动位置

TAB5 基线采用本地公开交付的 `AI-bot-tab5-0.2.234-ui-source.zip`，SHA-256：
`c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890`。
没有在旧版相邻 TAB5 主 checkout 上覆盖新功能，也没有改写此前冻结的候选证据。

工作源码：`artifacts/development/camera-password-20261021/tab5-source/source/`。
Windows 改动前原件：同任务目录的 `baseline/windows/`；用于区分本次修改与既有未提交工作。
Windows 候选从当前工作区构建，其中包含任务开始前已有的改动；本次自身的改动可单独查看
同目录的 `Windows-camera-password.patch`。该 Windows 候选已于 17:57 按后续授权安装，运行资料核对一致。
协议兼容细节见 [协议说明](../PROTOCOL.md#extended-unlock-credentials--扩展解锁凭据本地候选)。

## 验证与验收边界

- Windows Release 候选构建通过，0 警告、0 错误；`TAB5_PIN_SELF_TEST_OK checks=248`、
  `WIFI_UNLOCK_SELF_TEST_OK checks=298`，加密 RPC、状态预算与连续读取/差量 ACK 回归通过。
- 原生 LVGL 完整界面回归通过；核验数字默认、关闭再打开复位、大小写/符号/空格、单字符确认和遮罩。
  Windows 设置窗 100% / 150% / 200% 字体与布局缩放模拟通过，并查看了实际生成图片；未修改系统 DPI。
- 12 个相关原生回归程序通过，涵盖相机资源交接、驱动退出、帧所有权、合成、18 次 viewport 会话、
  照片编码/附件传输、键盘状态机与后端、图库 worker 和空闲 HTTP 租约。
- 对原生 viewport 测试的既有夹具修正为真实板子的物理竖屏 + 90° 横屏配置，并新增 270° 回退检查；
  原来的无旋转夹具不能进入当前生产代码的横屏优化路径。生产 viewport 实现没有修改。
- [TAB5 源码补丁](TAB5-CAMERA-PASSWORD-235.patch) 已在干净的 .234 对应文件上应用，20 个改动文件逐字节复现。

本地原生测试覆盖全部 95 个可打印 ASCII 单字符、Caps Lock 开关、逐键松开、64 字符上限、
拒绝控制字符与清除，以及图库资源交接、阻塞退出、取消和重开。Windows 隔离测试覆盖 DPAPI、
单字符与混合凭据、USB/BLE 实际加密 RPC、旧设备拒收、RFB 字节、状态确认和界面缩放。

工具重定向环境下 `--status-once` 最初被 AppProfileGuard 拒绝；改由普通 Explorer 桌面环境执行后通过，未绕过保护。
TAB5 ESP-IDF 5.4.2 实际交叉编译通过；镜像校验和、validation hash、版本、ELF 指纹、许可文件及
全部交付文件摘要通过。已经预览与本次镜像 SHA-256 绑定的实际 sidecar 顶部及滚动状态。
预览图中的“当前版本 0.2.21-ui”为测试夹具，不代表设备实际版本。

## 本地候选

- .235 固件归档：`artifacts/firmware/tab5/versions/0.2.235-ui/28ccac53af2c-2240a1e4/aibot_tab5.bin`，7,141,200 字节。固定 latest 入口现为后续 .236 候选。
- 固件 SHA-256：`28ccac53af2c3cd64c36e672d6761997d77aca98d78ad07bce72369892cf5d8d`。
- ELF 指纹：`c554f27a51cd66d2699682f270a11af9dc86c6d9a08bdea60650e5b8adc014ff`。
- Windows 候选：`artifacts/development/camera-password-20261021/windows-build/`，0.6.10 本地构建。
- Windows DLL SHA-256：`966be7eb8a374c95f388ab92e4c1e710790a84a8397a96d97a979151b1e9189d`。
- 完整身份、源码/对象摘要和日志索引：同任务目录的 `CANDIDATE.json`、`CHANGES.json` 和 `MAIN-SOURCES.json`。
- 固件固定入口及历史归档已准备，许可和校验文件一致；`versions/TAB5` 与中英文候选更新日志同步。
- `git diff --check` 通过；没有提交、push、创建标签或公开发布。Windows 桥接已安装；没有写入设备。

真实设备尚未验收：连续打开/拍照/重拍、浏览图库后再拍照、附件上传，以及 Windows USB、BLE、
Wi-Fi 的单字符和混合密码实际解锁，均不能由本地模拟或版本号替代。

后续运行核对：Windows 最后观察到设备 .234，USB 认证 RPC 为 401 / HMAC rejected、固件传输 0 字节；18:44 COM9 已断开，只读分区核验未取得数据。原桥接恢复，配对文件与设置未变，真实拍照和一位密码解锁尚待设备接回。
