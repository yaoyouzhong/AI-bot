"""Verify one locked annual category independently of legacy two-category ordering."""
import argparse, hashlib, json, zipfile
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def verify(directory,manifest_path,archive=None):
 locked=read(manifest_path)['works'];catalog=read(directory/'catalog.json');sources=read(directory/'selection.json');origins=read(directory/'provenance.json');guides=read(directory/'guides.zh.json')['works'];files=read(directory/'files.json')['files']
 ids=[w['id'] for w in catalog];expected=[w['id'] for w in locked]
 if ids!=expected or len(ids)!=366 or len({w['workId'] for w in catalog})!=366:raise ValueError('Annual membership/order/independent identities changed')
 for rows in [sources,origins,guides]:
  if len(rows)!=366 or {w['id'] for w in rows}!=set(ids):raise ValueError('Missing or duplicate artwork metadata')
 source_by={w['id']:w for w in sources};origin_by={w['id']:w for w in origins};guide_by={w['id']:w for w in guides};locked_by={w['id']:w for w in locked}
 originals={};frames=[];long=[]
 for day,w in enumerate(catalog,1):
  k=w['id'];source=source_by[k];origin=origin_by[k];guide=guide_by[k]
  if w['category']!='calligraphy' or w.get('annualDay')!=day or source.get('annualDay')!=day:raise ValueError('Incorrect category/date mapping: '+k)
  if w['workId']!=locked_by[k]['workId'] or guide['workId']!=w['workId'] or source['workId']!=w['workId']:raise ValueError('Artwork identity mismatch: '+k)
  if not w['frames'] or len(w['frames'])>64 or len(w['frames'])!=len(w['portraitFrames']):raise ValueError('Both orientations must preserve all pages: '+k)
  if any(w[f]!=guide[f] for f in ['title','author','museum']):raise ValueError('Caption/reader metadata mismatch: '+k)
  if source['source']!=w['source'] or source['license']!=w['license'] or origin['license']!=w['license']:raise ValueError('Source/license mismatch: '+k)
  for p in [origin,*origin.get('additionalSources',[])]:
   if min(p['displaySourceSize'])<600:raise ValueError('Insufficient source detail: '+k)
   h=p['originalSha256']
   if h in originals and originals[h]!=k:raise ValueError('Shared original across independent works: '+k+' / '+originals[h])
   originals[h]=k
  original=guide['transcription'];full=original.get('completeReferenceText') or original['text']
  if original['status'] not in ['source_verified','reference_verified'] or not full:raise ValueError('Selected work has no reviewed readable text: '+k)
  h=hashlib.sha256(full.encode()).hexdigest()
  if h!=locked_by[k]['completeReferenceSha256']:raise ValueError('Frozen complete reference changed: '+k)
  if original.get('completeReferenceTextSha256') and original['completeReferenceTextSha256']!=h:raise ValueError('Dedicated complete-text hash mismatch: '+k)
  if not guide['looking'] or guide['lookingBasis']['scope']!='work_specific_authoritative_analysis':raise ValueError('Missing authoritative viewing material: '+k)
  if len(full.encode())>=8192:long.append(dict(id=k,title=w['title'],utf8Bytes=len(full.encode()),sha256=h))
  frames.extend(w['frames']+w['portraitFrames'])
 if len(frames)!=len(set(frames)) or set(frames)!=set(files):raise ValueError('Display image inventory mismatch')
 total=0
 for n in frames:
  p=directory/n
  if Path(n).name!=n or sha(p)!=files[n] or p.stat().st_size>1048576:raise ValueError('Invalid image path/hash/transport size: '+n)
  with Image.open(p) as image:
   image.load()
   if image.format!='JPEG' or image.size!=(1280,720):raise ValueError('Invalid encoded image dimensions: '+n)
  total+=p.stat().st_size
 if len(long)!=7:raise ValueError('The frozen edition must retain all seven long originals')
 zip_checks=0
 if archive:
  with zipfile.ZipFile(archive) as z:
   pack=json.loads(z.read('PACK.json'));names=z.namelist()
   if len(names)!=len(set(names)) or pack['works']!=366 or pack['frames']!=len(frames) or pack['category']!='calligraphy':raise ValueError('Archive identity/count mismatch')
   if set(pack['files'])!=set(names)-{'PACK.json'}:raise ValueError('Archive manifest inventory mismatch')
   for name,h in pack['files'].items():
    if Path(name).name!=name or hashlib.sha256(z.read(name)).hexdigest()!=h:raise ValueError('Archive member digest mismatch: '+name)
    zip_checks+=1
   if not {'LICENSE','THIRD_PARTY_NOTICES.md','README.md','guides.zh.json'}<=set(names):raise ValueError('Required license/guide documents missing')
   packaged=json.loads(z.read('guides.zh.json'))['works']
   for g in packaged:
    original=g['transcription'];full=original.get('completeReferenceText') or original['text']
    if hashlib.sha256(full.encode()).hexdigest()!=locked_by[g['id']]['completeReferenceSha256']:raise ValueError('Archive discarded a complete reference')
  if archive.with_name(archive.name+'.sha256').read_text('ascii').split()[0]!=sha(archive):raise ValueError('External ZIP digest mismatch')
 return dict(status='passed',works=366,landscapePages=len(frames)//2,portraitPages=len(frames)//2,encodedImageBytes=total,independentSourceHashes=len(originals),exactAnnualSequence=True,allCompleteReferencesRetained=True,longOriginals=long,imageChecks=len(frames),archiveMemberChecks=zip_checks,archiveSha256=sha(archive) if archive else None,hardwareVerified=False,productionDeployment=False)

if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('directory',type=Path);p.add_argument('--manifest',type=Path,default=ROOT/'docs/art-guides/curated-calligraphy-2026-10-12.json');p.add_argument('--archive',type=Path);p.add_argument('--report',type=Path,required=True);a=p.parse_args();r=verify(a.directory,a.manifest,a.archive);a.report.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n','utf-8');print(json.dumps(r,ensure_ascii=False))
