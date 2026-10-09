# NetEase playback compatibility reference

- Reference: Kxnrl/NetEase-Cloud-Music-DiscordRPC, Kyle, MIT.
- Reviewed revision: `3a97a4b5c8b673ce2f69b0205adbc6a3ac861ba5`.
- Source: https://github.com/Kxnrl/NetEase-Cloud-Music-DiscordRPC/blob/3a97a4b5c8b673ce2f69b0205adbc6a3ac861ba5/Vanessa/Players/NetEase.cs
- Only the two x64 instruction signatures and the current-song/playback field
  layout inform this adapter. The bounded PE locator, checked Win32 reads,
  handle lifecycle, exact-song metadata parser and bridge integration are
  independently implemented for AI-bot. No upstream executable, dependency,
  injected plugin or whole source file is included.
- Original copyright/license text is retained in `LICENSE` alongside this file.
- Locally tested client: NetEase Cloud Music `3.1.40.205461`, Windows x64.
  Matching signatures do not promise compatibility with every future release.
