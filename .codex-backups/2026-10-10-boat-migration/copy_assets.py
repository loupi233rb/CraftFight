from pathlib import Path
import re,shutil,json,hashlib
target=Path(r'D:/课程/大三上/虚拟现实/craft/CraftFight')
source=Path(r'D:/课程/大三上/虚拟现实/boat attack/BoatAttack')
work=Path(__file__).parent
deps=(work/'export-dependencies.txt').read_text(encoding='utf-8-sig').splitlines()
existing={}
for p in (target/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: ([0-9a-f]{32})',p.read_text(errors='replace'),re.M)
    if m: existing[m[1]]=p
plan=[]
for rel in deps:
    src=source/rel
    dest=target/(rel if rel.startswith('Assets/BoatAttackTutorial/') else 'Assets/BoatAttackTutorial/Source/'+rel[7:])
    assert src.is_file(),src
    sm=src.with_name(src.name+'.meta')
    assert sm.is_file(),sm
    guid=re.search(r'^guid: ([0-9a-f]{32})',sm.read_text(),re.M)[1]
    if guid in existing and existing[guid]!=dest.with_name(dest.name+'.meta'):
        raise RuntimeError(f'GUID collision {guid}: {existing[guid]} versus {src}')
    if dest.exists(): raise RuntimeError(f'Destination already exists: {dest}')
    plan.append((src,dest,sm))
for src,dest,sm in plan:
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(src,dest);shutil.copy2(sm,dest.with_name(dest.name+'.meta'))
shutil.copy2(source/'LICENSE.md',target/'Assets/BoatAttackTutorial/BoatAttack-LICENSE.md')
summary={'files':len(plan),'bytes':sum(a.stat().st_size for a,_,_ in plan),'assets':[str(b.relative_to(target)).replace('\\','/') for _,b,_ in plan]}
(work/'copy-report.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k!='assets'}))
