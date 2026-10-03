# AI-bot browser music artwork companion

This optional Chrome/Edge Manifest V3 extension reads the current Media Session
title, artist and artwork on sites the user explicitly enables. It sends them to
the authenticated loopback bridge, never to an external service. It does not read
browser history, cookies or account credentials.

It selects the largest declared artwork. On YouTube watch pages it also supplies
the current video ID, so the bridge can request the available high-resolution
thumbnail automatically for every video. Other sites use their Media Session
artwork. Sites that do not expose useful metadata retain the Windows cover.
Images are matched against the Windows media title and artist to prevent another
tab from replacing the current cover. Download failures keep the last success.
Artwork, titles and video IDs stay in bridge memory.

The repository config has no pairing key. After approval, run the prepared desktop
script to copy the extension into the user's local app folder and create its own
local authentication key. Load that folder through Chrome/Edge's “Load unpacked”
button. Open a playback site, click the extension, then “启用当前网站”. This grants
access to that site; changing videos there subsequently needs no link entry.

Remove the extension in the browser to disable it. The bridge's Windows artwork
fallback and JPEG transport remain available without this extension.

## 中文

这是通用的当前媒体封面接口，不保存某一视频的替换图片，也不按标题搜索图片。
YouTube 从当前播放页面获取编号；其他网站使用 Media Session 提供的封面。
只处理用户逐个启用的网站，扩展只向本机桥接发送播放信息。

开发验证：`node capture.test.cjs`。这验证提取、切换和权限声明，不能代替真实
浏览器加载、网站授权和 TAB5 显示验收。配对密钥仅存在本机配置副本中。
