"""Explicit, recoverable 16 MiB TAB5 factory -> two-slot migration.

No command runs on import. Backups contain pairing/Wi-Fi secrets: keep under
.tools, never attach or publish them. Never erase the whole chip as a shortcut.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys

FLASH_SIZE = 0x1000000
OLD = {'nvs': (1,2,0x9000,0x6000), 'phy_init': (1,1,0xf000,0x1000),
       'factory': (0,0,0x10000,0x800000), 'assets': (1,0x40,0xb00000,0x200000)}
NEW = {'nvs': OLD['nvs'], 'phy_init': OLD['phy_init'],
       'otadata': (1,0,0x10000,0x2000), 'ota_0': (0,0x10,0x20000,0x6e0000),
       'ota_1': (0,0x11,0x700000,0x6e0000), 'assets': (1,0x40,0xe00000,0x200000)}

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def partitions(data):
    result = {}
    for at in range(0,0xc00,32):
        magic,kind,subtype,offset,size,name,flags=struct.unpack('<HBBII16sI',data[at:at+32])
        if magic in (0xffff,0xebeb): break
        if magic!=0x50aa or flags: raise ValueError('Unexpected partition format or flags')
        name=name.split(b'\0')[0].decode('ascii')
        if name in result: raise ValueError('Duplicate partition')
        result[name]=(kind,subtype,offset,size)
    return result

def prepare(backup, build, bundle):
    backup,build,bundle=Path(backup).resolve(),Path(build).resolve(),Path(bundle).resolve()
    raw=backup.read_bytes()
    if len(raw)!=FLASH_SIZE or partitions(raw[0x8000:0x9000])!=OLD:
        raise ValueError('Backup is not the verified legacy TAB5 layout')
    table=build/'partition_table/partition-table.bin'
    if partitions(table.read_bytes().ljust(0x1000,b'\xff'))!=NEW:
        raise ValueError('Candidate partition table differs from reviewed layout')
    app=build/'aibot_tab5.bin'; image=app.read_bytes()
    boot=build/'bootloader/bootloader.bin'
    if len(image)<1024 or len(image)>NEW['ota_0'][3] or image[0]!=0xe9 or image[80:112].split(b'\0')[0]!=b'aibot_tab5':
        raise ValueError('Candidate application size/identity invalid')
    if not 0<len(boot.read_bytes())<=0x6000: raise ValueError('Bootloader does not fit before table')
    bundle.mkdir(parents=True,exist_ok=False)
    pieces={'old-table.bin':raw[0x8000:0x9000], 'nvs-phy.bin':raw[0x9000:0x10000],
            'assets.bin':raw[0xb00000:0xd00000], 'otadata-empty.bin':b'\xff'*0x2000}
    for name,data in pieces.items(): (bundle/name).write_bytes(data)
    for src,name in ((table,'partition-table.bin'),(app,'application.bin'),(boot,'bootloader.bin')):
        shutil.copyfile(src,bundle/name)
    manifest={'format':1,'backup':str(backup),'backupSha256':sha(backup),
              'files':{p.name:sha(p) for p in bundle.iterdir()},'flashBytes':FLASH_SIZE,
              'appBytes':len(image),'oldAssetsOffset':0xb00000,'newAssetsOffset':0xe00000}
    (bundle/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    return manifest

def verified_bundle(bundle):
    bundle=Path(bundle).resolve(); manifest=json.loads((bundle/'manifest.json').read_text(encoding='utf-8'))
    if manifest.get('format')!=1 or manifest.get('flashBytes')!=FLASH_SIZE: raise ValueError('Unknown manifest')
    expected={'old-table.bin','nvs-phy.bin','assets.bin','otadata-empty.bin','partition-table.bin','application.bin','bootloader.bin'}
    if set(manifest['files'])!=expected: raise ValueError('Unexpected bundle files')
    for name,digest in manifest['files'].items():
        if sha(bundle/name)!=digest: raise ValueError('Bundle hash changed: '+name)
    backup=Path(manifest['backup']).resolve()
    if backup.stat().st_size!=FLASH_SIZE or sha(backup)!=manifest['backupSha256']: raise ValueError('Recovery backup hash changed')
    if partitions((bundle/'partition-table.bin').read_bytes().ljust(0x1000,b'\xff'))!=NEW: raise ValueError('Wrong target layout')
    return bundle,manifest

def invoke(port,*args):
    # An explicit esp32p4 target refuses another chip. no_reset keeps ROM mode
    # until every write/verification finishes; user short-resets only afterwards.
    subprocess.run([sys.executable,'-m','esptool','--chip','esp32p4','--port',port,
                    '--baud','460800','--before','no_reset','--after','no_reset',*map(str,args)],check=True)

def install(bundle,port,run=invoke):
    bundle,manifest=verified_bundle(bundle)
    def file(name): return str(bundle/name)
    # Verify the live device still matches the snapshot BEFORE the first write.
    run(port,'verify_flash','0x8000',file('old-table.bin'),'0x9000',file('nvs-phy.bin'),'0xb00000',file('assets.bin'))
    run(port,'write_flash','0xe00000',file('assets.bin'))
    run(port,'verify_flash','0xe00000',file('assets.bin'))
    run(port,'write_flash','0x20000',file('application.bin'))
    run(port,'verify_flash','0x20000',file('application.bin'))
    # Clear inactive slot headers/old resources; the copied resource bank has
    # already been checked. No old app is valid as a candidate OTA fallback.
    run(port,'erase_region','0x700000','0x6e0000')
    run(port,'write_flash','0x2000',file('bootloader.bin'),'0x8000',file('partition-table.bin'),
        '0x10000',file('otadata-empty.bin'))
    run(port,'verify_flash','0x2000',file('bootloader.bin'),'0x8000',file('partition-table.bin'),
        '0x10000',file('otadata-empty.bin'),'0x9000',file('nvs-phy.bin'),'0xe00000',file('assets.bin'))
    (bundle/'installed.json').write_text(json.dumps({'verified':True,'applicationSha256':manifest['files']['application.bin']}),encoding='utf-8')
    print('OTA_MIGRATION_VERIFIED: short-reset device; hardware acceptance still required')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    sub=parser.add_subparsers(dest='command',required=True)
    p=sub.add_parser('backup');p.add_argument('--port',required=True);p.add_argument('--output',required=True)
    p=sub.add_parser('prepare');p.add_argument('--backup',required=True);p.add_argument('--build',required=True);p.add_argument('--bundle',required=True)
    p=sub.add_parser('install');p.add_argument('--port',required=True);p.add_argument('--bundle',required=True);p.add_argument('--confirm-layout-migration',action='store_true',required=True)
    p=sub.add_parser('restore');p.add_argument('--port',required=True);p.add_argument('--bundle',required=True);p.add_argument('--confirm-restore-backup',action='store_true',required=True)
    args=parser.parse_args()
    if args.command=='backup':
        out=Path(args.output).resolve()
        if out.exists(): raise ValueError('Refusing to overwrite an existing backup')
        out.parent.mkdir(parents=True,exist_ok=True)
        invoke(args.port,'read_flash','0',hex(FLASH_SIZE),out)
        if out.stat().st_size!=FLASH_SIZE or partitions(out.read_bytes()[0x8000:0x9000])!=OLD:
            raise ValueError('Unexpected device layout; preserve backup and stop')
        invoke(args.port,'verify_flash','0',out)
        print('BACKUP_VERIFIED sha256='+sha(out))
    elif args.command=='prepare':
        result=prepare(args.backup,args.build,args.bundle);print('BUNDLE_READY appBytes='+str(result['appBytes']))
    elif args.command=='install': install(args.bundle,args.port)
    else:
        _,manifest=verified_bundle(args.bundle)
        invoke(args.port,'write_flash','0',manifest['backup'])
        invoke(args.port,'verify_flash','0',manifest['backup'])
        print('BACKUP_RESTORED_VERIFIED')

if __name__=='__main__': main()
