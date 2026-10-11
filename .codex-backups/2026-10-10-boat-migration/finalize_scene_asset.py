from pathlib import Path
import re,math
root=Path(r'D:/课程/大三上/虚拟现实/craft/CraftFight')
scene=root/'Assets/BoatAttackTutorial/Scenes/Tutorial_BoatAttack.unity'
text=scene.read_text()
pattern=r'^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)'
docs=list(re.finditer(pattern,text,re.M|re.S))
def obj(name):
    d=next(d for d in docs if d[1]=='1' and re.search(r'^  m_Name: '+re.escape(name)+r'$',d[3],re.M))
    return d[2],re.search(r'component: \{fileID: (\d+)\}',d[3])[1]
build_go,build_tr=obj('Buildings');env_go,env_tr=obj('BoatAttack Beach Environment');camera_go,camera_tr=obj('Preview Camera')
village=root/'Assets/BoatAttackTutorial/Source/Objects/Levels/Island/Environment Objects Prefabs/Buildings/DistantVillage.prefab.meta'
guid=re.search(r'^guid: (\w+)',village.read_text(),re.M)[1]
if not re.search(r'm_SourcePrefab:.*guid: '+guid,text):
    source=Path(r'D:/课程/大三上/虚拟现实/boat attack/BoatAttack/Assets/Objects/Levels/Island/Environment Objects Prefabs/Buildings.prefab').read_text()
    sd=list(re.finditer(pattern,source,re.M|re.S))
    instance=next(d for d in sd if d[1]=='1001' and re.search(r'm_SourcePrefab:.*guid: '+guid,d[3]))
    transform=next(d for d in sd if d[1]=='4' and re.search(r'm_CorrespondingSourceObject:.*guid: '+guid,d[3]))
    assert instance[2] not in {d[2] for d in docs} and transform[2] not in {d[2] for d in docs}
    parent=next(d for d in docs if d[1]=='4' and d[2]==build_tr)
    replacement=parent[0].replace('  m_Children:\n','  m_Children:\n  - {fileID: '+transform[2]+'}\n')
    text=text.replace(parent[0],replacement)
    text+='\n'+re.sub(r'm_TransformParent: \{fileID: \d+\}','m_TransformParent: {fileID: '+build_tr+'}',instance[0])+transform[0]
helper_guid=re.search(r'^guid: (\w+)',(root/'Assets/BoatAttackTutorial/Scripts/TutorialVegetationSetup.cs.meta').read_text(),re.M)[1]
if helper_guid not in text:
    helper_id='9223372036000000101'
    env=next(d for d in docs if d[1]=='1' and d[2]==env_go)
    text=text.replace(env[0],env[0].replace('  m_Component:\n','  m_Component:\n  - component: {fileID: '+helper_id+'}\n'))
    text+='\n--- !u!114 &'+helper_id+'\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: '+env_go+'}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+helper_guid+', type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n'
camera=next(d for d in docs if d[1]=='4' and d[2]==camera_tr)
yaw=math.atan2(10,-135);pitch=math.atan2(89,math.hypot(10,135))
q=(math.cos(yaw/2)*math.sin(pitch/2),math.sin(yaw/2)*math.cos(pitch/2),-math.sin(yaw/2)*math.sin(pitch/2),math.cos(yaw/2)*math.cos(pitch/2))
c=re.sub(r'  m_LocalPosition: .*','  m_LocalPosition: {x: 105, y: 94, z: 28}',camera[0])
c=re.sub(r'  m_LocalRotation: .*','  m_LocalRotation: {x: %.9f, y: %.9f, z: %.9f, w: %.9f}'%q,c)
text=text.replace(camera[0],c)
for d in docs:
    if d[1]=='20' and 'm_GameObject: {fileID: '+camera_go+'}' in d[3]:text=text.replace(d[0],d[0].replace('m_ClearFlags: 1','m_ClearFlags: 2'))
text=text.replace('m_AmbientMode: 1','m_AmbientMode: 3')
scene.write_text(text,encoding='utf-8',newline='\n')
print('Scene asset includes the east villages, lighting helper and final overview camera.')
