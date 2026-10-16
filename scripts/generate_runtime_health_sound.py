"""Original cuckoo-style two-note synthesis; no recorded or third-party audio."""
import math
import random
import struct
import sys
import wave
from pathlib import Path

rate = 16000
dry = [0.0] * (rate * 2)
random_source = random.Random(20261015)
for pair in [0.0, 1.0]:
    for offset, duration, pitch, gain in [(.08, .19, 659.25, .25), (.34, .31, 523.25, .29)]:
        phase = 0.0
        for n in range(round(duration * rate)):
            age = n / rate
            fraction = age / duration
            frequency = pitch * (1 + .025 * math.exp(-age / .018) - .018 * fraction)
            phase += 2 * math.pi * frequency / rate
            attack = min(1, age / .018)
            release = min(1, (duration - age) / .065)
            envelope = attack * release * (.94 + .06 * math.sin(math.pi * fraction))
            body = math.sin(phase) + (.16 - .08 * fraction) * math.sin(phase * 2) + .025 * math.sin(phase * 3)
            breath = random_source.uniform(-1, 1) * .006
            dry[round((pair + offset) * rate) + n] += gain * envelope * (body + breath)

wet = dry.copy()
for seconds, gain in [(.045, .10), (.095, .045)]:
    delay = round(seconds * rate)
    for n in range(delay, len(wet)):
        wet[n] += gain * dry[n-delay]

output_path = Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1] / 'windows-app/AIBotBridge/Assets/runtime-health.wav'
samples = b''.join(struct.pack('<h', round(max(-1, min(1, sample)) * 32767)) for sample in wet)
with wave.open(str(output_path), 'wb') as output:
    output.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
    output.writeframes(samples)
if len(sys.argv)>2:
    Path(sys.argv[2]).write_bytes(samples)
print('RUNTIME_HEALTH_CUCKOO_READY 2 seconds; two calls; 16 kHz PCM mono')
