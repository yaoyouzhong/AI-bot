"""Create a local desktop-only original-image index; never modify installed packs."""
import argparse,json,hashlib,sys,math
from pathlib import Path
from PIL import Image,ImageOps
from prepare_annual_calligraphy_layouts import views as native_views
Image.MAX_IMAGE_PIXELS=500_000_000

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
class View:
 def __init__(self,w,h,parts):self.width=w;self.height=h;self.parts=parts
 def crop(self,b):
  l,t,r,bottom=b;parts=[]
  for p in self.parts:
   x,y,w,h=p['dest'];a=max(l,x);c=max(t,y);d=min(r,x+w);e=min(bottom,y+h)
   if d<=a or e<=c:continue
   sx,sy,sw,sh=p['crop'];q=dict(p);q['crop']=[sx+((x+w-d) if p['rotation']==180 else (a-x))*sw/w,sy+((y+h-e) if p['rotation']==180 else (c-y))*sh/h,(d-a)*sw/w,(e-c)*sh/h];q['dest']=[a-l,c-t,d-a,e-c];parts.append(q)
  return View(r-l,bottom-t,parts)
 def resize(self,size):
  w,h=size;parts=[]
  for p in self.parts:
   q=dict(p);x,y,pw,ph=p['dest'];q['dest']=[x*w/self.width,y*h/self.height,pw*w/self.width,ph*h/self.height];parts.append(q)
  return View(w,h,parts)
def split(art,enabled,detail=None):
 result=[art]
 if detail:art=art.crop(detail)
 if enabled and art.width>art.height*3:
  width=round(art.height*1104/558);step=round(width*.9);starts=list(range(max(0,art.width-width),-1,-step))
  if starts[-1]!=0:starts.append(0)
  result += [art.crop((x,0,min(x+width,art.width),art.height)) for x in starts]
 elif enabled and art.height>art.width*2.2:
  height=round(art.width*1.6);step=round(height*.9);starts=list(range(0,max(1,art.height-height),step))
  if starts[-1]!=art.height-height:starts.append(art.height-height)
  result += [art.crop((0,y,art.width,y+height)) for y in starts]
 return result

def build(packs,root,out):
 pages=[];sources={};counts={}
 for category in ('painting','calligraphy'):
  folder=packs/category;cat=read(folder/'catalog.json');sel={x['id']:x for x in read(folder/'selection.json')};orig={x['id']:x for x in read(folder/'provenance.json')}
  for work in cat:
   row=sel[work['id']];origin=orig[work['id']];panels=[];records=[origin]+origin.get('additionalSources',[]);selections=[row]+row.get('additionalSources',[])
   for record,s in zip(records,selections,strict=True):
    name=record.get('file') or s['file'];path=next(p for p in (root/name,root/'artifacts/development/tab5-daily-art'/name) if p.exists()).resolve()
    digest=record['originalSha256'];key=digest
    if key not in sources:
     if sha(path)!=digest:raise ValueError('Original hash mismatch '+str(path))
     with Image.open(path) as im:
      if im.getexif().get(274,1)!=1:raise ValueError('Unmapped EXIF orientation '+str(path))
      if list(im.size)!=record['originalSize']:raise ValueError('Original dimensions changed')
     sources[key]=dict(path=str(path),sha256=digest,width=record['originalSize'][0],height=record['originalSize'][1])
    w,h=record['originalSize'];l,t,r,b=s.get('crop') or record.get('crop') or [0,0,w,h]
    rotation=s.get('rotation',row.get('rotation',0))
    if rotation not in (0,180):raise ValueError('Unsupported rotation')
    panels.append(View(r-l,b-t,[dict(source=key,crop=[l,t,r-l,b-t],dest=[0,0,r-l,b-t],rotation=rotation)]))
   native=origin.get('sourceLayout',origin.get('displayLayout'))=='reviewed-native-page-with-overlapping-detail'
   rendered=[]
   if native:
    for panel,record in zip(panels,records):
     part=panel.parts[0];source=sources[part['source']]
     with Image.open(source['path']) as im:
      l,t,w,h=part['crop'];art=im.crop((l,t,l+w,t+h)).convert('RGB')
     for _,_,bounds in native_views(art,'',work['id']):rendered.append(panel.crop(bounds) if bounds else panel)
   elif row.get('layout')=='paired-panels':
    group_size=3 if len(panels)==3 else 2
    for start in range(0,len(panels),group_size):
     group=panels[start:start+group_size];height=min(p.height for p in group);group=[p.resize((round(p.width*height/p.height),height)) for p in group];gap=round(height*.025);parts=[];x=0
     for p in reversed(group):
      for part in p.parts:q=dict(part);q['dest']=[part['dest'][0]+x,*part['dest'][1:]];parts.append(q)
      x+=p.width+gap
     rendered+=split(View(x-gap,height,parts),True)
   else:
    for i,panel in enumerate(panels):rendered+=split(panel,category=='calligraphy' or row.get('detailViews',False),row.get('detailCrop') if i==0 else None)
   correction=row.get('displayFrameCorrections')
   if correction:rendered=[rendered[i] for i in correction['keptOriginalIndices']]
   if len(rendered)!=len(work['frames']):raise ValueError(f"Page mapping differs: {work['id']} {len(rendered)} != {len(work['frames'])}")
   for frame,view in zip(work['frames'],rendered):
    pages.append(dict(category=category,id=work['id'],workId=work.get('workId') or work['source'],frame=frame,previewSha256=sha(folder/frame),width=view.width,height=view.height,parts=view.parts))
  counts[category]=len(cat)
 out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(dict(schema=1,sources=sources,pages=pages),ensure_ascii=False,separators=(',',':')),encoding='utf-8')
 print(json.dumps(dict(works=counts,pages=len(pages),sources=len(sources),index=str(out))))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--packs',type=Path,required=True);p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();build(a.packs,a.root,a.output)
