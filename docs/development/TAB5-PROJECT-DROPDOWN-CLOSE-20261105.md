# TAB5 项目菜单离页关闭

## 问题与设计

用户在实际 `.314` 上打开“对话 → 项目”下拉菜单后切到“总览”，菜单仍悬浮在新页面，延迟后才消失；再次进入总览仍复现。当前 `page_select` 关闭了键盘等对话控件，但没有关闭项目下拉浮层。LVGL 下拉列表属于屏幕浮层，隐藏原页面不会同步关闭它。

行为设计：任何离开对话页的切换都在页面切换函数中立即关闭该项目菜单；返回时保持关闭。保留项目选择、两客户端草稿、1280×720 布局、字体、颜色、间距和触控范围，不新增控件或动画。点击页面底栏、滑动以及程序切页复用同一关闭路径。

## 验证边界

先在真实 LVGL 原生预览中复现未关闭，再验证立即关闭、总览画面无浮层、返回对话页选择和草稿保留；两客户端分别覆盖。源码以完整 `.313` 工程加已验收 `.314` 补丁为基线，新增补丁单独保存。

当前 `.314` BIN、ELF、OTA 和原验收记录保持原样。本轮不刷机、不将 `.314` 的旧验收套到新固件；该交互缺口阻止将本轮全功能验收声明为完成。新固件原生构建、实际渲染和真机回归需要独立记录。电脑端无需为这项设备菜单修复修改协议。

## 定向结果

- 原 `.314` 生产 UI 在真实 LVGL 预览中复现：已切到总览，下拉列表仍悬浮；`!lv_dropdown_is_open` 断言失败，退出码 3。保留失败日志和原始画面。
- 修复在 `page_select` 离开对话页时调用 `lv_dropdown_close`。两客户端立即关闭、返回不重开、项目选择及草稿保留检查通过；覆盖一般切页与底栏/滑动共用切页入口。
- 6 张 1280×720 原生画面已生成，查看了旧代码残留、修复后总览和 Claude 返回画面。菜单消失，布局及草稿显示保持。
- `.314` 取消草稿恢复回归及客户端切换回归均通过；这些是当前源码的原生 LVGL 回归，不是新 BIN 的真机验收。
- [源码补丁](TAB5-PROJECT-DROPDOWN-CLOSE-20261105.patch)基于 `.313` 完整工程加 `.314` 三文件补丁；[摘要清单](TAB5-PROJECT-DROPDOWN-CLOSE-SOURCE.json)记录三个修改/新增文件。独立目录应用、恢复 LF 后逐字节核对通过；Windows Git 按仓库规则将测试文件转成 CRLF 时，须按摘要清单恢复 LF 后复核。生产代码仅改 `ui.c`，另外两项是预览入口及定向测试。

日志与原生渲染保存在本机 `artifacts/tab5-project-dropdown-close-20261105/`。正在准备独立 `.315` 开发候选；正式 `.314` 固定交付路径仍保留已验收镜像。尚无新的设备安装、真机通过或发布结论。

## English

Close the TAB5 project dropdown synchronously whenever navigation leaves the dialogue page. Preserve the selected project and drafts, without changing layout or protocol. The accepted .314 firmware bytes remain unchanged; native preview checks and any future device firmware acceptance must retain their own identity.
