"""Build self-contained HD gallery bundles from verified distribution materials."""
import argparse,hashlib,json,zipfile,tempfile
from pathlib import Path

def digest(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def build(materials,packs,output):
 output.mkdir(parents=True,exist_ok=False);results=[]
 for category in ('painting','calligraphy'):
  source=materials/f'AI-bot-HD-{category.title()}-2026.10.16.zip'
  target=output/f'AI-bot-Gallery-HD-{category.title()}-2026.10.16.zip'
  folder=packs/category;manifest=json.loads((folder/'PACK.json').read_text(encoding='utf-8-sig'))
  with tempfile.TemporaryDirectory(dir=output) as temp:
   standard=Path(temp)/'standard.zip'
   with zipfile.ZipFile(standard,'w',zipfile.ZIP_STORED,allowZip64=True) as z:
    for name in ['PACK.json',*manifest['files']]:
     p=folder/name
     if name!='PACK.json' and digest(p)!=manifest['files'][name]:raise ValueError('Standard source hash differs: '+name)
     z.write(p,name)
   with zipfile.ZipFile(source) as src,zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=1,allowZip64=True) as dst:
    prefix=src.namelist()[0].split('/')[0]+'/';sums={}
    expected={line[66:]:line[:64] for line in src.read(prefix+'SHA256SUMS.txt').decode('utf-8-sig').splitlines()}
    for entry in src.infolist():
     rel=entry.filename[len(prefix):]
     if rel in ('SHA256SUMS.txt','README.md'):continue
     h=hashlib.sha256()
     with src.open(entry) as inp,dst.open(prefix+rel,'w',force_zip64=True) as out:
      while data:=inp.read(1024*1024):h.update(data);out.write(data)
     if h.hexdigest()!=expected[rel]:raise ValueError('Material input differs: '+rel)
     sums[rel]=h.hexdigest()
    readme=f'''# 高清完整图库 / Complete HD gallery

类别：{category}。366 件作品。包含高清图片、作品目录、简介、赏析、已核实原文和标准展示图。
在桥接程序“屏保图库 → 安装图库包…”选择此 ZIP，一次安装即可使用，无需另装标准版。
电脑优先使用高清图片，素材不可用时自动回退标准展示图。TAB5 继续使用适配设备的展示图，不传输高清原图。
包含标准版图库 {manifest['version']}。standard.zip 是内部组件，无需手动解压。
巨幅汉宫春晓图使用处理后的 JPEG，未包含 1.2 GB TIFF；逐件处理和来源记录保留。

Import this ZIP through Artwork collections → Install gallery package. It includes images, guides and desktop HD sources; no separate standard package is needed. TAB5 uses the included device previews. See THIRD_PARTY_NOTICES.md and per-work attribution for rights.
'''.encode('utf-8')
    dst.writestr(prefix+'README.md',readme);sums['README.md']=hashlib.sha256(readme).hexdigest()
    dst.write(standard,prefix+'standard.zip',compress_type=zipfile.ZIP_STORED);sums['standard.zip']=digest(standard)
    dst.writestr(prefix+'SHA256SUMS.txt',''.join(f'{h}  {name}\n' for name,h in sums.items()))
   with zipfile.ZipFile(target) as z:
    for name,h in sums.items():
     with z.open(prefix+name) as f:
      if hashlib.file_digest(f,'sha256').hexdigest()!=h:raise ValueError('Output hash differs: '+name)
   result=dict(category=category,archive=target.name,bytes=target.stat().st_size,sha256=digest(target),verifiedFiles=len(sums)+1,standardVersion=manifest['version'])
   results.append(result);print(json.dumps(result),flush=True)
   target.with_suffix('.zip.sha256').write_text(f"{result['sha256']}  {target.name}\n",encoding='ascii')
 (output/'validation.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--materials',type=Path,required=True);p.add_argument('--packs',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();build(a.materials,a.packs,a.output)
