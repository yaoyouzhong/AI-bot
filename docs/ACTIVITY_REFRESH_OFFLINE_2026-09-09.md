# 活动页刷新与离线页优化

活动页原先每个 status 心跳都会清整屏，时钟只在心跳到达时重绘。现分为静态框架、时钟、两条活动状态、重置次数、两项 Token 数值：框架在进入页面时绘制；时钟逐秒更新；其它值变化才替换所属小矩形。离屏小缓冲完整覆盖旧内容，避免先清空带来的闪烁和位数减少残影。

现场第一次测试：5 秒内时钟更新 5 次、静态框架 0 次、未变数据 0 次；Token 123→9 恰好一次局部替换。未把测试计数等同于实体观感确认。

离线页按 frontend-design 的“主要信息优先”原则，保留黑底。按用户去掉秒数后的反馈，将白色 AI-bot 标题与青色 Font 7 大号 HH:MM（黄色冒号）一起下移 16px，分别位于 y=40、y=92；不显示秒数；灰色 Waiting for bridge 保持 y=176；橙色 PC OFF 恢复为右下角 Font 2（右边界 x=224，y=208）。静态文字只在进入离线时绘制，时分仅在分钟变化时更新。使用独立时/冒号/分小缓冲限制峰值内存，不分配整屏缓冲。

`page_data` 新增只读活动重绘计数 `activity_chrome_draws`、`activity_clock_draws`、`activity_data_draws`，以及离线字体实际尺寸 `offline_clock_width`、`offline_clock_height`。`--test-activity-refresh` 覆盖静态心跳、不变数值、位数缩短、短暂离线与 USB 恢复；测试结束恢复原模式。离线不等同于已验证 PC 实际关机或 Wi-Fi 回退。
