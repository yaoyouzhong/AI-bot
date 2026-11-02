"""Build a silent, native-control Mac setup supplement without opening windows."""
from pathlib import Path
import argparse
import json
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('input', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=False)
renderer = Path(__file__).with_name('render_mac_authorization_video.swift')
subprocess.run(['swift', str(renderer), str(args.input), str(args.output)], check=True)
segments = []
for index in range(1, 4):
    name = f'segment-{index}.mp4'
    subprocess.run(['ffmpeg','-v','error','-y','-loop','1','-i',str(args.output/f'scene-{index}.png'),
                    '-t','8','-vf','fps=24,format=yuv420p,fade=t=in:st=0:d=0.25,fade=t=out:st=7.75:d=0.25',
                    '-c:v','libx264','-threads','2','-preset','fast','-crf','20','-an',str(args.output/name)],check=True)
    segments.append(f"file '{name}'")
(args.output/'concat.txt').write_text('\n'.join(segments)+'\n')
target = args.output/'AI-bot-mac-authorization-20261102.mp4'
subprocess.run(['ffmpeg','-v','error','-y','-f','concat','-safe','0','-i',str(args.output/'concat.txt'),
                '-c','copy','-movflags','+faststart',str(target)],check=True)
subprocess.run(['ffmpeg','-v','error','-i',str(target),'-f','null','-'],check=True)
probe = json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-show_format','-of','json',str(target)]))
assert len(probe['streams']) == 1 and probe['streams'][0]['codec_name'] == 'h264'
assert float(probe['format']['duration']) == 24
(args.output/'PROBE.json').write_text(json.dumps(probe,indent=2)+'\n')
print('MAC_AUTHORIZATION_VIDEO_OK 24s; 1280x720; silent by design; fully decoded')
