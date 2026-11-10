import json,subprocess,re,sys
from pathlib import Path
r=Path(__file__).resolve().parents[1];out=Path(r"C:\Users\yaououzhong\Work\outputs\AI-bot-pinyin-compression-20261103\stack-probe")
commands=json.loads((r/'firmware/build/compile_commands.json').read_text(encoding='utf8'))
for name in sys.argv[1:]:
 key=name.removeprefix('before-');x=next(x for x in commands if x['file'].replace('\\','/').endswith('/main/'+key))
 cmd=x['command'];obj=out/(name+'.obj')
 cmd=re.sub(r' -o .+? -c ', ' -fstack-usage -o '+obj.as_posix()+' -c ',cmd)
 if name.startswith('before-'):cmd=cmd[:cmd.rindex(' -c ')+4]+str(out/name)
 subprocess.run(cmd,cwd=x['directory'],check=True)
 print('STACK_COMPILED',name,flush=True)
