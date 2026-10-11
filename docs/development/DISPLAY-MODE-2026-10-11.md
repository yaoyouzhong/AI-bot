# 内容轮播的显示模式选择

2026-10-11。本轮修改相邻 TAB5 工程的设置交互。原来的“自动选择＋页面下拉框”与“启用轮播”分散在总览模型选择的前后，状态互相修改；选择固定页立即离开设置，普通浏览与表盘切换也可能隐式改变显示方式。

## 新交互

三个并列、互斥的选择：

| 选择 | 展示行为 | 显示的设置 |
| --- | --- | --- |
| 内容轮播 | 按勾选及排列顺序循环展示 | 切换间隔、轮播页面与顺序 |
| 固定页面 | 退出设置后显示所选页面 | 显示页面 |
| 自动切换 | 音乐播放时显示音乐，其余显示总览；沿用已有提醒优先级 | 简短行为说明 |

模式按钮沿用设备深灰／浅蓝色及 24 px 正文字体，三个相同大小的触摸区域只突出当前选择。模式说明和专属参数紧邻选择，总览模型选择独立放在后面。切换模式不清空轮播页面、顺序和间隔，也不会跳出设置。

模式、固定页、间隔、勾选和顺序均先保存成功再更新实际偏好；失败恢复控件并提示重试。最后一个轮播页面不能取消；旧空列表进入轮播时选中既有顺序的第一项。普通浏览、更换表盘不把自动／轮播模式改为固定显示；固定显示下正常浏览页面沿用原有手动选页行为。

## 兼容与验收边界

继续使用设备已有 `selected`、`cycle`、`enabled`、`order`、`interval`；三个模式分别对应 `-1/true`、`0..7/false`、`-1/false`，没有新增协议字段、NVS 格式或迁移。电脑显示设置对端已经支持这些组合，未修改桥接界面或二进制。

实现阶段仅修改本地源码并完成原生渲染回归和开发构建，当时固定升级入口与版本登记仍为 `0.2.174-ui`。随后用户授权与返回／标题调整合并交付，规范入口与版本登记已更新为 `0.2.175-ui`；现有桥接显示设备运行 `.175`，按当前包重新执行 USB 启动核验通过。目视／触控验收仍待完成，见 [交付记录](../TAB5-ACCEPTANCE-175.md)。设备的实际偏好未被测试改写，未提交或公开发布。

Three exclusive modes reuse the existing selected/cycle fields and persistent format, show only the relevant settings, preserve cycle preferences and roll back failed writes. Following the user's subsequent delivery authorization, this change was bundled with back navigation and title alignment in the canonical .175 package. The existing bridge reports .175 running on TAB5, and the production USB boot verifier passed against the current package. Physical acceptance remains pending. No Windows bridge binary deployment or public release.

## 验证

证据目录：`artifacts/development/display-mode-20261011/`。

- `DISPLAY_MODES_UI_OK`：实际指针点击三个模式，唯一选中态及专属参数显示；模式切换保留页面／顺序／间隔；固定页面选择留在设置，返回后显示时钟；自动播放音乐／停止回总览；更换表盘不改变模式；模式／页面／间隔／勾选／顺序写入失败回滚；最后一页保护及旧空列表兼容。
- `CYCLE_UI_OK`、`OVERVIEW_OPTION_LOGOS_OK`、`COUNTDOWNS_UI_OK`、`QUICK_RETURN_CONTEXT_OK`、`QUICK_RETURN_POINTER_OK`、`LVGL_PREVIEW_OK` 全部通过。原有固定页手动滑动的延迟保存断言继续保留，测试现明确设置固定模式；模型图标实际点击前按新布局滚动至模型区，全部 11 项仍验证选择和取消。两次旧布局／旧模式测试前置条件失败的完整日志保留在 `failures/`，未跳过或弱化断言。
- 三种模式、轮播列表、固定时钟、保存失败及滚动后的模型区原生截图已检查。右边的模式按钮为滚动条留出 16 px，与下拉框右边缘对齐。
- `scripts/build.ps1 -LocalArt -LocalVersion '0.2.175-ui'` 开发构建通过；镜像 6,897,536 字节，SHA-256 `e99ca3f3816b7888c03cedb5a3242821895932cbfbeb64a51939b258aa3d0e97`，分区余量 311,424 字节。构建仍有既有 `weather_icon_create` 未使用警告。
- `DISPLAY_FLASH_SAFE_ELF_PASS` 通过，记录于 `display-flash-safe.json`；两仓库 `git diff --check` 通过。源文件、开发镜像及未变更的 `.174` 固定镜像身份见 `verification.json`。
- [交互预览](../../artifacts/development/display-mode-20261011/preview.html)使用实际原生截图；三种状态、固定时钟、滚动列表、失败状态及修改前画面可切换。`DISPLAY_MODE_ARTIFACT_OK` 校验七幅引用、模式按钮、画面点击区域、说明和原始图链接。未将此本地渲染或脚本校验视为真机／浏览器实测。
