# 服务标识来源

首页小图标仅用于识别当前连接的服务，保持原标识形状和颜色。

| 显示名称 | 来源 |
| --- | --- |
| Codex | OpenAI 官方仓库 `openai/openai-agents-python/docs/assets/logo.svg`，以 OpenAI 标识代表 Codex。品牌使用说明：https://openai.com/brand/ |
| Codex 直达 | 本机 OpenAI Codex 26.930.3930.0 随附的 `webview/assets/codex-app-ga-logo-3e5209898ca3.png`，保留原形状、颜色与透明背景，仅缩放用于服务入口。原 PNG SHA-256：`8e82b26c98a10e45798ce48124515720657f7735fb8d0853b3f087eaa8a6b74e`。不以本项目 MIT 条款重新许可此标识。 |
| 豆包快捷键 | [豆包输入法官网](https://ime.doubao.com/pc)页面引用的[人物与语音气泡原始 PNG](https://lf3-static.bytednsdoc.com/obj/eden-cn/lm_hz_ihsph/ljhwZthlaukjlkulzlp/wave-site/logo_0908.png)，保存为 `doubao-ime.png`，保留原图白色圆环、颜色与透明背景，仅缩放。原 PNG SHA-256：`ac90884322b94b517389897bd10e71aa2e5e1626e27df129a15eded42addbbe8`。不以本项目 MIT 条款重新许可此标识。旧版 `doubao-app.png` 来自豆包官网 favicon，保留作来源记录。 |
| DeepSeek | DeepSeek 官方网站 `https://www.deepseek.com/favicon.ico` |
| 智谱 | 智谱官方网站 `https://www.zhipuai.cn/favicon.png`，当前 Z.AI 图标 |

这些标识分别属于其权利人。公开分发固件前，应重新核对标识版本与使用条款。

## 总览十一供应商标识核对（2026-10-10）

采用下列官方页面当前链接的标识；原始文件、下载地址与 SHA-256 记录在 [providers.json](providers.json)。本次刷新 DeepSeek、智谱和阿里标识，OpenAI 官方 SVG 与原资源一致。阿里采用当前百炼服务标识，智谱采用当前 Z.ai 图案。

| 供应商 | 官方来源页面 |
| --- | --- |
| Claude | https://claude.com/ |
| Codex / OpenAI | https://openai.com/brand/ 和 OpenAI 官方仓库 |
| 阿里 / 百炼 | https://bailian.console.aliyun.com/ |
| Kimi | https://www.kimi.com/ |
| MiniMax | https://platform.minimax.io/docs/guides/models-intro |
| DeepSeek | https://www.deepseek.com/ |
| 智谱 / Z.ai | https://www.zhipuai.cn/ |
| 阶跃 | https://www.stepfun.com/ |
| 百度 | https://www.baidu.com/ |
| 小米 | https://www.mi.com/ |
| 火山方舟 / 火山引擎 | https://www.volcengine.com/product/ark ，使用该官网当前 favicon |

总览统一使用 36×36 像素透明画布、ARGB8888、144 字节行跨度；生成时保持比例和原色，不重绘或改色。独立卡片文字使用可读的柔和品牌区分色，额度告警仍保留状态色。原始官方文件分辨率不同（最小 32×32），不声称原图都具有同一分辨率；设备实际加载的十一个资源具有相同尺寸和像素格式。

内容轮播的总览模型选项与总览卡片共用这些标识，视觉尺寸统一由 `scripts/generate-brand-logos.py` 的 `PROVIDER_FIT_SIZE` 管理。画布仍为 36×36：Codex 从同一份 OpenAI 原图去除透明留白，保持已确认的可见图案 30×29；Kimi、MiniMax、智谱、阶跃、百度、小米的带底色图标等比缩至 32×32；Claude 使用 34×34，火山方舟按 34 像素长边等比缩放。百炼和 DeepSeek 保留现有比例。该调整平衡线条标识与实心底色图标的视觉大小，保持品牌形状、原色和居中对齐，无需运行时缩放。
