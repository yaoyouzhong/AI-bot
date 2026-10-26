# Windows 一键解锁（2026-10-22，本地候选）

用户要求先验证自动选择 PIN／密码，再接入 Windows；安装时默认配置全部组件，在同一页设置，并精简重复提示。Mac 不动，TAB5 保持 0.2.236-ui。本轮不刷固件、不推送、不合并主干、不打标签或发布。

## 最终流程

TAB5 从壁纸页触发一次，现有设备流程唤起登录界面，Windows 按明确保存的类型选择 PIN 或密码页，确认方式及输入焦点后，继续现有加密凭据和一次性输入，再确认实际解锁。数字密码不能推断为 PIN。失败、歧义、超时或会话变化不重试。

安装器默认调用 `--complete-unlock-setup`，补齐登录方式与 Wi-Fi 输入组件并检查连接，不增加组件选择页。单个设置页只保留开关、登录方式、两次凭据、删除和保存；保存自动补齐并验证依赖，失败不保存新凭据。已有凭据可留空只改类型。正常状态只显示一次“已就绪”，保存反馈仅“已保存”；独立 Wi-Fi 设置、组件按钮和重复通道列表已移除。

界面沿用白色、现有蓝色强调、Microsoft YaHei UI 和左对齐单列，固定底部操作、正文滚动。100/150/200% 布局检查和 PNG 复核通过。

## Windows 组件

- 独立四文件程序位于 `windows-app/AIBotLoginHelper/`，安装到 `C:\Program Files\AI-bot-LoginHelper`；目录、文件和清单归管理员所有，当前用户只读，拒绝重解析点和非管理员可写项。
- 按需 SYSTEM 任务 `AIBot-LoginMethodHelper` 无定时/锁屏触发器，不常驻、不收凭据、不监听网络。当前用户仅可读取和执行，不能修改动作。请求绑定 nonce、安装用户、活动控制台会话与短期时限。
- 核对系统 LogonUI 路径及 Winlogon 桌面，仅调用唯一入口，再确认方式标签、唯一输入框和连续焦点。结果须匹配 nonce、会话、方式与有效期，桥接才释放原加密凭据。
- UIA Name 仅作语义标签白名单分类，原文不输出；不请求 Value/Text。v3 DPAPI 记录新增明确类型，兼容 v1/v2，旧记录不猜测。当前机器按用户已确认的 PIN 设置类型，并在本机比较修改前后凭据字节一致。加密原件备份位于 Git 外的 AppData/Local/AI-bot/Backups。
- USB、BLE、Wi-Fi 都接入选择门槛。现有协议、设备键盘布局、Mac 和固件不变。

## 证据

真实安全桌面发现 PIN、密码、指纹和登录选项。首轮只切到 PIN，识别不足时主动停止；修正后 PIN→密码→PIN→密码→PIN 五次全部通过方式与焦点确认，用户也确认。调用到确认约 1.17–1.32 秒；没有提交凭据，不能当作解锁验收。

首次安装因任务不存在被映射成 FileNotFoundException 而提前退出；真实只读预检确认 0x80070002 后修正。随后实际组件安装、默认 Wi-Fi 接入、本机 RFB 检查及保留原 PIN 的类型迁移通过。普通 Explorer 环境的 status-once 通过，AppProfileGuard 保留。

当前 Windows DLL：`402ff5180085f04cc0effbb4ddc77dc737b59cb975afbc216fecbd29899d3d7c`；辅助 DLL：`02b22564c60f5b7a2be8e9ce4f426d6eef54393d72bc98bdeafe43c644e61d41`，均为本地候选。USB 实测后只同步了内嵌中英文更新日志，未更改解锁实现；正常桌面安装及 status-once 再次通过，凭据、配对和设置文件哈希保持一致，见 final-deployment.json。

Release 构建 0 警告/错误；win-x64 publish 的辅助文件复制通过。PIN/DPAPI/防重放/会话/单页界面 262 项、Wi-Fi/RFB 300 项合成检查通过。默认安装协调的缺失安装、就绪不重装、既有冲突保留、失败回滚检查通过。安装器编译及有效/篡改/404/不下载夹具通过；不执行真实运行时安装。

USB 完整真机解锁两次通过：第一次停在壁纸页直接触发；第二次停在指纹登录页直接触发，自动选择 PIN 后成功解锁。第二次按 Esc 未返回壁纸，不能记为“指纹→壁纸”场景。用户确认实际解锁，设备两条记录均为 transport=USB/result=success，WTS 两次由锁定变为已解锁，辅助组件匹配 PIN 且 Ready=true，无新的 USB RPC 中断。设备记录总耗时约 4.50／3.49 秒（含准备与输入确认）。现有凭据为 PIN；实际密码输入、一位密码均未在本轮测试。

关闭 Mac 蓝牙、保留配对并保持 USB 拔出／Windows 蓝牙关闭后，用户确认壁纸页和指纹页两种 Wi-Fi 解锁均成功。设备 attempt=5/6 都是 transport=Wi-Fi/result=success，耗时约 5.27／4.66 秒；对应 WTS 两次由锁定变为已解锁，辅助组件均为 Target=pin/Ready=true，准备耗时 821／1115 毫秒。最后一次桥接记录 unlocked，总耗时 4250 毫秒（设备计时范围不同）。现有 PIN 已通过 USB、Wi-Fi 两条完整输入链路；BLE 完整解锁、真实密码凭据和一位密码未在本轮验收。

证据位于 `artifacts/development/login-method-20261022/` 的 observation-1、selection-1/2、pin-tests-compact、wifi-tests-2、setup-tests、completion-state.json、CURRENT-HASHES.json、usb-unlock-evidence.json、wifi-unlock-evidence.json、acceptance-current.json 和 unlock-hardware.jsonl。临时探针任务已注销且可执行文件已清理，诊断保留；测试采样进程已停止，产品按需任务保留。

首次无线测试前，Windows 诊断显示 USB 已断开、Windows 蓝牙关闭、Wi-Fi 桥接正常；这只能证明 Windows 端状态，不能证明 TAB5 未连接其他电脑的蓝牙。实测设备提示“正在准备蓝牙解锁”，attempt=3 记录 BLE/preparation_timeout，未提交凭据；桥接没有收到 Wi-Fi 解锁请求。用户报告解锁失败；随后 WTS 回到桌面的来源未确认，不能据此认定自动解锁成功。Mac 是可能的蓝牙连接对象，尚未证明实际对端身份。

0.2.236-ui 的 `unlock_remote_wifi_only()` 要求设备全局 `ble_connected=false`，因此连着其他电脑的蓝牙也会阻挡 Wi-Fi 解锁分支；Windows 端“蓝牙关闭”无法证明这个设备条件。关闭 Mac 蓝牙后真实 Wi-Fi 解锁通过，与其他电脑蓝牙占用的解释一致，但未读取或证明之前的蓝牙对端身份。保存多台可信电脑没有消除这项物理连接判断限制；彻底解决需调整固件的目标电脑与解锁通道选择，本轮未修改或刷写固件，Mac 代码亦未改。日常通过 Wi-Fi 解锁 Windows 时，暂需让 TAB5 断开其他电脑的蓝牙。原 Windows 侧快照与局限分别见 wifi-prerequisite.json、unexpected-ble-attempts.json、wifi-isolation-limitation.json。

## 接口依据

- [Edit 控件 Name 与 Value 的契约](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporteditcontroltype)
- [Windows 凭据提供程序](https://learn.microsoft.com/en-us/windows/win32/secauthn/credential-providers-in-windows)
- [任务 Run 参数](https://learn.microsoft.com/en-us/windows/win32/api/taskschd/nf-taskschd-iregisteredtask-run)
- [任务访问权限](https://learn.microsoft.com/en-us/windows/win32/api/taskschd/nf-taskschd-iregisteredtask-setsecuritydescriptor)
