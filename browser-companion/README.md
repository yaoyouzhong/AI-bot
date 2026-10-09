# AI-bot browser media playback companion

Version 0.2.0 also supplies current HTML media playback, position, duration,
album and playback rate. It supports explicitly enabled websites with Media
Session metadata even if Windows omits their complete playback information.
An active media element wins over an inactive one; simultaneous playing elements
are ambiguous and are not assigned a guessed timeline. Ended/stale tabs expire,
and artwork is separated by the enabled site/tab and track. Live streams with no
finite duration retain unknown progress. Reload an existing extension and page
after updating; no new site permissions are requested. See [media support](../docs/MEDIA-PLAYERS.md).

This optional Chrome/Edge Manifest V3 extension reads the current Media Session
title, artist, album, playback state, position, duration, rate and artwork on sites the user explicitly enables. It sends them to
the authenticated loopback bridge, never to an external service. It does not read
browser history, cookies or account credentials.

It selects the largest declared artwork. On YouTube watch pages it also supplies
the current video ID, so the bridge can request the available high-resolution
thumbnail automatically for every video. Other sites use their Media Session
artwork. Sites that do not expose useful metadata retain the Windows cover.
When enriching a system session, the title, artist and album must match the
enabled tab. Companion artwork is scoped to the site/tab and track to prevent
another tab from replacing its cover. Download failures keep the last success
for that identity. Playback, artwork, titles and video IDs stay in bridge memory.

The repository config has no pairing key. After approval, run the prepared desktop
script to copy the extension into the user's local app folder and create its own
local authentication key. Load that folder through Chrome/Edge's “Load unpacked”
button. Open a playback site, click the extension, then “启用当前网站”. This grants
access to that site; changing videos there subsequently needs no link entry.

Remove the extension in the browser to disable it. The bridge's Windows artwork
fallback and JPEG transport remain available without this extension.

## 中文

0.2.0 增加曲目、专辑、播放状态、真实进度和倍速，沿用当前网站的授权与本机鉴权。
结束播放或没有新心跳的标签页会失效；封面按网站／标签页和曲目隔离。
支持提供 Media Session 元数据的网站。同页同时播放多个媒体时不猜进度，直播不伪造总时长。
旧扩展需要重新加载并刷新网页，才能使用新版状态接口。未进行真实浏览器加载或网站验收。

这是通用的当前媒体播放及封面接口，不保存某一视频的替换图片，也不按标题搜索图片。
YouTube 从当前播放页面获取编号；其他网站使用 Media Session 提供的封面。
只处理用户逐个启用的网站，扩展只向本机桥接发送播放信息。

开发验证：`node capture.test.cjs`。这验证提取、切换和权限声明，不能代替真实
浏览器加载、网站授权和 TAB5 显示验收。配对密钥仅存在本机配置副本中。
