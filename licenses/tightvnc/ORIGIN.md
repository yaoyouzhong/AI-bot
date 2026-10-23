# TightVNC 2.8.90 optional component

Upstream: https://www.tightvnc.com/download/
License information: https://www.tightvnc.com/licensing.php
License text: `LICENSE.txt`, extracted without modification from the official
source archive member `tightvnc-2.8.90-gpl/wix-installer/LICENSE.txt` (GNU GPL v2).

The Windows packaging script fetches and checks these exact upstream artifacts:

| Artifact | SHA-256 |
| --- | --- |
| `tightvnc-2.8.90-gpl-setup-64bit.msi` | `9693cb4cdc2e7e482e8e3f5890415b57ef87de3369fa5c337db516218a1170ce` |
| `tightvnc-2.8.90-src-gpl.zip` | `f3f76393a5865e1f1919c78c3eb43b9c937cf738927c2bfc920ecbd3913b061c` |

Artifact URLs are `https://www.tightvnc.com/download/2.8.90/` plus the filenames
above. The MSI publisher is OOO GlavSoft. Install-time verification checks both
the pinned hash and Windows Authenticode trust. The full corresponding source
ZIP accompanies the unchanged MSI in every Windows package that bundles it.
No Viewer, firewall exception or network-facing service access is enabled by
AI-bot's installation action. Existing installations are never silently replaced.

官方 MSI 与完整对应源码一起分发；许可、来源及校验记录随包保留。AI-bot
只通过 RFB 使用独立服务，不并入 TightVNC 源码；安装及卸载分别由用户发起。

Existing-install interoperability additionally checks the documented vendor
registry format against the official `util/VncPassCrypt.cpp` and `util/DesCrypt.cpp`
in this fixed source archive. Only the functional storage format and public
encoding-key constant are used; conversion is independently implemented with
.NET's DES provider. No vendor encryption routines or tables are incorporated.
Imported credentials remain in memory and original-user DPAPI storage only;
they are never displayed, logged or passed on command lines.
