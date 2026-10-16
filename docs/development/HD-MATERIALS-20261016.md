# 高清分发素材集 — 2026-10-16

用户授权整理当前高清素材集；中途明确要求 1.2 GB TIFF 不进入分发包。本次仅本地制作，不公开发布、不改桥接或 TAB5。

交付目录：artifacts/hd-materials-20261016-delivery。名画、书法各 366 件，分别提供 ZIP 和展开目录。按年度作品顺序组织，保留作者、机构、作品来源、图片来源、许可、像素尺寸和 SHA-256。每件作品一个目录，册页原素材不拆分为独立作品。CSV 为 UTF-8 BOM，方便 Excel 筛选。素材的路径全部相对化，desktop-index.relative.json 为后续导入支持保留，不能直接替换当前桥接的绝对路径索引；当前素材集不是现有 TAB5 图库导入 ZIP。

## 大 TIFF 分发处理

仅 cma-1954.369（汉宫春晓图）的 83933×4868 TIFF 做分发处理：原文件 1,225,801,468 字节，分发 JPEG 为 60000×3480、quality=95、subsampling=0，116,957,885 字节。有损缩放/编码明确记录在 derivativeOf 与 CSV“素材处理”列中。原 TIFF 本机保留且重新核对 SHA-256 不变；包内不含该 TIFF。其他文件字节不改，包括另一个约 127 MB TIFF。

调整该作品所有桌面页映射的 crop、dest、width、height，保持已有展示图哈希绑定和作品页序，所有边界检查通过。人物、树木和衣纹的局部对照图位于 artifacts/hd-materials-distribution-20261016/detail-comparison.png。新 JPEG 的 Windows 高清解码及相关 7 个名画页面抽查通过；不将该抽查写成全图库高清解码验收。

## 构建与验证

构建脚本 scripts/package_hd_art_materials.py；输入为 artifacts/gallery-desktop-hd-20261016/desktop-originals.json、当前安装的 DailyArt 两类图库、artifacts/hd-materials-distribution-20261016/overrides.json。输出目录必须不存在，防止覆盖旧素材。

复制后逐个校验素材哈希和尺寸；ZIP 完成后逐个解压流计算 SHA-256，对照展开目录清单；ZIP 本身另有 .sha256。每包附 LICENSE、THIRD_PARTY_NOTICES.md、README、CSV、catalog.json、相对索引和逐文件 SHA256SUMS.txt。许可继承来源记录，项目 MIT 不覆盖馆藏图片；本次没有重新作许可法律判断。

最终完成数量、压缩包大小与哈希以交付目录 validation.json 为准。早期 hd-materials-20261016、hd-materials-20261016-final 目录为包含大 TIFF 的中间产物，已标记 NOT-FOR-DELIVERY.md，不应交付。

最终两包完成：名画 1,789,830,728 字节、书法 2,186,111,602 字节；ZIP 共 2501 个成员逐项哈希通过，最终重新检查 732 件／1755 张及巨幅 TIFF 排除情况通过。
