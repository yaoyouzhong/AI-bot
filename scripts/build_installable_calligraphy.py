"""Stage the locked annual calligraphy selection without modifying the source library.

Existing native display pages are retained unless a reviewed caption correction
is supplied. New originals are rendered independently. The manifest, rather
than a legacy frame-count sort, fixes the annual order.
"""
import argparse, copy, hashlib, json, shutil
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
def read(p):return json.loads(p.read_text('utf-8-sig'))
def write(p,j):p.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n','utf-8')
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def by_id(items):
 result={w['id']:w for w in items}
 if len(result)!=len(items):raise ValueError('Duplicate identity')
 return result
def stage(manifest_path,materials_path,assets,output,new_gallery=None,corrections=None,existing_only=False):
 manifest=read(manifest_path);locked=manifest['works'];materials=by_id(read(materials_path)['works'])
 if len(locked)!=366 or len({w['id'] for w in locked})!=366 or len({w['workId'] for w in locked})!=366:
  raise ValueError('The reviewed annual selection requires 366 independent works')
 if [w['annualDay'] for w in locked]!=list(range(1,367)):raise ValueError('Locked annual sequence changed')
 asset_catalog=by_id(read(assets/'catalog.json'));asset_sources=by_id(read(assets/'selection.json'));asset_origins=by_id(read(assets/'provenance.json'));asset_files=read(assets/'files.json')['files']
 for name,expected in manifest['sourceMetadataSha256'].items():
  if sha(assets/name)!=expected:raise ValueError('Full-library baseline changed: '+name)
 sets=[]
 for folder in [corrections,new_gallery]:
  if folder:
   sets.append((folder,by_id(read(folder/'catalog.json')),by_id(read(folder/'selection.json')),by_id(read(folder/'provenance.json')),read(folder/'files.json')['files']))
 output.mkdir(parents=True,exist_ok=True)
 catalog=[];selection=[];origins=[];guides=[];files={};copied=0;corrected_pages=[]
 for row in locked:
  k=row['id'];g=copy.deepcopy(materials[k]);override=next((item for item in sets if k in item[1]),None)
  if override:folder,c,s,p,hashes=override;work=copy.deepcopy(c[k]);source=copy.deepcopy(s[k]);origin=copy.deepcopy(p[k])
  elif k in asset_catalog:folder=assets;work=copy.deepcopy(asset_catalog[k]);source=copy.deepcopy(asset_sources[k]);origin=copy.deepcopy(asset_origins[k]);hashes=asset_files
  elif existing_only:continue
  else:raise ValueError('Rendered new original missing: '+k)
  if work['workId']!=row['workId'] or g['workId']!=row['workId']:raise ValueError('Stable object identity changed: '+k)
  if work['category']!='calligraphy' or len(work['frames'])!=len(work.get('portraitFrames',[])):raise ValueError('Paired layouts missing: '+k)
  if not(1<=len(work['frames'])<=64):raise ValueError('Frame count outside importer bounds: '+k)
  # Labels may have been corrected during research; membership and day order
  # stay fixed. Image caption corrections are separately audited.
  work.update(title=g['title'],author=g['author'],museum=g['museum'],annualDay=row['annualDay'])
  source.update(title=g['title'],author=g['author'],museum=g['museum'],annualDay=row['annualDay'],workId=row['workId'])
  for name in work['frames']+work['portraitFrames']:
   if Path(name).name!=name or name in files:raise ValueError('Unsafe/repeated display image: '+name)
   src=folder/name;expected=hashes[name]
   if sha(src)!=expected:raise ValueError('Rendered page differs from frozen source: '+name)
   dest=output/name
   if dest.exists():
    if sha(dest)!=expected:
     # These are this invocation's staged copies, not installed/user images.
     # Only a separately reviewed correction may replace the frozen old JPEG.
     if override is None or name not in asset_files or sha(dest)!=asset_files[name]:raise ValueError('Output already contains a different page: '+name)
     if ROOT not in dest.resolve().parents:raise ValueError('Correction target outside workspace')
     shutil.copy2(src,dest);corrected_pages.append(name)
   else:shutil.copy2(src,dest);copied+=1
   files[name]=expected
  g.setdefault('brief',g['title']+' · '+g['author'])
  if not g.get('lookingBasis'):
   source_id=g.get('lookingSupport',{}).get('sourceId') or g['sources'][0]['id']
   if source_id not in {s['id'] for s in g['sources']}:raise ValueError('Missing source for authoritative note: '+k)
   g['lookingBasis']=dict(scope='work_specific_authoritative_analysis',sourceIds=[source_id],checkedOn='2026-10-12')
  g['contentKinds']=dict(g.get('contentKinds',{}),overview='source_based_context',looking='work_specific_authoritative_analysis',transcription=g['transcription'].get('scope',''))
  t=g['transcription'];full=t.get('completeReferenceText') or t['text']
  expected=row['completeReferenceSha256']
  if hashlib.sha256(full.encode()).hexdigest()!=expected:raise ValueError('Locked complete text changed: '+k)
  t['completeReferenceText']=full
  t['completeReferenceTextSha256']=expected
  if len(full.encode())>=8192:
   # The research archive retains its old excerpt instructions. Only this
   # installable edition's reader description changes to complete paging.
   t['priorReaderScope']=t.get('readerScope');t['readerScope']='complete_reference_paged_reader_v3'
   t['priorExplanation']=t['explanation']
   replacements={
    'colbase-tnm-TB-187':('阅读页连续节录序文前4段，全文另存','本包保留完整参考正文，支持分段阅读'),
    'colbase-tnm-TB-1460':('阅读页连续前段，全文另存','本包保留完整参考正文，支持分段阅读'),
    'colbase-tnm-TB-250':('阅读页连续节录，全文另存','本包完整参考正文分段阅读'),
    'colbase-tnm-TB-366':('仍需艺术筛选','已按作品价值纳入年度精选'),
    'colbase-tnm-TB-1219':('《离骚》显示开头、结尾节录，《九歌》十一篇完整，按墨迹篇序排列；《离骚》完整参考全文另存','《离骚》完整参考及《九歌》十一篇全文随包保存，按登记篇序分段阅读'),
    'expansion-npm-shupu':('设备现有正文容量不足，设备接入另行处理','完整长文由支持guideVersion=3的客户端分段阅读'),
    'expansion-npm-ni-poems':('完整馆方参考13829字节另附全文文件和手机/电脑HTML，未截断；年度本地详情可完整阅读，设备正文尚未接入','完整馆方参考13829字节随包保存；Windows全文及guideVersion=3客户端分段阅读，不截断'),
   }
   old,new=replacements[k]
   if old not in t['explanation']:raise ValueError('Long-reader source description changed: '+k)
   t['explanation']=t['explanation'].replace(old,new)
  catalog.append(work);selection.append(source);origins.append(origin);guides.append(g)
 write(output/'catalog.json',catalog);write(output/'selection.json',selection);write(output/'provenance.json',origins);write(output/'files.json',dict(count=len(files),files=files))
 write(output/'guides.zh.json',dict(schemaVersion=1,language='zh-CN',edition='annual-calligraphy-2026-10-12',works=guides))
 summary=dict(status='existing_pages_staged' if existing_only else 'all_pages_staged',works=len(catalog),frames=len(files),landscapePages=sum(len(w['frames']) for w in catalog),portraitPages=sum(len(w['portraitFrames']) for w in catalog),newImagesCopied=copied,reviewedCorrectedPages=corrected_pages,annualManifestSha256=sha(manifest_path),materialsSha256=sha(materials_path),sourceLibraryUnchanged=True,deviceDeployment=False)
 write(output.parent/'stage-report.json',summary);print(json.dumps(summary))

if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',type=Path,default=ROOT/'docs/art-guides/curated-calligraphy-2026-10-12.json');p.add_argument('--materials',type=Path,default=ROOT/'artifacts/development/annual-art-materials-20261012-final/annual-calligraphy-materials.zh.json');p.add_argument('--assets',type=Path,default=ROOT/'windows-app/AIBotBridge/Assets/DailyArt');p.add_argument('--output',type=Path,required=True);p.add_argument('--new-gallery',type=Path);p.add_argument('--corrections',type=Path);p.add_argument('--existing-only',action='store_true');a=p.parse_args();stage(a.manifest,a.materials,a.assets,a.output,a.new_gallery,a.corrections,a.existing_only)
