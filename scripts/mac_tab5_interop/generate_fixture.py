"""Public, synthetic TAB5 v1 vectors. Never reads a device or user profile."""
import hashlib
import hmac
import json
from pathlib import Path

from cryptography.hazmat.primitives.ciphers.aead import AESGCM


def generate():
    key = bytes(range(32))
    nonce = bytes(range(12))
    challenge = "0123456789abcdef0123456789abcdef"
    device = "001122334455"
    settings = dict(revision="1234abcd", brightness=75, volume=40, muted=False,
                    selected=-1, cycle=True, interval=15, saverMinutes=5, alerts=True,
                    enabled=[1] * 8, order=list(range(8)))
    report = dict(kind="display-settings", requestId="2" * 32, session="1" * 32,
                  issuedAt=1800000000000, ok=True, error="", settings=settings)
    clear = json.dumps(report, ensure_ascii=False, separators=(",", ":"), sort_keys=True).encode()
    encrypted = AESGCM(key).encrypt(nonce, clear, challenge.encode())
    packet = nonce + encrypted[-16:] + encrypted[:-16]

    def proof(text):
        return hmac.new(key, text.encode(), hashlib.sha256).hexdigest()

    message = f"POST|/tab5/v1/rpc|{device}|{challenge}|{hashlib.sha256(packet).hexdigest()}"
    discover = f"TAB5_DISCOVER_V1|{device}|{challenge}"
    answer = f"TAB5_BRIDGE_V1|{device}|{challenge}|192.168.1.20|8765"
    bad = []
    for field, value in [("brightness", 9), ("volume", 101), ("selected", 0), ("interval", 11),
                         ("saverMinutes", 2), ("enabled", [0] * 8), ("order", [0] * 8), ("revision", "bad")]:
        bad.append({**settings, field: value})
    return dict(note="PUBLIC SYNTHETIC TEST DATA ONLY", keyHex=key.hex(), nonceHex=nonce.hex(),
                challenge=challenge, device=device, clearHex=clear.hex(), packetHex=packet.hex(),
                proofMessage=message, proof=proof(message), settings=settings, invalidSettings=bad,
                discovery=discover + "|" + proof(discover), discoveryReply=answer + "|" + proof(answer))


if __name__ == "__main__":
    target = Path(__file__).resolve().parents[2] / "mac-app/Tests/AIBotBridgeTests/Fixtures/tab5-v1.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(generate(), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("WROTE_PUBLIC_SYNTHETIC_TAB5_FIXTURE")
