# Runtime asset policy

Optional page logos follow the same private-runtime boundary. Windows
`--import-local-legacy-logos <legacy-root>` validates both 40×40 header arrays,
undoes pre-swapped RGB565 words and saves only converted pixels under
`%LOCALAPPDATA%/AI-bot/page-logos`. macOS offers a local-import menu with source-level
support in its private pet cache. Resources 12/13 feed the mirror and device;
no logo pixels or source headers are included in the repository/release. This
import does not confer a redistribution license. macOS remains unbuilt/unverified.

AI-bot distributes only its procedural `BYTE SPROUT` pet. No legacy sprite, vendor logo, third-party screenshot, or third-party character is bundled. Reviewed offline captures of AI-bot's own UI are documented in [SCREENSHOTS.md](SCREENSHOTS.md).

The Windows and macOS bridges can send a user-selected PNG, JPEG, BMP, or animated GIF as a private runtime pet. GIFs are sampled into at most eight frames with aggregated per-frame durations; inputs above 1000 frames are rejected. Import is accepted only when the image directory also contains `LICENSE`, `LICENSE.txt`, or `<image-name>.license.txt`, limited to 64 KiB. This is a provenance gate rather than a legal classifier: AI-bot verifies that a notice exists but does not claim that arbitrary notice text grants a particular right.

Images are limited to 20 MiB and 4096×4096, fitted into a black 112×112 RGB565 canvas, and sent over the authenticated USB resource protocol. The source image and notice are not copied into this repository, application settings, caches, logs, or release archives. The device stores only converted pixel data in LittleFS.

Windows additionally saves converted APET pixels to `%LOCALAPPDATA%/AI-bot/pet.apet`
for restart/reconnect and mirror rendering, outside the repository. The same imported
custom animation can be selected for both roles or separately for Claude/Codex. Per-role selections persist in `claude-selected.apet` and `codex-selected.apet`; replacing a selection retains a `.previous` backup. Restore-default uses that role's imported local default and fails without changing the selection when the default is missing. The explicit
Windows `--import-local-legacy-pets <legacy-root>` command reads the user's local
Claude/Codex default sprite headers into separate `claude-legacy.apet` and
`codex-legacy.apet` caches, preserving six frames and 120ms frame intervals while
preserving original dimensions (Claude 111x120, Codex 120x120) in APET v2 without resampling. These private caches are not bundled
or licensed by this project. Provider pages select their corresponding local cache;
the pet page follows the working provider. User-imported custom animation takes
precedence. Windows gallery/manual motion selection is implemented; full idle/alert
animation behavior parity remain pending. macOS APET
conversion and host-side restart caching (`PetCache.swift`) are implemented in source,
but remain unverified until a Mac build and restart/reconnect acceptance.

The robot application icon is separately provenance-verified maintainer-generated
artwork (see `PROVENANCE.md`); it is not a vendor or upstream logo.

The petdex picker downloads only from `https://assets.petdex.dev`, refuses redirects,
limits manifests to 8 MiB and sheets to 20 MiB, and accepts only known 8x9/8x11 grids.
It uses the public format documented at https://github.com/crafter-station/petdex
and the nine motion rows in `src/lib/pet-states.ts`. Only a public manifest and
user-selected converted pixels persist outside the repository. Petdex describes
its submissions as fan art and does not claim underlying IP rights; this integration
does not declare these assets MIT or include them in public distribution.

When no custom animation is available, the Windows mirror and firmware quota/pet pages draw the built-in BYTE SPROUT without importing a file. Mac mirror source provides the same fallback but remains platform-unverified. Existing user selections retain priority.
