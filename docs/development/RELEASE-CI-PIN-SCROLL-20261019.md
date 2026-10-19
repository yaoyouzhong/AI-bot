# PIN 滚动可达性 CI 修复 / PIN scroll-reachability CI repair

后续远端结果：[CI 39237959782](https://github.com/yaoyouzhong/AI-bot/actions/runs/39237959782) 的 Windows、macOS 和固件检查全部成功。两个正式 Release 使用原验收字节，服务器摘要核对后经明确授权手动公开；原标签失败记录保留，不改写旧标签。

Remote outcome: the linked Windows/macOS/firmware CI run passed. Reviewed original package bytes were published after server-digest verification under the explicit user authorization. The old failed tag run is retained; no tag rewriting occurred.

首次 0.6.10 标签的远端 Windows 构建通过，公共回归在 clipped PIN control 断言停止，自动草稿未创建；原失败日志保留。生产 FitScreen 会按工作区限制窗口大小，PIN 正文有自动滚动，保存反馈可能已滚动到底部。旧断言要求所有控件同时位于窗口内，不符合这一滚动行为。

独立 .NET 8 WinForms 程序直接加载原版 c608，在合成资料和 680px 客户区、1x／1.5x／2x 条件下复现五次旧断言失败；21 个原控件逐项滚动后均完整可达。进一步将滚动视口缩到 10px，严格边界检查仍拒绝真实裁切。早期 400px 极小视口也因控件高度大于视口而被严格拒绝，原日志保留，不能算成成功。

修复仅在 Tab5PinSelfTest.cs 中逐项滚入视口后执行原窗口边界／最小高度断言，再检查每一层滚动视口，新增 Wi-Fi 设置入口覆盖和安全数值诊断。生产窗口、PIN 存储、认证、输入及所有实际用户程序文件不变。隔离 Release 构建与完整公共回归通过，包括 156 项 PIN 检查和 63 项无线生命周期检查。远端修复后结果单独记录，不能用本地通过代替。

Windows 正式安装器、运行 ZIP 和 c608 保留原验收字节；其标签和对应源码仍指向原编译输入。此后续提交只维护测试及说明，不重打用户包、不改 CI 配置，也不改写已推送标签。

English: The first bridge-tag Windows build succeeded but its public test required every PIN control to be simultaneously visible after feedback scrolled a screen-capped dialog. A pure harness loading actual c608 reproduced five old-assertion failures at 680px and proved all 21 controls reachable by scrolling. Genuine clipping remains rejected, including a 10px viewport and the preserved earlier 400px-too-small case. Only the test now scrolls each control into view before retaining strict form/minimum-height assertions and adding ancestor-viewport checks plus Wi-Fi entry coverage. The isolated Release/full public run passed 156 PIN checks and 63 wireless lifecycle checks; remote CI is recorded separately. Production UI/security and accepted Windows package bytes stay unchanged. No workflow changes or tag rewriting are involved.
