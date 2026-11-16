# TAB5 321-R5 书画屏保复用 TF 原图

R4 复现时，电脑通告新图库版本，屏保前台先于详情批次提交请求当天图片。TF 已有相同 JPEG，前台仍发出 2 次范围读取，共 65,536 字节。Mac 的年度图库实测也在批次前记录到 JPEG 流量。

R5 在前台需要新版本画面且当前版本记录缺失时，尝试读取 TF 上最后完成的同类图库。只有图片字节与当前认证清单的 SHA-256 匹配时才复用，并使用当前清单的作品详情。图片变更时走原下载路径。已经运行的目录任务可以完成提交，屏保继续显示已成功绘制的画面。

[固件补丁](TAB5-321-R5-SAVER-GALLERY-20261113.patch)应用在 [321-R4](TAB5-321-R4-GALLERY-RETRY-20261113.md) 之后。Mac 对齐及 R4 基线见 [Windows 发布交接](WINDOWS-RELEASE-TAB5-321-HANDOFF-20261113.md)。

回归测试覆盖两种提交顺序。屏保先触发和详情先提交均为 0 次 JPEG 范围读取、0 字节 JPEG 流量。图片内容确实变更时仍能下载并显示。

## 2026-11-15 软件补验

迁移输入为 `2026.11.03.1 → 2026.11.12.1` 两版全量图库。两版清单中 4,036 张名画 JPEG 和 7,162 张书法 JPEG 的 SHA-256 一致。Windows 桥接生成的详情批次分别为名画 260,170 字节和书法 528,343 字节，覆盖 539 件名画、585 件书法的变更记录，其中书法包含 31 个原文续页。

- **屏保复用通过。** 在 Windows 上运行 R5 的生产图库代码。屏保先请求和详情先提交两种顺序均为 0 次 JPEG 范围读取、0 字节 JPEG。换图对照成功下载并显示新图。
- **链路故障注入通过。** USB 分段请求失败后，Wi-Fi 就绪时由调度器接管，失败分段没有通过 USB 重读。同链路重试间隔通过一秒断言。
- **模拟 TF 提交及离线读回通过。** 用 R5 存储代码写入真实详情批次，注入提交失败时旧目录仍有效。用新进程打开模拟 TF 目录，读回两类图库共 2,694 个逐件摘要、全部变更记录及 31 个原文续页。另取实际批次中的 7 件长文共 34 页做离线翻页回归，重组全文的 SHA-256 与来源一致。

日志摘录如下。完整文件位于本地 `artifacts/tab5-r5-full-gallery-20261115/`，包括 `image-manifest-check.jsonl`、`actual-batches.log`、`native-saver-*.log`、`native-worker.log`、`actual-tf-write.log`、`actual-tf-cold-read.log` 和 `actual-reader.log`。

```text
SAVER_ORDER_FOREGROUND_FIRST image_ranges=0 jpeg_bytes=0 offline_RPC=0
SAVER_ORDER_COMMIT_FIRST image_ranges=0 jpeg_bytes=0 offline_RPC=0
SAVER_CHANGED_IMAGE_OK changed JPEG still downloaded and displayed
GALLERY_CATALOG_RETRY_OK USB backoff, same-link retry after one second, immediate Wi-Fi takeover, normal success cadence
GALLERY_USB_BATCH_YIELD_OK failed range returns once; ready Wi-Fi prevents another USB read
REAL_BATCH_COMMIT_OK painting bytes=500782 works=1765 failed_commit_preserved_previous=1
REAL_BATCH_COMMIT_OK calligraphy bytes=945022 works=929 failed_commit_preserved_previous=1
REAL_BATCH_COLD_READ_OK painting hashes=1765 changed=539 additional_pages=0
REAL_BATCH_COLD_READ_OK calligraphy hashes=929 changed=585 additional_pages=31
NATIVE_ANNUAL_LONG_READERS_OK works=7 link_facades=3; production worker, per-page copies, exact complete SHA-256, TF offline reload, rejected corrupted/cancelled replacement retains previous guide, frozen reader survives in-flight next-work completion and pack replacement, releases pages; no hardware claim
```

## 2026-11-16 真机全量迁移

使用 `321-R5` 和本机 0.6.19 图库批次测试桥接。从 TF 卡读回两类完整旧版目录，备份 `aibot/gallery` 后暂存原新版目录和批次文件，以 `2026.11.03.1` 为活动目录启动。设备随后通过 Wi-Fi 迁移到 `2026.11.12.1`，采样期间 JPEG 接收计数为 0 字节，TF 写入失败计数为 0。09:45:47 首次采到两类新版目录均完成。

| 图库 | 详情批次字节 | 接收耗时 | TF 阶段耗时 | 批次总耗时 | 提交进度 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 名画 | 218,744 | 398 ms | 306 ms | 6,959 ms | 1,765/1,765 |
| 书法 | 413,373 | 827 ms | 404 ms | 5,250 ms | 929/929 |

迁移期间设备回报图库页 `page=9`、`saver=0`。本地证据在 `artifacts/tab5-r5-full-gallery-20261115/`，包括 `card-before-20261116.json`、`card-old-baseline-20261116.json`、`tf-gallery-backup-20261116.log` 和 `physical-user-restart-20261116.jsonl`。

离线翻页通过。全量迁移后正常退出 Windows 桥接，操作前后本地桥接端口均拒绝连接。用户在 TAB5 上确认书法横屏翻图、竖屏翻图和原文翻页均正常。见 `offline-browse-bridge-state-20261116.json` 和 `offline-browse-result-20261116.json`。

恢复桥接后，以新版图库验证了「屏幕声音 → 每日书法 → 预览屏保」入口。设备采样记录从 `saver=0` 变为 `saver=1`，随后回到 `saver=0`。该观测发生于迁移完成后。见 `physical-saver-entry-20261116.jsonl`。

## 2026-11-16 全量批次联合模拟

名画和书法分别用实际两版全量包生成的压缩详情批次运行 R5 生产图库代码。测试以本地目录模拟 TF，预置完整旧版目录游标和代表性旧图，模拟屏保在详情提交前先取图。两类均复用旧图，JPEG 接收增量为 **0 字节**。

每类批次经模拟 USB 成功读入首段 49,152 字节，在第二段注入失败。此时旧目录仍为活动目录。Wi-Fi 就绪后，R5 在下一轮调度重新接收完整批次，USB 未重试，模拟 TF 提交成功。断开模拟链路并清空内存缓存后，两种方向均可翻图并读回新版详情。书法样本的 8 段原文逐段比对一致，离线 RPC 次数为 0。

| 图库 | 变更件数 | Wi-Fi 收到的批次字节 | JPEG 字节 | 模拟 TF 提交 |
| --- | ---: | ---: | ---: | ---: |
| 名画 | 539 | 260,170 | 0 | 1,765/1,765 |
| 书法 | 585 | 528,343 | 0 | 929/929 |

日志位于本地 `artifacts/tab5-r5-full-gallery-20261115/` 的 `full-migration-painting.log` 和 `full-migration-calligraphy.log`。

## 真机剩余验收

| 场景 | 状态与验收条件 |
| --- | --- |
| 书画屏保开启时迁移 | **待验。** 图库页的全量迁移和 TF 提交已通过。下次从同一旧版基线启动，迁移期间核实 `saver=1`，同时记录 JPEG 增量和 TF 提交。 |
| USB 中断后 Wi-Fi 接管 | **待验。** 在真实 USB 详情批次传输期间中断 USB，确认设备自动改用 Wi-Fi，并完成同一类图库的提交。 |
