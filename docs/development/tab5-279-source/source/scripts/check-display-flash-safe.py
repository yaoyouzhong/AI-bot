"""Check the built scanout ISR chain, not only Kconfig/source annotations."""
import argparse,json,re,subprocess
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('elf',type=Path);p.add_argument('--objdump',type=Path,required=True)
p.add_argument('--output',type=Path);p.add_argument('--sdkconfig',type=Path);a=p.parse_args()
roots=['dma_trans_done_cb','lvgl_port_camera_refresh_callback','tab5_lcd_frame_tick',
       'tab5_lcd_underrun','dw_gdma_channel_enable_ctrl','dw_gdma_channel_use_link_list',
       'dw_gdma_link_list_get_item','dw_gdma_lli_set_block_markers','dw_gdma_channel_default_isr',
       'crash_record_checkpoint']
xip=False
if a.sdkconfig:
    config=a.sdkconfig.read_text(encoding='utf-8')
    xip='CONFIG_SPIRAM_XIP_FROM_PSRAM=y' in config
    if xip:
        for option in ['CONFIG_SPIRAM_FETCH_INSTRUCTIONS=y','CONFIG_SPIRAM_RODATA=y','CONFIG_SPIRAM_FLASH_LOAD_TO_PSRAM=y']:
            assert option in config,option
        assert 'CONFIG_SPI_FLASH_SHARE_SPI1_BUS=y' not in config
flash_roots=['spi1_start','spi1_end'] if xip else ['tab5_flash_guard_begin','tab5_flash_guard_end']
roots+=flash_roots
table=subprocess.check_output([str(a.objdump),'-t',str(a.elf)],text=True)
symbols={}
for line in table.splitlines():
    fields=line.split()
    if len(fields)>=6 and fields[-1] in roots:
        symbols[fields[-1]]=dict(address=fields[0],section=fields[-3])
for name in roots:
    assert name in symbols and symbols[name]['section']=='.iram0.text',(name,symbols.get(name))
    body=subprocess.check_output([str(a.objdump),'-d','--disassemble='+name,str(a.elf)],text=True)
    if xip and name in flash_roots:
        # XIP eliminates the cache-off hooks. Prove the linked flash OS uses
        # the mutex path, rather than accepting missing hooks as a pass.
        assert '<'+('_lock_acquire' if name=='spi1_start' else '_lock_release')+'>' in body,(name,body)
        for disabled in ['cache_disable','cache_enable','spi_flash_disable_interrupts_caches_and_other_cpu','vTaskSuspendAll','vTaskPreemptionDisable']:
            assert '<'+disabled+'>' not in body,(name,disabled)
    calls=[]
    for line in body.splitlines():
        if not re.search(r'\b(?:jal|jalr|j)\b',line):continue
        m=re.search(r'(?:#\s*)?([0-9a-f]{8})\s+<([^>]+)>',line)
        if not m:continue # indirect user callback is checked on registration
        address=int(m[1],16)
        if not (xip and name in flash_roots):
            assert not 0x40000000<=address<0x48000000,(name,'flash call',line)
        calls.append(m[2])
    symbols[name]['calls']=sorted(set(calls))
result={'passed':True,'flashOs':'psram_xip_mutex' if xip else 'cache_off_hooks','symbols':symbols}
if a.output:a.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('DISPLAY_FLASH_SAFE_ELF_PASS: scanout ISR and helpers in IRAM; flash OS '+result['flashOs'])
