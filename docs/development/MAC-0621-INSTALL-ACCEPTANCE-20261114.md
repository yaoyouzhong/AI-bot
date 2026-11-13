# Mac 0.6.21 冻结包安装验收：启动未通过

2026-11-14，北京时间，Apple Silicon / macOS 27.3。在日常账号用实际候选覆盖原运行的 0.6.20。结论：`macos-install-startup` **未通过**；这份证据不应登记为通过或延期。

- Actions 批次 [43450903389](https://github.com/yaoyouzhong/AI-bot/actions/runs/43450903389)，[原生制品 14051721453](https://github.com/yaoyouzhong/AI-bot/actions/runs/43450903389/artifacts/14051721453)。完整制品 ZIP 的 SHA-256 与 GitHub 记录一致（`e533de6bbaad65c4f908373fe35a0880e6efc9e88db6087bad47b1a5be7c65cc`）。
- Mac-only 冻结清单候选 ID `735bbf59e9c2a47ce635a8de9151daebffbada26d84f0ee86db4d5c54d879063`，源码及包内标识均为 `0a8c01ef2c9103c33c657055135ab82d768dc487`。实际测试的 `AI-bot-bridge-v0.6.21-macos-arm64.zip` 为 19,978,781 字节，SHA-256 `3532f662d9946a20890706173a49b990079e3fdd4db3e8a1d223f30cfe687df3`，与原生来源和冻结清单一致。安装目录内容与 ZIP 解包逐文件一致，严格签名检查通过；ad-hoc 签名、未公证。未验证冻结制品内其他附件的全部字节。
- 使用 Launch Services 从 `/Applications` 启动后，进程仍在，但系统辅助功能树中未出现菜单栏项目，本地桥接端口也未开始监听。采样约一分钟后，主线程仍处于 `applicationDidFinishLaunching → MacKeychainAccess → SecItemCopyMatching`，等待钥匙串返回。采样不能证明系统是否显示了交互授权提示。未批准任何钥匙串提示，也未修改钥匙串权限。
- 候选正常退出未结束，随后对本次候选进程发送 TERM。恢复原 0.6.20 应用并确认菜单栏和本地监听恢复。安装前后 48 个受检资料文件（含 29 个 TAB5 私有存储文件）SHA-256 一致。未验证 0.6.21 完整启动后的重启和权限复用。
- 下一步：在可处理系统钥匙串提示的会话，使用同一 ZIP 复测覆盖安装，分别记录系统提示、菜单栏和重启。若新签名或代码解决了问题，则按新候选身份记录实际原包的验收。

## TAB5 321-R5

未检测到外接 TF 卡或 TAB5 USB，也未找到含 `2026.11.03.1` 指定全量图库的旧卡镜像。R5 的原生回归和已报告的启动核验不能替代更新重放。继续使用备用旧版卡实测，记录两类批次接收、写卡和目录提交耗时及 JPEG 字节；保留当前新版卡。见 [R5 修复记录](https://github.com/yaoyouzhong/AI-bot/blob/0a8c01ef2c9103c33c657055135ab82d768dc487/docs/development/TAB5-321-R5-SAVER-GALLERY-20261113.md)。
