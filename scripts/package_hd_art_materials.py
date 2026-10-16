"""Assemble byte-preserving, portable local HD material collections (no deployment)."""
import argparse,csv,hashlib,json,re,shutil,zipfile,sys,copy
from pathlib import Path
from PIL import Image
Image.MAX_IMAGE_PIXELS=500_000_000

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,value):p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def portable(value):
 if isinstance(value,dict):return all(portable(v) for v in value.values())
 if isinstance(value,list):return all(portable(v) for v in value)
 return not isinstance(value,str) or not re.search(r'(?i)(?:(?<![a-z0-9])[a-z]:[\\/]|\\\\|artifacts[\\/]development|C:/Users)',value)
def safe(title):return re.sub(r'[<>:"/\\|?*\x00-\x1f]','_',title).strip(' .')[:55] or '未命名'
def build(index,packs,output,version,overrides_path=None):
 if output.exists():raise ValueError('Use a new collection output directory')
 hd=read(index);overrides=read(overrides_path) if overrides_path else {};root=Path(__file__).resolve().parents[1];output.mkdir(parents=True);report=[]
 for category,label in [('painting','名画'),('calligraphy','书法')]:
  name=f'AI-bot-HD-{category.title()}-{version}';dest=output/name;dest.mkdir()
  folder=packs/category;catalog=read(folder/'catalog.json');selections={w['id']:w for w in read(folder/'selection.json')};origins={w['id']:w for w in read(folder/'provenance.json')}
  guides={w['id']:w for w in read(folder/'guides.zh.json')['works']}
  rows=[];works=[];sources={};total=0
  for ordinal,work in enumerate(catalog,1):
   selection=selections[work['id']];origin=origins[work['id']];guide=guides.get(work['id'],{})
   title=guide.get('title') or work['title'];author=guide.get('author') or work['author']
   directory=dest/'作品'/f'{ordinal:03d}-{safe(title)}';directory.mkdir(parents=True)
   items=[]
   for n,(record,selected) in enumerate(zip([origin]+origin.get('additionalSources',[]),[selection]+selection.get('additionalSources',[]),strict=True),1):
    digest=record['originalSha256'];original_source=hd['sources'][digest];source=overrides.get(digest,original_source);path=Path(source['path']);target=directory/f'{n:03d}{path.suffix.lower()}'
    shutil.copyfile(path,target)
    if sha(target)!=source['sha256']:raise ValueError('Copied original differs: '+work['id'])
    with Image.open(target) as im:
     if list(im.size)!=[source['width'],source['height']]:raise ValueError('Original dimensions differ')
    relative=target.relative_to(dest).as_posix();size=target.stat().st_size;total+=size
    item=dict(file=relative,originalFilename=path.name,width=source['width'],height=source['height'],bytes=size,sha256=source['sha256'],imageSource=record.get('imageSource') or selected.get('imageSource') or work['source'],license=record.get('license') or work['license'],pdfPage=record.get('pdfPage'),reviewedCrop=record.get('crop'),detail=selected.get('detail',''))
    if digest in overrides:item['derivativeOf']=dict(originalSha256=digest,originalWidth=original_source['width'],originalHeight=original_source['height'],processing=source['processing'])
    items.append(item);sources[digest]={**source,'path':relative}
    rows.append({'序号':ordinal,'作品名称':title,'作者':author,'馆藏机构':guide.get('museum') or selection.get('museum',''),'素材页':n,'宽度像素':source['width'],'高度像素':source['height'],'文件字节数':size,'素材处理':source.get('processing','原始字节不变'),'许可标记':item['license'],'作品来源':work['source'],'图片来源':item['imageSource'],'PDF页码':item['pdfPage'],'相对路径':relative,'SHA256':source['sha256']})
   meta=dict(order=ordinal,id=work['id'],workId=work.get('workId') or work['source'],title=title,author=author,period=guide.get('period',''),museum=guide.get('museum') or selection.get('museum',''),source=work['source'],license=work['license'],files=items)
   write(directory/'作品信息.json',meta);works.append(meta)
  assert len(works)==366
  write(dest/'catalog.json',dict(schema=1,category=category,edition=version,works=works))
  pages=copy.deepcopy([p for p in hd['pages'] if p['category']==category])
  for page in pages:
   changed={part['source'] for part in page['parts'] if part['source'] in overrides}
   if changed:
    if len(changed)!=1 or any(part['source'] not in changed for part in page['parts']):raise ValueError('Mixed-source derivative needs explicit remapping')
    key=next(iter(changed));sx=overrides[key]['width']/hd['sources'][key]['width'];sy=overrides[key]['height']/hd['sources'][key]['height']
    page['width']=round(page['width']*sx);page['height']=round(page['height']*sy)
    for part in page['parts']:
     for field in ('crop','dest'):
      x,y,w,h=part[field];l,t,r,b=round(x*sx),round(y*sy),round((x+w)*sx),round((y+h)*sy);part[field]=[l,t,r-l,b-t]
  write(dest/'desktop-index.relative.json',dict(schema=1,sources=sources,pages=pages))
  with (dest/'作品与素材目录.csv').open('w',encoding='utf-8-sig',newline='') as f:
   writer=csv.DictWriter(f,fieldnames=rows[0].keys());writer.writeheader();writer.writerows(rows)
  shutil.copyfile(root/'LICENSE',dest/'LICENSE')
  (dest/'THIRD_PARTY_NOTICES.md').write_text('''# 高清素材权利与来源 / HD material attribution

项目 LICENSE 的 MIT 许可仅适用于 AI-bot 自有代码和整理工具，不替代图片的独立许可。
The project MIT license covers project code and tooling, not the artwork reproductions.

每件作品的作者、馆藏机构、作品页面、图片来源和现有许可标记完整保存在 catalog.json、作品信息.json 和 CSV 目录中。除逐文件 derivativeOf 标记的分发处理版本外，素材按原始字节收录；处理版本的原始哈希、尺寸和处理参数另行保留。界面中使用的裁切仅作为 reviewedCrop 记录，素材文件保留裁切前原图。部分图片为已取得 PDF 内的原始页图；图片来源链接及 pdfPage 保留，集合不另行声称包含整个 PDF。

CC BY 4.0 条目保留作者、机构、来源及许可标记；再次使用时随作品保留这些信息。许可链接：https://creativecommons.org/licenses/by/4.0/
CC0：https://creativecommons.org/publicdomain/zero/1.0/
Public domain 是现有来源记录中的公版标记，不是新增的版权授权。来源记录未在本次整理中重新作法律判定。

Credits, museum names, source URLs and recorded license labels are included per work. Source bytes are unchanged except explicitly recorded distribution derivatives. PDF-derived original images retain their source links and page numbers. No new blanket image license is granted by this collection.
''',encoding='utf-8')
  image_policy='巨幅 TIFF 改为长边 60000 像素、质量 95、无色度降采样的 JPEG，包内不含这张原 TIFF；处理记录见 CSV 和 derivativeOf，原文件在制作机保留。其余素材保持原始字节。' if any(f.get('derivativeOf') for w in works for f in w['files']) else '全部图片保持原分辨率、原格式和原始字节，没有缩小或重新编码。'
  (dest/'README.md').write_text(f'''# {label}高清分发素材集 / HD {category} materials

版本：{version}。共 {len(works)} 件作品、{len(rows)} 张素材，文件合计 {total:,} 字节（{total/2**30:.2f} GiB）。

- 作品目录按当前年度图库顺序排列；一个文件夹对应一件作品，001、002 等为原素材顺序，不把册页拆成多件作品。
- {image_policy}
- 先打开“作品与素材目录.csv”按作者、尺寸或机构筛选；每件作品另有“作品信息.json”。
- catalog.json 保存完整作品及来源；SHA256SUMS.txt 覆盖所有交付文件（校验清单自身除外）。
- desktop-index.relative.json 保存电脑端页序、分组及裁切映射，文件路径全部相对于本目录。它用于后续高清素材导入开发，当前桥接不能直接把这个相对路径索引作为本机 desktop-originals.json 使用。
- 本集是独立素材交付，不是现有 TAB5 展示图库包，不能通过当前图库导入入口直接安装；不包含桥接程序或固件。当前本机高清浏览配置不改变。
- 来源和许可见 THIRD_PARTY_NOTICES.md 以及逐件目录。目录顺序和许可标记沿用当前已入库版本。

This portable collection contains original image bytes plus explicitly documented distribution derivatives, per-work attribution, dimensions and SHA-256 checksums. It is not an installable TAB5 display pack. The relative desktop index is for future import support; no program or device is modified.
''',encoding='utf-8')
  for path in dest.rglob('*.json'):
   if not portable(read(path)):raise ValueError('Local path leak: '+str(path))
  files={p.relative_to(dest).as_posix():sha(p) for p in sorted(dest.rglob('*')) if p.is_file()}
  (dest/'SHA256SUMS.txt').write_text(''.join(f'{h}  {name}\n' for name,h in files.items()),encoding='utf-8')
  archive=output/(name+'.zip');print(f'ARCHIVE_START {category} {len(rows)} images',flush=True)
  with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1,allowZip64=True) as z:
   for path in sorted(dest.rglob('*')):
    if path.is_file():z.write(path,name+'/'+path.relative_to(dest).as_posix())
  verified=0
  with zipfile.ZipFile(archive) as z:
   if len(z.namelist())!=len(files)+1:raise ValueError('Archive inventory mismatch')
   for file,expected in files.items():
    with z.open(name+'/'+file) as stream:actual=hashlib.file_digest(stream,'sha256').hexdigest()
    if actual!=expected:raise ValueError('Archive content hash mismatch: '+file)
    verified+=1
   if z.read(name+'/SHA256SUMS.txt')!=(dest/'SHA256SUMS.txt').read_bytes():raise ValueError('Checksum manifest mismatch')
  digest=sha(archive);archive.with_suffix('.zip.sha256').write_text(f'{digest}  {archive.name}\n',encoding='ascii')
  result=dict(category=category,works=len(works),images=len(rows),sourceBytes=total,archive=archive.name,archiveBytes=archive.stat().st_size,sha256=digest,verifiedArchiveFiles=verified+1,portableMetadata=True,sourceBytesUnchanged=not any(w['files'][i].get('derivativeOf') for w in works for i in range(len(w['files']))),processedImages=sum(bool(f.get('derivativeOf')) for w in works for f in w['files']))
  report.append(result);print(json.dumps(result),flush=True)
 write(output/'validation.json',report)
 (output/'README.md').write_text('# 高清素材集\n\n名画和书法分别提供 ZIP 与展开目录；完整来源、尺寸和逐文件校验在各自目录。ZIP 全部成员已逐项读取并核对 SHA-256。未修改现有图库、桥接配置或设备，未公开发布。\n',encoding='utf-8')
if __name__=='__main__':
 sys.stdout.reconfigure(encoding='utf-8');p=argparse.ArgumentParser();p.add_argument('--index',type=Path,required=True);p.add_argument('--packs',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--version',required=True);p.add_argument('--distribution-overrides',type=Path);a=p.parse_args();build(a.index,a.packs,a.output,a.version,a.distribution_overrides)
