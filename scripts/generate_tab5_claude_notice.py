"""Generate a local TAB5 test prompt using the installed macOS Tingting voice.

The output is raw signed little-endian 16-bit, 16 kHz mono PCM. It is a local
candidate; distribution rights and final voice choice are separate checks.
"""
import pathlib
import subprocess
import sys
import tempfile
import wave

if len(sys.argv) != 2 or sys.platform != "darwin":
    raise SystemExit("Usage: python3 scripts/generate_tab5_claude_notice.py OUTPUT.pcm (macOS)")
output = pathlib.Path(sys.argv[1])
with tempfile.TemporaryDirectory(prefix="aibot-claude-voice-") as directory:
    source = pathlib.Path(directory) / "speech.aiff"
    converted = pathlib.Path(directory) / "speech.wav"
    subprocess.run(["say", "-v", "Tingting", "-o", str(source), "Claude任务已完成"], check=True)
    subprocess.run(["afconvert", "-f", "WAVE", "-d", "LEI16@16000", "-c", "1", str(source), str(converted)], check=True)
    with wave.open(str(converted), "rb") as audio:
        if (audio.getnchannels(), audio.getsampwidth(), audio.getframerate()) != (1, 2, 16000):
            raise ValueError("Unexpected PCM format")
        output.write_bytes(audio.readframes(audio.getnframes()))
print("CLAUDE_NOTICE_PCM_READY 16 kHz mono; generated locally with macOS Tingting")
