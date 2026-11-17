# 隔离GitHub共享中转与QQ通知包装器

## 明确不作为生产启动开关

`coordination_mailbox.py` 当前只允许 `mode=isolated-test`，namespace 必须为 `isolated-test/<明确测试名>`；main/master 写入禁止。生产分支、现有开发会话和双向来源映射尚未在此脚本自动配置。只有明确核验并授权的历史事件可用于受控测试，不把历史回放说成自然监测。Auto Loop 源码不变，测试不合并、不发布产品。

## GitHub共享中转

源端与目标端都使用已登录的本机 GitHub CLI。固定配置：

```json
{
  "repository": "yaoyouzhong/AI-bot",
  "branch": "codex/autoloop-coordination",
  "namespace": "isolated-test/<本次固定测试名>",
  "mode": "isolated-test",
  "enabled": true,
  "writeAuthorized": true,
  "authorizationEvidence": "<用户对本次独立测试中转的授权记录>",
  "ghExecutable": "<本机已核验的gh绝对路径>"
}
```

route/provenance 使用 `COORDINATION-FLOW.md` 定义的固定映射。配置中的 true 仅记录调用方核验结果，不自行授予权限；测试源事件来自既有扫描记录，双方不得临时猜测分支来源。

```sh
# 源端发布唯一测试packet
python coordination_mailbox.py --mailbox mailbox.json --route route.json publish \
  --event event.json --provenance provenance.json
# 目标端读取GitHub，不需要Dot逐条搬运内容
python coordination_mailbox.py --mailbox mailbox.json --route route.json poll \
  --binding binding.json --database checkpoint.sqlite --worker <固定目标适配器标识>
```

所有文件仅位于 `docs/development/coordination-ledger/transport/isolated-test/<测试名>/`：
- `<packetId>.json`：观察核心字段和显式来源；排除原始扫描日志、私有本地路径和凭证。
- `<packetId>.receipt.json`：真实执行器终态证据及交接ID，不包含会话全文或QQ目的地。

packet身份由规范JSON哈希计算。源端重复publish发现同内容只返回已存在；同路径不同内容失败，不覆盖。写入响应丢失时读取远端同身份内容核对；无法核对则unknown，不能换ID盲重。目标端先固定Git分支快照，再按该SHA列目录和读取packet，核验路径、内容哈希、repo/branch/platform后调用flow。重复poll和进程重开使用原SQLite检查点，不重复派发。终态receipt只create并读回验证，cancelled照实记录，不当开发成功。

被目标端消费的packet仍是资料，不是授权；技术分析归现有开发会话，临时只读测试规则不能升级为产品修改权限。正常适配器路径不根据源端自述生成终态：目标端只有读到真实匹配运行才生成receipt；这不是加密身份认证或GitHub平台级访问隔离，仓库写入权限持有者依然可改文件，生产启用前需另审访问权限。

测试packet/receipt保留审计，不为“清理测试”删除既有记录。清理的是本次临时Auto Loop规则/Agent，需原配置字节一致、检查通过；不动原规则、产品main或正在开发的工作区。

## QQ通知包装器

`QQNotifier` 只根据检查点中 confirmed 的真实交接和已保存终态生成通知。它不采纳事件提供的命令，不自动切换其他通道。固定binding含：`command`（既有qq-only程序参数数组）、`recipient`（核验为本人）、`bindingVerified`、`bindingEvidence`、`sendAuthorized`；受控测试必须 `testMode=true`，消息前缀为“【协调链路测试】”。可在本机内存构造binding，避免QQ标识落盘。模块不读取或复制凭据。

只有 sendAuthorized=true 才调用传输；否则预览返回 awaiting-authorization、传输调用零次。持久通知key由实际delivery和终态组成。成功响应必须是 `qq:` 后1–512个无空白、无控制符的可见opaque字符，允许真实providerID中的 `!!` 等符号，不将其误判失败。

返回 providerAccepted=true 仅代表接口接收；deliveryConfirmed/readConfirmed 都为false，不能宣称用户已读。同key重复和重开通知账本后都不再次发送；超时、非零退出、未识别回执均保留unknown，禁止盲重。没有按key查询的QQ接口不能自动消除unknown，只能核对实际传输证据。

```sh
python coordination_qq.py --binding qq-binding.json --checkpoints checkpoint.sqlite \
  --notifications notifications.sqlite --delivery <实际已完成运行的deliveryId>
```

首次实发必须有明确目的地和发送范围授权；一条测试成功不等于常驻生产通知已启用。现有成功Accepted回执不能拿来给新的任务标成功。通知数据库独立本地保存，不上传到GitHub。

## 离线回归与真实验证分别报告

离线覆盖：源/目标独立客户端、共享假GitHub API、真实SQLite重启、写响应丢失读回、不可变冲突/packet篡改、重复poll只一轮、生产/main禁止；QQ未授权零调用、opaque回执、重复不发、unknown不重发。

真实验证报告应单列：源端实际GitHub写入SHA/packet路径、目标端读取快照、真实run及receipt、源和目标重启/重复调用结果、临时规则恢复。不得用离线结果冒称真实跨机链路，也不得用一次受控回放冒称常驻生产系统。
