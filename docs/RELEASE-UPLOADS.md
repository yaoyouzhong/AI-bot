# 图库分段上传与恢复 / Resumable gallery uploads

GitHub Release 不支持从一个附件的中间继续上传。发布大图库时先分成最大 128 MiB 的独立附件，再逐段校验；中断后运行同一上传命令，先查询服务器，跳过大小和 SHA-256 一致的附件，只重传未完成段。即使本地进度文件丢失，也按服务器摘要恢复。标准书法本次已完成整包上传，保留整包供程序直接更新；高清包采用分段。

```powershell
python scripts/upload_release_resumable.py split <archive.zip> --output artifacts/gallery-parts --chunk-mib 128
python scripts/upload_release_resumable.py upload --repo yaoyouzhong/AI-bot --tag <existing-release-tag> --directory artifacts/gallery-parts --manifest artifacts/gallery-parts/upload-manifest.json --state artifacts/gallery-parts/upload-state.json --workers 2
```

先按项目规则准备并核验对应 Release，再执行上传。脚本不会创建 Release、标签或更改正式状态。`split` 支持多个完整 ZIP，生成每段、整包清单和上传白名单；已有正确分段不重写。包内许可、正式附件白名单及发布状态仍按原发布流程核对。可把其他已准备附件及其大小／SHA-256 加入明确的白名单；不要遍历目录上传重建后的大 ZIP。

上传前核对本地全部白名单文件；每个附件在服务器大小和 SHA-256 均一致后写入进度。查询和上传失败有界重试；已公开的不同附件拒绝自动覆盖。没有令牌进入白名单或进度文件。`--direct` 只在本进程内忽略代理环境变量，不修改系统或 Git 代理配置。

本轮实际完成：39 个高清分段、51 个 TAB5／图库附件逐项校验；卡住的一段单独重试，其余不重传。两个完整包重新合并后哈希与原 ZIP 一致。测试覆盖响应丢失后的服务器去重、重启恢复、本地坏哈希、下载 HTTP 中断后的 Range 续传、合并中断和已有不同文件保留。公开检查使用服务器摘要、HEAD 和首尾小 Range，不重复下载数 GiB 整包。

普通用户通过[高清图库安装说明](GALLERY-HD.md)下载及合并；工具支持已完成段复用、HTTP Range 续传、逐段和整包校验，不改变系统执行策略。

GitHub uploads resume at independently verified part boundaries, not inside one asset. Restart the same upload command to skip server-verified parts; local state loss does not cause duplicate uploads. The script validates the local whitelist, bounds retries and refuses to replace different published assets. Optional `--direct` changes only this process's environment. End-user download/merge instructions, distinct from publication tooling, are linked above.
