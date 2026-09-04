# Runtime asset policy

AI-bot distributes only its procedural `BYTE SPROUT` pet. No legacy sprite, vendor logo, screenshot, or third-party character is bundled.

The Windows bridge can send a user-selected PNG, JPEG, BMP, or first GIF frame as a private runtime pet. Import is accepted only when the image directory also contains `LICENSE`, `LICENSE.txt`, or `<image-name>.license.txt`, limited to 64 KiB. This is a provenance gate rather than a legal classifier: AI-bot verifies that a notice exists but does not claim that arbitrary notice text grants a particular right.

Images are limited to 20 MiB and 4096×4096, fitted into a black 112×112 RGB565 canvas, and sent over the authenticated USB resource protocol. The source image and notice are not copied into this repository, application settings, caches, logs, or release archives. The device stores only converted pixel data in LittleFS.
