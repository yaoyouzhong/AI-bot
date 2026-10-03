# TAB5 0.2.83: bounded identity and truthful link indicators

On 2026-10-02, .082 boot verification and a 603.8-second observation passed
without another panic, but no authenticated BLE data frame completed. Device
service reconnect and a full normal bridge restart did not clear the timeout.
The lit TAB5 icon represented `ble_connected`, while Windows required an
authenticated frame acknowledgement. These were different readiness criteria.

An independent read-only probe ran after normal bridge exit. Uncached service
discovery succeeded with three services; the TAB5 service opened successfully
and exposed all four characteristics. Its identity `ReadValueAsync` timed out.
Earlier probes while the bridge was running encountered `SharingViolation` and
`AccessDenied`; those do not establish a Windows permission problem. The
device's disconnect reason 531 is HCI 0x13 plus the NimBLE 0x200 base (remote
termination), not a supervision-timeout code.

The existing identity value exceeded the ATT 512-octet attribute limit, even
though the negotiated MTU was 517. The production callback regression produces
553 bytes with representative numeric values and 744 with maximum values.
See the [Bluetooth ATT specification](https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core-54/out/en/host/attribute-protocol--att-.html).
This is a confirmed protocol defect on the failing read path; hardware must
still confirm that correcting it resolves the observed timeout.

The .083 callback retains the identity, nonce, acknowledgement, all three HMAC
proofs, resource identifiers, firmware version, telemetry capability and window
negotiation. It omits optional `link`, `mailboxStats` and `rpcPacing` diagnostics
and the redundant `rpcMailboxWindow=8`: the existing Windows client defaults
that field to `mailboxWindow=8`, while notifications still advertise 32.
It uses a 513-byte buffer including NUL and rejects truncation. No mandatory
field is truncated. The periodic identity read also no longer queries the
controller PHY solely for an optional diagnostic field. Bulk interval/PHY/data
length requests and bounded mailbox flow remain unchanged.

Native production-callback tests verify unchanged core JSON fields, all hex
values, authentication/read errors and mbuf failure paths. Replies measure
451 and 492 bytes; the maximum case uses UINT64_MAX, a 31-byte firmware version
and the full 26-byte resource identifier. Actual ELF stack budgets pass: the
identity callback is 256 bytes; .082's stack protection and nonce/proof fix
remain. ESP-IDF build, image validation and display ISR IRAM inspection pass.

The header now renders grey for a disconnected physical link, amber for a
physical link without fresh authenticated state and green after valid state
within eight seconds. Each channel is checked independently, including backup
channels. Full LVGL preview passes and covers connected-but-unready BLE,
ready Wi-Fi, and a disconnected link overriding stale readiness.

Candidate and evidence: `artifacts/development/link-status-083-20261002`.
The firmware is not installed yet; BLE recovery, stability and device appearance
remain pending. The current bridge is unchanged and its original automatic
cycle was verified after the user restored it. Tool-initiated startup after the
isolated probe was rejected by automatic approval with only `blocked by policy`;
the user then ran the prepared current-version startup script successfully.
