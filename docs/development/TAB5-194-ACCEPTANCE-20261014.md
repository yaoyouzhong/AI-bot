# TAB5 194 验收收尾 / Acceptance closeout

2026-10-14。此页为本轮最终状态；190–193 的失败及候选阶段保留在带日期的原始证据中，不作为当前待操作指令。

| 对象 | 身份与结果 |
| --- | --- |
| TAB5 | 0.2.194-ui；7,191,328 字节；`ota_1`、`VALID` |
| 应用 SHA-256 | `811c38d5506c0092bcf67a8c971fe03115f15c1901de0a588f211657a2096f29` |
| ELF SHA-256 | `2227cbad75d957e5282d9700cb49a2b1ebbf60ec536fd61291e068a63eb39f98` |
| 已验收桥接 DLL | 0.6.6；`f6a99f8897d0996d487cb58a46104d074f52971dacd07e489eb363e32a8c7baf` |
| 桥接源码基线 | `48afbb46fa0c8f6c90a3f79948a99997ef225c68` 加本轮限定文件修复；主目录其他未提交工作不属于此候选 |

## 已通过

- 告警提示、详情、声音、去重及阈值恢复由用户确认。
- 191 监控左侧闪屏、自动恢复、书画横竖屏信息布局、两类 TF 离线预览及轮播由用户确认。
- 193 实际启动首屏为总览由用户确认；194 首个读回页面为总览，启动身份及健康核验通过。
- 194 三轮上传／下载共六次完整 256 KiB、错误码 0。上传 10.676／15.536／13.165 秒，下载 12.740／14.706／13.281 秒；汇总 19.504／18.857 KiB/s。
- 临时 Wi-Fi 暂停持续 80.673 秒、恢复成功且未过期。本轮设备 RPC 失败计数为 0，桥接未记录 BLE 中断；末段响应确认重传成功，不声称所有分片一次投递成功。
- 配对及图库保留；连接自动选择、USB 优先、展示 auto、循环开启，原六页顺序及 15 秒间隔保持。用户确认退出设置后实体屏轮播正常。最后一次 API 采样仍为设置页，轮播的实体表现以用户确认记录为依据。
- ESP8266 PC Off 桥接修复已包含在同一候选；独立小屏会话此前通过实机验证。本轮没有重刷 ESP8266，也没有把 TAB5 验收替代小屏验收。

## 保留边界

- 多页作品上一页／下一页：旧版曾通过；本轮当天作品只有一页，用户要求后续再验。
- BLE 整包固件升级：旧版曾通过；192→193 曾中断，本轮 194 尚无完整 BLE OTA 结果，留到下一版实际升级验证。194 安装成功，但未保留可确认其实际传输通道的日志，不将其称为 BLE OTA 通过。
- BLE 速度提升有限；单轮组比较和六次短传输不能证明持续高速或长期稳定性。
- 190 Wi-Fi 写卡单次测速较旧样本慢，仅为观察记录，未确认回归原因；不将两个样本推导为确定性能缺陷。
- 本次本地提交／包准备不等于远端 CI、公开发布、附件下载验证或独立环境逐字节重建。

## 证据

本机 `artifacts/development/tab5-190-acceptance-20261014/` 保存原始构建、说明预览和安装记录；最终身份见 `deployment-194/boot-verification.json`，测速见 `deployment-194/ble-6A-194-result.json`，恢复见 `deployment-194/deployment-state.json`。原始诊断及用户资料备份不进入 Git 或发布附件。

The final installed image is 194, boot-verified in ota_1/VALID. User-confirmed monitor, artwork, TF-preview and rotation fixes passed; all six 256 KiB BLE transfers passed at aggregate 19.504/18.857 KiB/s. Pairing, galleries and original display policy were retained. Multi-page navigation, whole-image BLE OTA, sustained speed and longer stability remain separate items. Older failed/candidate snapshots are historical. Local preparation does not establish public release or independently reproducible binaries.
