# QQ Music playback compatibility reference

- Reference: Kxnrl/NetEase-Cloud-Music-DiscordRPC, Kyle, MIT.
- Reviewed revision: `3a97a4b5c8b673ce2f69b0205adbc6a3ac861ba5`.
- Source: https://github.com/Kxnrl/NetEase-Cloud-Music-DiscordRPC/blob/3a97a4b5c8b673ce2f69b0205adbc6a3ac861ba5/Vanessa/Players/Tencent.cs
- Only the x86 initialization signature, current-song field offsets and string
  layout inform this adapter. The PE-file locator, relocation checks, bounded
  process reads, stable-song checks, exact metadata merge, artwork handling and
  multi-source selection were independently implemented for AI-bot.
- No upstream executable, memory-scanning library, injected plugin or complete
  player implementation is included. The original MIT notice is retained here.
- The reference describes the QQ Music 20.43 x86 layout. Matching signatures are
  required; other architectures/layouts fail closed and retain SMTC fallback.
- No installed QQ Music client was available for live acceptance on 2026-10-08.
- On 2026-10-09, local QQ Music `22.71.10.11.55` x86 was inspected read-only.
  The same signature and current-song layout are present. The first byte-store
  ends the title initializer; subsequent size/capacity stores belong to the next
  string. Locator checks now validate this interleaving and both adjacent strings.
  Native metadata/clock and public artwork were confirmed through live sampling;
  interactive pause/seek/track switching and device rendering remain separate.
