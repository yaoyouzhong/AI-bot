"""Read only TAB5 identity and channel diagnostics. Never prints provisioning packets."""
import argparse
import json
import time
import serial

parser = argparse.ArgumentParser()
parser.add_argument("--port", required=True)
args = parser.parse_args()
port = serial.Serial(port=None, baudrate=460800, timeout=0.3, write_timeout=2)
port.dtr = True
port.rts = False
port.port = args.port
port.open()
with port:
    port.reset_input_buffer()
    port.write(b'@AIBOT {"version":1,"type":"tab5_ping"}\n')
    deadline = time.monotonic() + 5
    while time.monotonic() < deadline:
        line = port.readline(4096)
        if not line.startswith(b"@AIBOT "):
            continue
        try:
            reply = json.loads(line[7:])
        except (ValueError, UnicodeError):
            continue
        if reply.get("type") != "tab5_hello":
            continue
        fields = ("version", "deviceId", "firmware", "active", "wifiConnected", "bleReady", "ageMs", "bleConnected", "bleConnectCount", "bleDisconnectReason", "assetsReady", "assetsCached", "uiPage", "uptimeMs", "resetReason")
        print(json.dumps({k: reply.get(k) for k in fields}, ensure_ascii=False))
        break
    else:
        raise SystemExit("TAB5 did not answer the client protocol; firmware may still be UIFlow2.")




