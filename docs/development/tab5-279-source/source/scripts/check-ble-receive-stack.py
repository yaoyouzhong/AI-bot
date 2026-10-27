"""Reject the real .065 receiver and .081 identity callback stack regressions."""
import argparse,json,re,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('elf',type=Path);p.add_argument('--objdump',type=Path,required=True);p.add_argument('--output',type=Path)
a=p.parse_args();result={}
for symbol,maximum in [('telemetry_info_access',512),('telemetry_input_access',320),('receive_frame',256),('telemetry_tree_bounded',160)]:
    text=subprocess.check_output([str(a.objdump),'-d','--disassemble='+symbol,str(a.elf)],text=True)
    assert '<'+symbol+'>:' in text,('missing receiver symbol',symbol)
    frames=[int(n) for n in re.findall(r'addi\s+sp,sp,-(\d+)',text)]
    assert frames and max(frames)<=maximum,(symbol,frames,maximum)
    no_print=symbol!='telemetry_info_access'
    if no_print:assert not re.search(r'<(?:cJSON_Print\w*|sprintf|snprintf|_svfprintf_r)(?:\+[^>]*)?>',text),(symbol,'JSON/printf on receive call chain')
    result[symbol]={'maximumLocalFrame':max(frames),'limit':maximum,'noDirectPrint':no_print}
for symbol in ['device_nonce','device_proof']:
    text=subprocess.check_output([str(a.objdump),'-d','--disassemble='+symbol,str(a.elf)],text=True)
    assert '<'+symbol+'>:' in text,('missing auth symbol',symbol)
    assert not re.search(r'<(?:sprintf|snprintf|_svfprintf_r)(?:\+[^>]*)?>',text),(symbol,'printf in authentication hex conversion')
    result[symbol]={'noDirectPrint':True}
result['scope']='Static local frames and direct printing only; real task stack margin still needs hardware acceptance.'
if a.output:a.output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('BLE_RECEIVE_STACK_ELF_PASS '+json.dumps(result))
