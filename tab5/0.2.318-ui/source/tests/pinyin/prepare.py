from pathlib import Path
import sys
root,baseline,out=map(Path,sys.argv[1:]);marker='typedef struct {\n    lv_obj_t *keyboard'
for variant,source in [('baseline',baseline/'firmware/main'),('current',root/'firmware/main')]:
 s=(source/'tab5_ime.c').read_text(encoding='utf8');assert marker in s;s=s.split(marker)[0]
 if variant=='baseline':
  s=s.replace('"tab5_pinyin_dict.inc"','"baseline_dict.inc"').replace('tab5_pinyin_lookup','baseline_pinyin_lookup')
 else:
  s+='\nsize_t current_count(void){return DICT_COUNT;}\nbool current_match(size_t i,const char *key,const char *word,uint32_t weight,uint32_t boundaries){pinyin_entry e=dictionary_entry(i);return !strcmp(e.py,key)&&!strcmp(e.text,word)&&e.weight==weight&&e.boundaries==boundaries;}\n'
 (out/(variant+'.c')).write_text(s,encoding='utf8')
(out/'baseline_dict.inc').write_bytes((baseline/'firmware/main/tab5_pinyin_dict.inc').read_bytes())
