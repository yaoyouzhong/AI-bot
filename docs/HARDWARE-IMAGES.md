# 硬件介绍配图 / Hardware introduction images

2026-10-17：主页硬件介绍使用两张产品合成图，帮助首次使用者理解设备外形与用途。它们不是实拍，也不作为真机验收或账户状态证据。两图不按实物比例排列。

Both homepage hardware images are composites, not photographs of AI-bot running on physical devices. They are not acceptance evidence or live account readings, and are not shown to a common physical scale.

## ESP8266 小屏 / Compact display

- 文件：`assets/hardware/esp8266-product-composite.png`。
- 界面参考：本仓库 `assets/screens/codex.png`，沿用[原生界面与桌宠来源记录](SCREENSHOTS.md)。
- 外形参考：[人人都懂物联网：SD2 桌面天气小电视](https://getiot.tech/projects/sd2/)，参考图 `https://static.getiot.tech/SD2-Project.png`。原照片未加入仓库；合成图重新生成白底、光照和单台设备构图。实际外壳以所购设备为准，兼容性按[硬件配置](FLASH_BUILD.zh.md#1-核对硬件并留好回退材料)确认。
- 外形参考照片及第三方硬件设计权利仍归各自权利人，不纳入本项目 MIT 许可。界面中的账户类型、额度、日期为演示数据。

The SD2 reference informed the enclosure shape; its original photograph is not redistributed. The background, lighting and single-device composition were newly generated. Actual enclosures vary. Third-party reference photography, hardware designs and pre-existing artwork retain their respective rights.

## M5Stack TAB5

- 文件：`assets/hardware/tab5-product-composite.png`。
- 产品实图来源：[M5Stack 官方 TAB5 产品页](https://docs.m5stack.com/zh_CN/core/Tab5)，原图 `https://m5stack-doc.oss-cn-shenzhen.aliyuncs.com/1132/C145_06.webp`。
- 最终底图：用户确认并重新提供的斜侧合成图，源于上述官方照片的生成式编辑，并非未经修改的官方实拍。最终处理保留这张底图的机身、角度和构图，仅以本仓库 `assets/screens/tab5-quota.png` 原生截图替换屏幕区域。账户类型、额度、日期为原生界面的固定演示数据。
- 合成方式：使用 `scripts/compose_tab5_product_image.py`，对完整截图进行一次四点透视变换与 4 倍抗锯齿采样，不重绘文字、控件或机身。屏幕之外与用户确认底图逐像素一致；这不表示底图与官方原照片逐像素一致。
- 官方产品图及其衍生部分版权归 M5Stack，不纳入本项目 MIT 许可。本图仅作硬件识别与本项目界面说明；不表示 M5Stack 背书。
- 5 英寸触控屏规格来自上述官方产品页。USB / Wi-Fi / 蓝牙为本项目的 TAB5 连接方式。

The final TAB5 base is the user-approved angled composite derived from generative editing of M5Stack's official photo, not the untouched official photograph. A single planar perspective transform places the complete native UI capture onto its LCD. All pixels outside the LCD remain identical to the approved base. M5Stack retains rights to the product photograph and its derivative portions; these are not covered by the project's MIT license. No vendor endorsement is implied.

### TAB5 侧面接口实图 / Official side-port photo

- 文件 / file: `assets/hardware/tab5-side-official.png`.
- 来源 / source: [M5Stack 官方 TAB5 产品页](https://docs.m5stack.com/zh_CN/core/Tab5)，[官方侧面照片 C145_10.webp](https://m5stack-doc.oss-cn-shenzhen.aliyuncs.com/1132/C145_10.webp).
- 仅裁去原图留白：1200 × 1200 原图裁剪框为 `(282, 521, 947, 667)`，输出 665 × 146；未缩放、重绘、锐化或改变接口。版权归 M5Stack，不纳入项目 MIT 许可。主页作为可展开的补充实图，主合成图保持不变。
- Cropped only to remove whitespace, using the coordinates above. No scaling, redrawing, sharpening or port changes. M5Stack retains copyright; the photo is excluded from the project's MIT license. The homepage provides it as an expandable detail; the approved main composite is unchanged.

## 生成与验证 / Generation and validation

小屏与 TAB5 底图使用内置 `image_gen.imagegen`。TAB5 最终屏幕改用 Pillow / NumPy / OpenCV 精确合成，不再使用生成式编辑。原始界面截图保持不变。最终图片 SHA-256 登记在 `licenses/materials.json`。

The compact image and TAB5 base were created with the built-in image tool. The final TAB5 screen was composited deterministically using Pillow, NumPy and OpenCV; no further generative editing was used. Original native screenshots remain unchanged.

### 小屏提示词 / Compact-display prompt

```text
Use case: product-mockup / compositing. Create ONE clean product illustration for the AI-bot GitHub README, on a pure white background, square 1024x1024 composition. Input image 1 is the exact current AI-bot ESP8266 screen capture; Input image 2 shows the SD2 mini desktop display hardware form for reference only. Show ONE compact white SD2-style desktop screen with the square LCD, broad white bottom chin, rounded white enclosure and slightly rearward-tilted integrated wedge body seen on the LEFT white device in reference 2. Front view with a very slight three-quarter angle so the right side has a little depth, occupying 75% of image height. Create a NEW clean studio composition and original lighting; do not copy the reference photograph's desk, background, cables, second turquoise device or photograph pixels. Mount image 1 as the complete undistorted screen content; preserve the pixel-art blue mascot, black background, green status border, all labels and figures exactly, including CODEX, PLUS, 5H, 18%, 2h 0m, WK, 42%, 3d 0h, and the two upper-right R*1 date rows. Do not invent UI, change the membership label, redesign the UI, add weather, or blur the screen. No new text outside the screen, no logo, no badges, no watermarks, no decorative objects, no feet or knobs absent from the reference housing. This is an openly labeled product illustration, not evidence of real hardware photography. Balanced generous white margins with a subtle neutral contact shadow.
```

### TAB5 初版提示词 / Initial TAB5 prompt

```text
Use case: precise-object-edit / compositing. INPUT 1 is an official M5Stack TAB5 product photograph from a low three-quarter angle on a white background. INPUT 2 is the native AI-bot quota screen, 1280x720. Change ONLY the active LCD display in input 1, inserting the FULL input 2 screen with the correct planar perspective transform to the four corners of the LCD. Preserve the exact original three-quarter camera angle and all hardware photographic details: device thickness, white front and right side walls, black glass bezel, corner radii, brass antenna connectors, side labels, microphone holes, visible USB-A, USB-C, audio jack and red port. Absolutely do not flatten it into a straight-on tablet, do not replace the body with a generic device, and do not add a stand or extra device. Keep the original clean white background and soft studio lighting, a square output with the complete device centered and generously padded. The hardware should be large, about 85% of the image width. The screen must look like input 2 with its original dark colors, layout, readable Chinese text, chart and buttons. Preserve Codex, PRO, 88%, 76, 15:50:10, 5d 14h and the three reset cards, fitting them to the existing perspective. Do not invent screen contents or float text outside the screen. The result is a labeled product composite for a hardware guide, not evidence of hardware operation.
```


### TAB5 透视与对齐修订 / Perspective and alignment revision

2026-10-17：对斜侧合成图进行屏幕透视修订，调整卡片间距、两列布局和底部按钮对齐，保留机身视角与外观；另校正首条重置日期。仍为生成式合成图，不保证与原始截图逐像素一致。

The angled composite was revised to improve screen perspective, card spacing, column layout and bottom navigation alignment while retaining the hardware view. The first reset date was also corrected. This remains a generated composite, not a pixel-exact screen transfer.

```text
Use case: compositing / precise-object-edit.
Image 1 is the edit target: existing angled TAB5 product composite. Image 2 is the exact original flat 1280x720 AI-bot quota UI that MUST replace its entire active screen.
Fix ONLY the screen plane and its content. Preserve the hardware body, camera angle, visible chassis thickness, side ports, black bezel, white background, shadows, and square composition of image 1.
The current screen has distorted and misaligned layout. Treat image 2 as ONE rigid rectangular texture, apply ONE global planar perspective projection to fit the four active LCD corners in image 1. Do not independently skew, stretch, redraw, resize or reposition individual UI widgets. All horizontal UI edges must share the same perspective vanishing point as the LCD top and bottom edges. All vertical UI edges must share the LCD left and right edge perspective. Straight lines must remain straight with no bending, bulging, waving or nonuniform scaling.
Preserve the entire image 2 including margins and bottom navigation, with no cropping. Precisely preserve its two-column grid: left column ~62% width with quota panel above chart, right column ~37% width with reset list; both columns begin at the same screen-coordinate y=156 and end at y=604. Keep the four bottom navigation buttons aligned on the same baseline, same height, and evenly spaced. Keep card gutters and outer margins consistent in perspective. Top status icons and header text aligned together. Preserve every layout proportion from image 2.
Exact key contents: 模型额度, Codex, PRO, 缓存, 本周已使用, 88%, 距重置 5d 14h, 近7日 · 周额度使用（百分比）, 额度重置, 周额度 · 09-29 05:50, 重置卡, 3 张, 2026-10-04 11:26, 2026-10-05 13:38, 2026-10-23 00:27; bottom 总览, Codex, 额度, 应用. Preserve chart data and dates from image 2. No UI redesign, no new text, no new hardware, no gloss covering content. Use sharp clean text and edges. The desired result looks like a real flat LCD viewed at an angle, not an elastic deformed layout.
```

```text
Use case: precise-object-edit. Edit this existing TAB5 product composite with ONE tiny text correction. In the right side reset list, the FIRST row mistakenly reads 2026-10-41 11:26. Change that date to exactly "2026-10-04 11:26". Preserve every other pixel, all layout alignment, the other two dates, whole device, camera perspective, screen plane, white background, spacing, borders and icons. No other change. The first reset row must be 2026-10-04 11:26, digits in exact order year 2026 month 10 day 04 time 11:26.
```

### TAB5 最终精确合成 / Final deterministic composite

用户选择保留斜侧底图，弃用改变机身视角的近正面版本。最终仅将完整原生截图按屏幕四角贴合，不改变底图其他区域。

The user selected the angled base and rejected the near-front variant. The final edit replaces only the LCD content with the complete native screenshot using one homography.

- 底图 / approved base SHA-256: `05683454a26c311aae8f4aa92eac1a54f52753125d20013f2f72cd6b1a2266c3`.
- 原生截图 / native UI SHA-256: `0f89b687f133ff446d69aff1c88457d981bf09ab67fc5f2afb341a5d4ef50e9e`.
- 四角 / LCD corners (top-left, top-right, bottom-right, bottom-left): `(372,362)`, `(1136,525)`, `(944,849)`, `(121,605)` on the 1254 x 1254 base.
- 屏幕外变更像素 / changed pixels outside LCD: **0**; unchanged pixels: **1,301,085**.
- 四角投影误差 / maximum corner projection error: **0 px**.
- 复现脚本 / script: [`compose_tab5_product_image.py`](../scripts/compose_tab5_product_image.py). Dependencies: Pillow, NumPy, OpenCV.
- 底图与验证报告本地保存在 / base and verification report are retained locally under `artifacts/hardware-intro-preview/`.

```powershell
python scripts/compose_tab5_product_image.py --hardware artifacts/hardware-intro-preview/tab5-approved-base.png --screen docs/assets/screens/tab5-quota.png --output docs/assets/hardware/tab5-product-composite.png --report artifacts/hardware-intro-preview/tab5-composite-verification.json
```
