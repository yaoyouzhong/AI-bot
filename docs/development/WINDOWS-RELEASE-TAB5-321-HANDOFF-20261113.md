# Windows 发布交接｜TAB5 图库批量同步

## 接手源码与范围

Windows 端是本轮唯一发布负责人。本次 Mac 交付授权覆盖本地提交和推送交接分支；本轮没有创建标签、上传候选或公开 Release。

```powershell
git fetch origin main codex/mac-tab5-321-gallery-sync-20261113
git log --oneline origin/main..origin/codex/mac-tab5-321-gallery-sync-20261113
python scripts/release_pipeline.py scope --online
```

- 交接分支：[codex/mac-tab5-321-gallery-sync-20261113](https://github.com/yaoyouzhong/AI-bot/tree/codex/mac-tab5-321-gallery-sync-20261113)。Windows 基线为 `135e47da07f6de4ce32600398a7636a435a6b89e`；Mac 功能提交为 `2dd20957def61a9a0964cbb099667b6d7e1c7853`。以合并 Windows 后续修复、最终版本及双语说明后的干净提交作为候选。交接文档提交不是已验收的安装包。
- 当前公开基线：Windows／Mac `0.6.20`、TAB5 `0.2.320-ui`、ESP8266 `0.5.8`。桥接源码仍为 `0.6.20`，下一桥接版本及产品总版本由发布负责人按远端实际占用分别确定。若两端同发，桥接范围用 `all`，共用源码 SHA，各自在原生平台验收；若只发 Windows，则用 `windows`，保留实际已发布的 Mac 入口。
- TAB5 待发布版本已登记 `0.2.321-ui`，已观察到的内部版本为 R4。若修复固件，沿用待发版本并递增内部修订；对应首刷、升级、源码和说明须绑定最终镜像。ESP8266 和图库内容包本轮无需重发。
- 批量传输是协议 v1 的可选能力，要求支持 `guideBatch:1` 的桥接和包含 321 图库批量实现的 TAB5。旧端继续用既有目录和范围读取。合并时保留 Mac 旧目录别名、Windows 目录／图片／逐件摘要的字节兼容及 TF 校验。见[图库协议](../PROTOCOL.md#tab5-321-批量详情同步--batched-artwork-guides)。

## 验收结论与发布条件

| 项目 | 证据与结论 | Windows 接手后要做的事 |
| --- | --- | --- |
| 跨端摘要及协议 | 已完成 Mac 离线与自动化验证。两套年度图库 732 件摘要匹配；测试覆盖多页原文、旧目录回退、坏摘要及分段篡改。 | 原生 Windows 跑 Release 构建、`--status-once` 和图库回归；候选代码变动时只补受影响的 Mac 检查。 |
| 实际设备、批次与设置 | 321-R4 真机收到两类压缩批次；年度目录最终均为 366/366。临时 Wi-Fi 切换、恢复 USB 和设置读回相同。Windows 的 321-R2 全量现场结果可作对照。 | 不能把临时切换当作批次断线接管，或把开发版 App 当作正式安装包。 |
| 屏保开启时零 JPEG | **未通过，阻止将当前方案作为已验收的零图片迁移交付。** 真机在详情批次提交前已开始收图；生产图库 worker 的模拟复现两次范围请求、65,536 字节。批次先提交时为 0。 | 先用[复现补丁](tab5-321-release-evidence/TAB5-321-SAVER-ORDER-REPRO.patch)重放，修复前台取图与 TF 链接提交的先后。验证同图时继续显示旧图、批次成功后切换、批次失败可回退，以及实际换图仍能正常加载。 |
| 指定全量包、跨端和离线 | 实机尚未完整验证；宿主模拟已覆盖 R4 重试和 TF 离线读取。 | 在最终组合上从 `2026.11.03.1` 更新到 `2026.11.12.1` 全量标准包，核对 11,198 张 JPEG 一致、名画 539 件／书法 585 件变更和 31 个额外原文分页。屏保开、关分别测量图片字节及 TF 提交；实测批次中断、Wi-Fi 接管、Windows ↔ Mac 复用、断开桥接后双方向翻页和长原文。 |

[详细 Mac 交接](MAC-TAB5-321-GALLERY-HANDOFF-20261113.md) · [可跨机读取的证据 JSON](tab5-321-release-evidence/MAC-321-EVIDENCE.json) · [Windows 现场结果](TAB5-321-R2-GALLERY-SCANOUT-20261113.md) · [R4 选路](TAB5-321-R4-GALLERY-RETRY-20261113.md)。证据 JSON 是开发记录，不是发布脚本的 `ACCEPTANCE.json`。

## 构建、封存与收尾

1. **先修复，再定候选。** 在对应 R4 独立工程上把测试补丁应用到 `tests/ui-preview/gallery_worker_test.c`，按工程的 Windows 原生预览构建生成 `gallery-worker-test.exe`。R4 复现预期：`--saver-foreground-first` 为 2 次范围／65,536 字节，`--saver-commit-first` 为 0；两者提交后离线为 0 RPC。补丁是诊断用的基线复现，前一个断言在修复后应改为期待 0，并保留后一个对照。
2. **冻结实际交付的包。** 桥接候选走 `candidate.yml`，指定最终 SHA 和平台。Mac 开发 App 已通过 524 项 Swift 测试（4 跳过、0 失败）、Release 构建及本地签名校验；未形成冻结安装 ZIP，也没有公证或最终包安装验收证据。TAB5 R4 的真机 ELF 已记入证据；本机固定 `latest` 仍是 320-R6，不可拿来准备 321。Windows 端提供最终 BIN、首刷／升级 ZIP、对应源码和实际 sidecar；运行 `prepare_tab5_firmware.ps1`、预览实际说明，再用 `prepare_release_assets.py` 和 `seal` 绑定最终包。
3. **按实际包登记。** 运行对应系统安装／启动和资料保留检查，以及上表的设备检查。将真实结果与候选 ID、源码 SHA、文件 SHA-256 一起记录。缺失和失败保持原状态，开发二进制哈希不填入 ZIP 或安装器字段。
4. **由 Windows 负责人晋级。** 按[发布自动化](../RELEASE-AUTOMATION.md)先做本地 `status` 和晋级预检；达到发布条件后晋级同一原包。候选随包提供 `LICENSE`、`THIRD_PARTY_NOTICES.md` 和校验文件。桥接草稿文案见两个 changelog 的 Unreleased 段落；TAB5 有独立双语更新日志。实际发布后按[收尾清单](../RELEASE-CHECKLIST.md)同步中英文主页、下载和安装说明，核对公开附件。界面和入口未变时可复用仍准确的截图和视频，文案只写最终候选已验证的行为。

## 2026-11-14 补充

见 [Mac 0.6.21 冻结包安装结果](MAC-0621-INSTALL-ACCEPTANCE-20261114.md)：系统日志确认 0.6.21 的新签名触发钥匙串授权提示，启动主线程同步等待，菜单栏启动未通过；已恢复 0.6.20。原 ZIP 需在用户能处理提示时复测。R5 的旧版全量图库更新重放按用户要求暂缓，仍记为待验收。
