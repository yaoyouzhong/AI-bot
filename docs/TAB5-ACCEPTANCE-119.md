# TAB5 .119 本地安装验收 / Local installation acceptance

2026-10-05，用户授权后提供已内部验证的 .119，并由用户在 TAB5 开始安装、确认升级完成。没有更新桥接二进制，没有重新配对。

- 镜像 SHA-256：`522df5ab7b939aabba94a4e9d1ee7bf5e630bf57f2915c7ec8dc705b4dcd91aa`。
- ELF SHA-256：`054ef7aabf44d47f4fdb9babce43347749396d55469dd050f87d7ff84f75b368`。
- 实际安装通道 USB（transport=0），完整镜像 6,940,368 字节；USB 启动核验确认 ota_1 VALID，previousStage=9、previousError=0，设备版本与指纹一致。
- 安装任务开始至镜像校验完成 19.207 秒；准备 7.804 秒、写入 9.953 秒、等待数据 0.147 秒、校验 0.920 秒。总耗时不含完成提示和重启，并行接收时间不能重复相加。现有摘要显示解压后的完整镜像字节，不据此推断实际线上压缩字节数。
- 原始证据：`artifacts/development/tab5-119-percent/hardware/boot-verified.json`。部署收尾正常退出桥接，执行 `--restore-cycle-default` 后正常启动；原六页顺序、15 秒间隔与配对保持，最终核对记录于同目录 `final-status.json`。
- 用户真机确认“字体合适，轻触退出正常”：年度点阵预览、最终百分号字重及触摸退出通过。用户随后完成重启与闲置检查并确认“类型保留，自动进入正常”：重启后年度点阵类型保留、闲置自动进入通过。本次年度点阵屏保验收完成；上述交互与外观结论来自用户真机观察。没有提交、推送或公开发布。

The user authorized and completed the .119 USB installation. Exact ELF/version checks confirm ota_1 VALID, the complete 6,940,368-byte image and zero retained OTA error. Installation through verification took 19.207 seconds, excluding the completion dwell and reboot. The bridge binaries and pairing were unchanged. Original automatic cycling is restored and checked separately. The user confirmed the final typography and normal preview/touch exit. After rebooting and waiting for idle entry, the user also confirmed that the annual style persisted and automatic entry worked normally. The annual screensaver acceptance is complete; appearance and interaction results are user-reported hardware observations.

## Codex 会话与语音集中验收 / Codex and voice acceptance

2026-10-05，用户对本轮会话选择（包括长标题目标）、语音录音/停止/追加/长按清空、发送保护检查回复“全部通过”；本会话也收到用户注明“这只是测试发送，不用回复”的测试消息。按用户真机验收结论记录通过，不推断未提供的逐项日志或三通道覆盖。此前 .118 记录中的长标题及语音集中回归待验项由本结果更新。

用户同时指出追加输入目前另起一行，并询问应接在末尾还是换行。现场代码 `Tab5CodexComposer.AppendText` 在已有文本且末尾无换行时插入换行；这属于交互偏好讨论，不作为功能失败，也未据此修改运行行为。

The user reported that all checks in this session's Codex selection, voice drafting and send-protection acceptance passed. A user-labelled test message was also received in this conversation. This records user hardware feedback, not independently captured per-case logs or proof of coverage on every transport. The separate preference discussion about newline versus inline appending does not change the accepted runtime behavior.
