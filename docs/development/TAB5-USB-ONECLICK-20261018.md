# USB / 蓝牙一键解锁候选：0.2.224-ui

本文件记录本地候选，不代表部署或真机验收。候选目录：`artifacts/development/usb-oneclick-unlock-20261018`。保留 `.223` 的黑屏准备、取消清空、解锁成功关闭面板和无敏感信息诊断。

## 用户操作

- 未保存 PIN：在 TAB5 原锁屏／解锁按钮打开面板，每次输入 Windows 数字 PIN。USB 或蓝牙由打开面板时的连接模式确定。
- 保存 PIN：在 Windows 的应用控制设置中打开「一键解锁设置」，打开一键解锁开关，输入当前 Windows PIN 两次并保存。只有开关打开且已有有效 PIN 时才启用；关闭开关保留 PIN，单独保存 PIN 不自动打开开关。之后点 TAB5 原解锁按钮，准备屏幕后只提交一次，确认解锁后先显示「电脑已解锁」，约 0.5 秒后自动关闭。
- 两个输入框各有眼睛按钮：按住显示、松开隐藏；移出按钮、丢失鼠标捕获、失去焦点、窗口失活也隐藏。键盘可按住空格查看。普通点击不会保持明文。保存、删除或关闭清空输入并恢复隐藏。
- 修改／删除只在 Windows 已解锁的当前会话中进行。删除后恢复手动输入。电脑更换 PIN 后需要在设置中修改；不会自动尝试其他密码。
- 一键解锁授予持有已配对 TAB5 的人解锁能力。加密保存不等于第二次身份验证，Windows 同一用户进程仍处于相同信任边界。

## 实现边界

PIN 使用 Windows DPAPI CurrentUser 加密，单独保存在用户 AppData 的 `AI-bot/tab5-unlock-pin.dat`。不写普通设置、仓库、日志、备份或固件；设备仅在操作期间持有易失缓冲，完成／取消／失败后清空。开关和可选 PIN 一起存入同一份 DPAPI 加密记录，原子更新；记录绑定配对设备及配对密钥，重新配对后需重新设置。旧候选记录保留 PIN，但开关默认关闭。

使用既有 USB CDC + HID 复合设备及蓝牙 HID。USB HID 就绪要求同一 USB 连接代次内收到经 AES-GCM 验证、随机 nonce 绑定的桥接 RPC 响应，普通明文遥测不是认证依据。DTR 关闭或拔插使旧授权失效。面板固定一种输入通道，中途失败不换通道、不重发数字。

一键请求复用现有加密 RPC：仅 USB / BLE 的 `unlock-pin/read`；响应内再用设备配对密钥和请求 ID 关联加密 PIN，避免明文进入 JSON。Windows 要求当前锁屏、屏幕 On/Dim 且没有明确识别到锁屏壁纸。设备沿用现有屏幕准备和提交前探测；未知或失败停止。现有检测器不提供 PIN 输入框或焦点的绝对证明，实际目标仍是用户当前 Windows PIN 登录配置。

单次请求 ID 在桥接端防重复；设备取用一次，6 秒未取得结果则终止本次请求。取消后的迟到回复丢弃；最多一个凭据请求工作线程。Windows 自动接受末位数字后不会再向桌面发送 Enter。

不支持开机首次登录、BitLocker 或 UAC；桥接在用户会话运行时用于锁屏后的解锁。本候选没有增加 Wi-Fi 解锁服务、驱动或 TightVNC。

## 验证与验收

本地检查覆盖 DPAPI、设备绑定、修改删除、错误／篡改／重放请求、取消与迟到响应、USB 断开不重放、按键释放、自动提交不向桌面补 Enter、原有黑屏准备以及成功提示停留 500 ms 后关闭面板。WinForms 界面有 100% / 150% / 200% 的布局与字体模拟截图；这不是改变 Windows DPI 后的真机测试。

真机集中验收待部署后进行：USB 手动解锁；USB／蓝牙保存 PIN 后一键解锁及自动关面板；修改和删除；取消或拔线无额外输入。真实 PIN 只由用户在本机输入，不发送到聊天，不由自动化读取。刷机前以候选哈希与配套桥接为准确认部署。

## English summary

Local candidate only, not deployed or hardware-accepted. Reuses the lock/unlock button, USB composite HID and existing BLE HID. An independent toggle and a configured PIN are both required; turning the toggle off retains the PIN and blocks new USB/BLE credential grants. Optional one-click unlock stores a pairing-bound numeric Windows PIN with DPAPI CurrentUser on the PC, retrieves it through authenticated USB/BLE RPC with an additional request-bound encrypted envelope, and wipes transient device buffers after use or cancellation. No Wi-Fi unlock component is installed. PIN fields reveal only while held and mask on release, pointer exit, capture/focus loss or window deactivation. Save, delete and close clear the fields. Local simulations and self-tests do not replace real USB/BLE unlock acceptance.

Success feedback: the existing fresh-unlocked/peer checks remain unchanged. After confirmation, clear pending PIN input and stop input work, show 电脑已解锁, then dismiss on the first UI refresh at or after 500 ms. This adds no wait before PIN submission.

状态面板设计：一键解锁使用居中的 520 × 304 小面板，处理中显示进度环和取消按钮，异常时显示原因及关闭按钮。确认成功后，手动输入和一键解锁都只显示绿色对勾与“电脑已解锁”，不显示重复标题或关闭按钮，约 500 ms 后关闭。实际 LVGL 预览及定向回归已通过；未改变认证或成功判定条件，尚未刷机。

Status layout: the centered one-click card shows a spinner and Cancel while working, or an error and Close when recovery is needed. Confirmed success replaces manual or automatic entry with a green check and 电脑已解锁 for about 500 ms. Actual LVGL previews and targeted regression passed; authentication and success guards are unchanged. Not flashed or hardware-accepted.

控制页音量图标：左侧喇叭由 24 px 放大至 40 px，与标题“电脑音量”左对齐、与滑条垂直居中；静音状态也使用相同字号，不改变滑条位置、长度或音量操作。实际预览中正常／静音图标均完整显示。

Control-page volume icon: increased from 24 px to 40 px, left-aligned with its heading, and centered vertically with the unchanged slider. Normal and muted states render fully in actual LVGL previews; volume interaction is unchanged.
