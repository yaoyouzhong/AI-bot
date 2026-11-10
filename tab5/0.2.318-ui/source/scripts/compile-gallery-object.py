import json, subprocess, pathlib
p=pathlib.Path(__file__).resolve().parents[1]/'firmware/build/compile_commands.json'
x=next(x for x in json.loads(p.read_text(encoding='utf8')) if x['file'].replace('\\','/').endswith('/main/'+__import__('sys').argv[1]))
raise SystemExit(subprocess.run(x['command'],cwd=x['directory']).returncode)
