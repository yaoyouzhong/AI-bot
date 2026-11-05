# TAB5 项目菜单离页关闭

## 问题与设计

用户在实际 `.314` 上打开“对话 → 项目”下拉菜单后切到“总览”，菜单仍悬浮在新页面，延迟后才消失；再次进入总览仍复现。当前 `page_select` 关闭了键盘等对话控件，但没有关闭项目下拉浮层。LVGL 下拉列表属于屏幕浮层，隐藏原页面不会同步关闭它。

行为设计：任何离开对话页的切换都在页面切换函数中立即关闭该项目菜单；返回时保持关闭。保留项目选择、两客户端草稿、1280×720 布局、字体、颜色、间距和触控范围，不新增控件或动画。点击页面底栏、滑动以及程序切页复用同一关闭路径。

## 验证边界

先在真实 LVGL 原生预览中复现未关闭，再验证立即关闭、总览画面无浮层、返回对话页选择和草稿保留；两客户端分别覆盖。源码以完整 `.313` 工程加已验收 `.314` 补丁为基线，新增补丁单独保存。

原 `.314` BIN、ELF、OTA 和验收记录保持原样，不将 `.314` 的旧验收套到受影响的新固件路径；该交互缺口须补齐独立真机回归，才能声明本轮全功能验收完成。新固件构建、实际渲染、用户升级授权和启动核验分别记录如下。电脑端无需为这项设备菜单修复修改协议。

## 定向结果

- 原 `.314` 生产 UI 在真实 LVGL 预览中复现：已切到总览，下拉列表仍悬浮；`!lv_dropdown_is_open` 断言失败，退出码 3。保留失败日志和原始画面。
- 修复在 `page_select` 离开对话页时调用 `lv_dropdown_close`。两客户端立即关闭、返回不重开、项目选择及草稿保留检查通过；覆盖一般切页与底栏/滑动共用切页入口。
- 6 张 1280×720 原生画面已生成，查看了旧代码残留、修复后总览和 Claude 返回画面。菜单消失，布局及草稿显示保持。
- `.314` 取消草稿恢复回归及客户端切换回归均通过；这些是当前源码的原生 LVGL 回归，不是新 BIN 的真机验收。
- [源码补丁](TAB5-PROJECT-DROPDOWN-CLOSE-20261105.patch)基于 `.313` 完整工程加 `.314` 三文件补丁；[摘要清单](TAB5-PROJECT-DROPDOWN-CLOSE-SOURCE.json)记录三个修改/新增文件。独立目录应用、恢复 LF 后逐字节核对通过；Windows Git 按仓库规则将测试文件转成 CRLF 时，须按摘要清单恢复 LF 后复核。生产代码仅改 `ui.c`，另外两项是预览入口及定向测试。

日志与原生渲染保存在本机 `artifacts/tab5-project-dropdown-close-20261105/`。`.315` 开发候选已由完整工程构建，图库栈预算和绘制后端链接检查通过；生产镜像校验器与实际更新说明原生预览通过。构建前后 9 项既有 SDK 补丁摘要保持，配置值无变化。

用户于 2026-11-05 明确批准升级这份独立 `.315`。已通过固定交付路径提供并安装同一 BIN：

- BIN：6,924,128 字节；SHA-256 `436df029e55f816dc00a2a37a011b0fa077fa7fe7f05478b1df1309c98a9a40b`。
- ELF：SHA-256 `d1d7d7ea1bf8369aac5ebd018b113c498a7b085e79711c97a14ed14413506651`。
- 首次过早核验未通过，保留失败记录，没有重刷。稍后通过正式核验器的三个递增运行时间样本、版本、ELF 指纹、运行分区及 `VALID` 状态检查。
- 原 `.314` 归档 BIN 的 SHA-256 仍为 `f3f98f2019c46ac9c49be3b60fe7f550b1b03ee0c5732e044796c53bbc21dd99`；原包及其验收记录继续保留。

`.315` 真机后台点按与截图已覆盖两客户端：实际展开项目菜单，点总览后画面无浮层，返回对话时菜单关闭。Claude 两句录音文字和照片在升级及客户端切换后恢复。用户随后用手指操作确认：“立即消失，回来也关闭。”菜单修复真机验收通过。

独立的照片发送链路随后只发送一次，Claude 会话记录收到原 87,646 字节 JPEG，设备经只读回执核对显示“已发送，请查看会话”；持久回执为 `accepted` / 202，并与真实供应商 UUID 一致。模型读取该图片时产生真实 `Read` 工具确认，TAB5 回复页正确显示“等待电脑端确认”。诊断结束时显示设置、配置文件摘要保持，正常常驻桥接恢复。

随后用户在正常常驻桥接下发送一条不同的继续指令，没有重新添加或发送照片；从 Windows 托盘打开“Claude 操作确认”，核对测试照片路径并点“允许本次”。真实 `Read` 返回成功，供应商生成公开回复“照片已收到”；用户在 TAB5 确认看到该回复，顶部状态为“已完成”。照片发送、一次工具确认及最终设备回复链路通过，原照片消息没有重复发送。当前尚无正式发布结论。

## English

Close the TAB5 project dropdown synchronously whenever navigation leaves the dialogue page. Preserve the selected project and drafts, without changing layout or protocol. The accepted .314 archive remains unchanged. The separately authorized .315 image passed the full firmware build, actual sidecar rendering, exact device boot verification and menu touch checks. A single photo delivery, native tray Read approval and final photo response were also verified with the real provider and on TAB5. No public release is claimed.
