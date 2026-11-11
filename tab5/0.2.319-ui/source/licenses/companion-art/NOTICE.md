# Codex and Clawd supplied artwork / 萌宠素材归属

On 2026-11-04 the AI-bot project owner explicitly confirmed that both supplied official asset sets have authorization for public redistribution with AI-bot firmware: “两套素材均有公开再分发授权”. This records the owner's confirmation for this release; public availability and software repository licenses are not used as substitutes for that confirmation.

- **Codex companion — OpenAI.** Original atlas from the installed Codex application, 1536×2288, frame size 192×208. Original SHA-256: `ac2990f24d55372411cebdc66a726e1fcbf5e80ec95378e2d25a6241abbedf88`.
- **Clawd — Anthropic.** Original indexed animation data used by the official developer site, <https://claude.dev>. Asset bundle: <https://claude.dev/_next/static/chunks/3o01xgcny9qbw.js>, SHA-256 `b45ebc4241bf8f98efd6bd3637e71930eb2d4d65ef5a16d68684f4d1e1ff20ba`. Extracted full reference-data SHA-256: `f42458f1fae656788de65917569017a8c706cf4ef191b415c496f7281c257158`. AI-bot uses walk, pool, kite, skate, astronaut, and the original standing frame. Website program code is not redistributed as AI-bot source.

Artwork, character designs and product marks remain their respective owners' property and are **outside AI-bot's MIT grant**. State meanings, frame selection, playback, positioning and procedural backgrounds are AI-bot contributions. No endorsement or broader trademark right is claimed. This notice does not grant rights beyond the recorded distribution scope.

本节覆盖上述官方素材随 AI-bot 固件及对应来源材料公开分发。来源、指纹、生成数据和相应声明随固件及源码材料保留。两套官方素材均在 MIT 授权范围之外。

## Legacy Claude headphones animation / 旧版 Claude 耳机动画

2026-11-11，项目维护者明确确认宣传片所用的原版戴耳机六帧可随 AI-bot TAB5 固件公开分发。素材来自 `yaoyouzhong/esp8266-ai` 提交 `5f52a39bd5c4245aff24bfa3a23cde2d62d57e3c` 的 `firmware/include/img/claude_sprite.h`。本机原版 APET 与该提交的 RGB565 像素逐帧一致，SHA-256 为 `8c9e76f5f42e041ecd135df5c060dc29f6c69eb5ec6ec6cc6c15381d22fc03e5`。授权范围为此六帧动画及其固件内转码版本。其第三方归属保留在项目 MIT 授权之外。

The project owner confirmed public distribution of these six frames with AI-bot TAB5 firmware on 2026-11-11. TAB5 uses the original order and 120 ms timing for Claude's working state in Overview and the Claude screensaver. The transcoder keys the original black background to transparency, preserves the black eyes, crops shared empty margins and packs the unchanged visible RGB565 values. The encoded include SHA-256 is `e3be2cbac23df48a2cde61e2bdece24abb3470387da659296aecbdb133cb088b`. The asset remains outside the project MIT grant.

## Native preview media

`docs/assets/screens/tab5-companion-savers-0312-preview.png` is a composite of two separately rendered production LVGL savers. The 2026-11-11 composite preserves every pixel of the original 1280 × 720 `companion-saver-working.ppm` and `claude-saver-working.ppm` renders from 0.2.312-ui. The clock is synthetic demonstration data; the image is not hardware photography and does not show both mascots simultaneously on a device. The user separately accepted the actual TAB5 placement, backgrounds and time typography. Its SHA-256 is registered in `licenses/materials.json`.

`docs/assets/screens/tab5-companion-savers-headphones-preview.png` keeps the Codex panel above and uses the native `headphones-saver-02.ppm` render from the TAB5 0.2.319-ui R2 candidate for Claude's working state. It shows one frame of the same headphone animation used in the project video. The panels are combined at native resolution with synthetic clock data. The new image's SHA-256 is registered in `licenses/materials.json`.

On 2026-11-11 the project owner authorized publication of this still on the Chinese and English repository homepages. The headphone sprite comes from `firmware/include/img/claude_sprite.h` in `yaoyouzhong/esp8266-ai` commit `5f52a39bd5c4245aff24bfa3a23cde2d62d57e3c`. The artwork remains outside the project's MIT grant.
