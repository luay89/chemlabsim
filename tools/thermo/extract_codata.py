#!/usr/bin/env python3
"""Extract the ΔfH° column of the CODATA Key Values page (saved HTML) into JSON.

Usage: python3 -I tools/thermo/extract_codata.py key1.html out.json
The stored result is tools/thermo/codata_key_values_1989.json (values only; metadata added by hand).
"""
import re,html,json,sys
t=open(sys.argv[1],encoding='latin-1').read()
t=re.sub(r'<sup>(.*?)</sup>',r'^\1',t,flags=re.S); t=re.sub(r'<sub>(.*?)</sub>',r'\1',t,flags=re.S)
out=[]
for r in re.findall(r'<tr.*?>(.*?)</tr>',t,flags=re.S|re.I):
  c=[' '.join(html.unescape(re.sub('<.*?>','',x)).split()) for x in re.findall(r'<t[dh].*?>(.*?)</t[dh]>',r,flags=re.S|re.I)]
  if len(c)!=5 or not c[0] or c[0].startswith('Home') or not c[2]: continue
  m=re.match(r'^(-?[\d.]+)(?:\s*±\s*([\d.]+))?$',c[2])
  if not m: print('SKIP',c,file=sys.stderr); continue
  out.append({'substance':c[0],'state':c[1],'dfH_kJ_per_mol':float(m.group(1)),'uncertainty_kJ_per_mol':float(m.group(2)) if m.group(2) else 0.0})
json.dump(out,open(sys.argv[2],'w'),indent=1,ensure_ascii=False)
print(len(out))
