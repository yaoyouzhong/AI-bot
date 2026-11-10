import json, pathlib, subprocess
base=pathlib.Path(r'C:\Users\yaououzhong\Work\AI-bot\artifacts\tab5-birthday-health-302-20261103\source\firmware\build')
stage=pathlib.Path(__file__).resolve().parents[1]/'link-size-check';stage.mkdir(exist_ok=True)
obj=pathlib.Path(__file__).resolve().parents[1]/'firmware/build/esp-idf/main/CMakeFiles/__idf_main.dir/gallery_store.c.obj'
ar=next(pathlib.Path(__import__('os').environ['IDF_TOOLS_PATH'],'tools').rglob('riscv32-esp-elf-ar.exe'))
archive=stage/'libmain.a';__import__('shutil').copyfile(base/'esp-idf/main/libmain.a',archive)
subprocess.run([str(ar),'r',str(archive),str(obj)],check=True)
s=(base/'build.ninja').read_text(encoding='utf8');i=s.index('build aibot_tab5.elf:');j=s.index('\n\n',i);edge=s[i:j];lines=edge.splitlines();variables={line.strip().split(' = ',1)[0]:line.strip().split(' = ',1)[1] for line in lines if ' = ' in line}
libs=variables['LINK_LIBRARIES'];old='esp-idf/main/libmain.a';assert old in libs;libs=libs.replace(old,archive.as_posix());rsp=stage/'link.rsp';rsp.write_text('CMakeFiles/aibot_tab5.elf.dir/project_elf_src_esp32p4.c.obj '+variables['LINK_PATH']+' '+libs,encoding='utf8')
ninja=next(pathlib.Path(__import__('os').environ['IDF_TOOLS_PATH'],'tools/ninja').rglob('ninja.exe'))
r=subprocess.run([str(ninja),'-C',str(base),'-t','commands','aibot_tab5.elf'],capture_output=True,text=True,encoding='utf8',errors='replace',check=True)
cmd=r.stdout.splitlines()[-1]; oldmap=(base/'aibot_tab5.map').as_posix();assert oldmap in cmd and '@CMakeFiles\\aibot_tab5.elf.rsp' in cmd and '-o aibot_tab5.elf' in cmd
cmd=cmd.replace(oldmap,(stage/'candidate.map').as_posix()).replace('@CMakeFiles\\aibot_tab5.elf.rsp','@'+rsp.as_posix()).replace('-o aibot_tab5.elf','-o '+(stage/'candidate.elf').as_posix())
print('LINKING_ISOLATED_CAPACITY_CHECK',flush=True);subprocess.run(cmd,cwd=base,check=True)
size=next(pathlib.Path(__import__('os').environ['IDF_TOOLS_PATH'],'tools').rglob('riscv32-esp-elf-size.exe'));subprocess.run([str(size),str(base/'aibot_tab5.elf'),str(stage/'candidate.elf')],check=True)
