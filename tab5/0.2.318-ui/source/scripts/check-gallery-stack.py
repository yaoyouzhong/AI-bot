"""Bound project-owned frames in the 8 KiB gallery callback chain.
This does not model SDK/interrupt frames; retain the hardware guard and verify
low-water measurements under a real transfer as well.
"""
from pathlib import Path
import sys
root=Path(sys.argv[1]);files=list(root.rglob('gallery_remote.c.su'))+list(root.rglob('voice_remote.c.su'))+list(root.rglob('gallery_store.c.su'))
frames={}
for path in files:
 for line in path.read_text(encoding='utf8').splitlines():
  fields=line.split('\t')
  if len(fields)>=3:frames[fields[0].rsplit(':',1)[-1].split('.')[0]]=int(fields[1])
limits={'foreground_busy':64,'voice_remote_busy':64,'download_cancelled':64,'card_writer_progress':64,'worker':1280,'store_put_impl':640,'gallery_store_get_impl':640,'gallery_store_refresh_impl':640,'reference_meta_impl':640,'catalog_pair_save_impl':640,'catalog_migrate_impl':640,'direct_resume_load_impl':640,'gallery_resume_append_impl':640,'gallery_resume_clear_impl':640}
for name,limit in limits.items():
 # GCC may inline the private body into its resource-owning wrapper. Check
 # that actual merged frame against the same bound, never skip the budget.
 actual=name if name in frames else name.removesuffix('_impl')
 if actual not in frames:raise SystemExit('Missing stack record: '+name)
 frames[name]=frames[actual]
 if frames[name]>limit:raise SystemExit(f'GALLERY_STACK_REJECT {name}={frames[name]} > {limit}')
print('GALLERY_STACK_BUDGET_OK '+' '.join(f'{name}={frames[name]}' for name in limits))
