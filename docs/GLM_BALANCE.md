# 智谱 GLM 可用余额

Windows 托盘“模型额度”的国产模型授权窗口选择“智谱 GLM”，在官方财务总览完成登录。显示模式 → 国产模型 → 智谱 GLM 可查看余额；也可加入原有轮播。首次授权前显示 `--` 和 `Authorize in bridge`，不以零值冒充余额。

GLM 沿用 DeepSeek 的数字字号、CNY 基线对齐、底部 USED 栏，标题使用蓝紫色。主数字为**可用余额**（CNY），底部为接口提供的累计消费金额，不是今天消费或 Coding Plan 额度。失败保留最近成功值，后台每轮轮询包含 GLM；429 退避 5 分钟。

金额与官方财务页面一致，向零截断为两位小数（例如 99.9993936 显示 99.99，而非四舍五入为 100.00）。缓存保存用于显示的金额。

## 数据依据和边界

- 官方财务总览：<https://bigmodel.cn/finance-center/finance/overview>
- 2026-09-09 核对官方前端 `app.64d2f69c.js` 和 `financialoverview.03c02d17.js`：GET `/api/biz/account/query-customer-account-report`，响应 `code=200`，`data.availableBalance` 是可用余额，`data.totalSpendAmount` 是累计消费金额，单位元。`balance` 与 `availableBalance` 不混用，不除以 100。
- 仅接受 HTTPS `bigmodel.cn` 上该精确路径的浏览器 XHR/Fetch 响应。不是厂商承诺稳定的公开 API，页面变化时需要重新核对；缺失/无效字段不覆盖缓存。
- 使用既有隔离 WebView2 登录配置。不会要求粘贴 API Key，不记录请求头、响应正文或令牌；缓存和串口仅包含可显示金额、币种、更新时间和陈旧标记。
- macOS 仅增加可选 `zhipu` 数据契约和设备模式兼容；没有新增 macOS 登录/取数能力，尚未在 Mac 编译验收。

## 验证

`--self-test-zhipu` 使用明确标注的测试数据验证金额选择、零余额、欠费、非法响应、精确域名/路径、JSON 和页面路由，生成 `artifacts/zhipu/` 镜像预览；不会写入真实账户缓存。

`--test-zhipu-device` 只发送生产缓存，检查真实 USB 握手、GLM 模式和可用状态，结束恢复原模式；未授权时明确报告 `real_balance_available=False`，不能据此宣称真实余额已验收。

真实验收须在用户登录后对照官方页面余额，确认桥接 `/status` 的 `domesticQuotas.zhipu` 和实体屏一致。

2026-09-09 本机验证：Windows Release 构建 0 警告/0 错误；GLM 自测、数据源自测、迁移授权解析自测通过。固件成功写入 COM7 并通过写入哈希校验；`ZHIPU_DEVICE_OK port=COM7 mode=domestic_zhipu real_balance_available=False`，验证了实际设备路由，但当时账户未授权，真实金额仍待验收。镜像余额截图为测试样例，不是账户实测结果。

随后实际授权响应已捕获；最终 `ZHIPU_REAL_BALANCE_MATCH_OK cache_reload/USB/device_amount/currency` 及 `ZHIPU_DEVICE_OK port=COM7 mode=domestic_zhipu real_balance_available=True` 均通过，验证的是真实账户缓存，不是样例注入。实体屏光学观感仍需用户确认。已去掉授权启动前的提前串口切页，避免未握手时弹出阻塞提示。

修正后重启并重新打开 GLM 财务页，14:57:37 再次捕获官方响应，`stale=false`，显示值与两位小数规则一致；证明不只是沿用上一进程缓存。
