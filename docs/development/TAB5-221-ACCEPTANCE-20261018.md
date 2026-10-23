# TAB5 0.2.221-ui 部署与集中验收（2026-10-18）

## 当前结果

用户明确批准配套桥接和0.2.221-ui刷入，要求Wi-Fi组件不安装、刷后集中验证要点。已部署、设备回报新版本，正式USB精确启动核验通过。实际解锁速度、黑屏恢复和取消体验仍待用户集中验收，不将本地模拟或启动通过写成解锁通过。

## 精确对象和证据

- 设备e8f60ae2ec56；固件SHA-256：`2a4336baf2f912da91575ca39e7a3361790b1861d96bca851457649ab1b103e0`，7,115,376字节。
- ELF SHA-256：`6c45a7745cad24476c890dd66e4a089013d5011cb3dbdbef0a495ba7ae02dfd6`。
- 桥接DLL：`582f1826bbd3086820daa9079a693df19da9d0b3bfecbad5b9b7f964c24133bb`；PDB：`9daa604d041a090b7f855e3d49cf1e12aed148deeece5bb0d29a843f0c33bbdc`。仅替换DLL/PDB，EXE/deps/runtimeconfig哈希不变。
- 批准清单：`93e89d0575482d3642283a974f4b4f127101e2934563d2b27bc0ebb2a3b386eb`；原JSON未改写。标准脚本已归档并更新固定交付路径，先前.220包和运行文件均有回退备份。
- 桥接仍从原固定Release路径运行，普通Windows桌面用户资料已检查；没有重配对、安装Wi-Fi组件、修改系统电源／登录策略或执行ESP8266恢复轮播命令。

## 实际执行中的边界

最初两个退出请求没有使原桥接退出，未替换文件；失败结果保留。随后通过现有Explorer桌面对象启动同一部署脚本，父进程为Explorer，正常退出、替换并重启成功。没有强杀桥接或修改资料目录保护。

正式远程开始升级请求被现有Busy检查拒绝；随后诊断已见精确新包的完整Wi-Fi分块压缩传输（4,868,817字节，8,094ms），未重复发起。传输由哪一次设备端操作触发没有完整证据，不能把被拒绝的请求写成成功。随后设备重启并回报.221；USB连接就绪后正式核验返回通过。传输诊断的“等待设备校验”是保留的传输阶段提示，最终以精确启动核验结果为准；不是蓝牙OTA测速证据，也不涉及Wi-Fi解锁组件。

正式核验核对设备身份、版本、精确ELF、分区VALID和三次递增UI心跳。核验前曾观察到一次failed/IOException阶段，未将其当成最终通过；USB连接就绪后的正式核验结果如下：

```
启动核验：verified; firmware=0.2.221-ui; elfSha256=6c45a7745cad24476c890dd66e4a089013d5011cb3dbdbef0a495ba7ae02dfd6; partition=ota_1; state=VALID; previousStage=9; previousOffset=7115376; previousTotal=7115376; previousError=0; previousStackFree=3904; otaMs=[0,0,0]; previousOtaMs=[18519,7960,18085]; previousOtaPhases={transport=1,prepareMs=8238,writeMs=9846,freeWaitMs=0,readyWaitMs=33,verifyMs=925,installMs=19748}
```

记录：`artifacts/development/ble-unlock-simple-20261018/deployment/`中的authorization.json、backup-hashes.json、deployment-result.json、file-verification.json、ota-start-result.json、boot-verification-result.json、boot-verified-diagnostics.json与acceptance-status.json。当前USB为COM9已连接，蓝牙USB优先待命；功能验收前需拔USB回到仅蓝牙。PIN未收集、未写日志、助手未发送登录按键。

## 集中验收要点

先拔掉USB，保持仅蓝牙并确认控制页音量正常，再集中反馈：

1. 普通PIN页：面板只显示“请输入 Windows PIN（不保存）”，输入并提交后正常解锁，确认提交停顿是否明显缩短。
2. 自然黑屏约2–3秒：点原解锁按钮，等准备结束，电脑应到PIN页、两边输入前为空、数字键可用，再完成解锁；如首次未成功，提示应清楚，关闭重开可重试。
3. 取消：输入少量本地测试数字但不提交，直接取消；电脑仍锁屏、无新增PIN圆点，重开TAB5输入为空。若过程中自然遇到失败，确认不会自行重发PIN；不要求故意输错真实PIN。

一次反馈结果或异常即可，不再逐项发问。新版本功能尚未验收，旧.220的成功记录不自动迁移。

## 用户集中验收：三项通过

用户明确反馈三项均通过：提交停顿明显缩短；刚黑屏也可正常解锁；取消后电脑和TAB5输入框都为空，已成功检测电脑解锁状态。本次.221对应的集中功能验收通过。新增要求是确认解锁后自动关闭面板，属于下一份候选的变更，不能提前记为.221已有行为。PIN未收集。
