# 产品介绍视频 / Product introduction video

[观看视频 / Watch video](assets/product-intro/AI-bot-product-intro.mp4)

## 内容与来源

- 36 秒、1920×1080、30fps，H.264 视频与 AAC 音频。2026-09-19 制作，经维护者确认文案并授权上传。
- 产品定位：**AI 状态，一眼便知。** 任务状态、账户额度，集中显示在桌面小屏。AI-bot · 你的 AI 状态桌面时钟。
- 展示 AI 活动、等待输入/完成提示、账户额度、天气/音乐/系统、农历时钟与桌宠；这是产品介绍，不是版本更新公告。
- 界面由 `tools/doc-capture/DocCapture.cs` 编译并调用原生 `MirrorForm.RenderSnapshot`，在隔离配置下生成，数据为固定虚构样例。没有读取真实账号、凭据、会话正文或私人动画。界面来源代码为 `b2404e4`。
- 页面与图标来自本项目；桌宠为原创 BYTE SPROUT。不包含用户私有的第三方角色。画面是程序渲染，不是实体设备实拍或新一轮真机验收。
- 英文字体 Inter、中文字体 Noto Sans SC，均使用 SIL Open Font License；这里只发布渲染后的视频和封面，不打包字体文件。
- 配乐为本片代码原创合成；动作音效来自归藏 product video skill 的原创合成 WAV。没有使用参考片音轨、下载的第三方录音或 CodePilot 默认样式。
- 制作工具使用 [归藏 product video skill](https://github.com/op7418/guizang-product-video-skill) 的 AGPL-3.0 脚手架；本目录不分发工具源码或其依赖。工具许可不自动改变渲染视频的许可，素材仍依各自来源使用。
- 成片关键帧、字体/图片、时长/音轨与前后定位已检查；音频经过响度测量，未声明已完成实际听感验收。
- 视频与封面 SHA-256 记录于 `licenses/materials.json`，公开内容检查仅接受这两个明确路径与对应字节。

## English

A 36-second product overview with Chinese captions and English headings. The video shows AI activity, attention/completion indicators, account quotas, weather/music/system pages, and the clock/pet. Native Windows UI captures use isolated synthetic data; they are not hardware footage or new live-account/platform acceptance. The project icon and original BYTE SPROUT pet are used; no private artwork or credentials are included. Music is synthesized for this film and SFX are original synthesized assets from the credited production skill. Font families are Inter and Noto Sans SC (SIL OFL), rendered into the output without font redistribution. The AGPL production scaffold itself is not distributed here. Visual/media checks and loudness measurements are complete; subjective listening remains unverified. Exact media hashes are listed in `licenses/materials.json`.
