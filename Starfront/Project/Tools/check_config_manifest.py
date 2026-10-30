from pathlib import Path
import re,json
r=Path(__file__).resolve().parents[1]
manifest=r/'Assets/Scripts/csharp/login/config/ConfigManifest.cs'
names=re.findall(r'"(cfgobj_[^"]+)"',manifest.read_text(encoding='utf-8-sig'))
tables=re.findall(r'loader\("([^"]+)"\)',(r/'Assets/Scripts/csharp/login/cfg/Tables.cs').read_text(encoding='utf-8-sig'))
assert len(names)==len(set(names)) and set(names)==set(tables),(names,tables)
group=(r/'Assets/AddressableAssetsData/AssetGroups/configData.asset').read_text(encoding='utf-8-sig')
for name in names:
    p=r/'Assets/ConfigBin'/f'{name}.json'
    json.loads(p.read_text(encoding='utf-8-sig'))
    guid=re.search(r'guid: (\w+)',p.with_suffix('.json.meta').read_text()).group(1)
    assert guid in group,name
foundation=r/'Assets/Scripts/csharp/foundation'
for p in foundation.rglob('*.cs'):
    t=p.read_bytes()
    assert b'ConfigManifest' not in t and b'ALL_CONFIG_LIST' not in t,p
assert json.loads((r/'Assets/Scripts/csharp/login/Login.asmdef').read_text())['name']=='Login'
print(json.dumps({'status':'passed','tables':len(names),'generatedNamesMatch':True,'addressableEntriesMatch':True,'foundationHasNoManifestDependency':True}))
