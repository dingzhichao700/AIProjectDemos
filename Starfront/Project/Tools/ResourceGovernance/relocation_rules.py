"""Exact uiMain -> default/mainView relocation of already admitted resources. Read only."""
import hashlib,re
from pathlib import Path
REVISION='UIMAIN-RELOCATION/R3.1'
SOURCE_ART='Assets/Art/frameAnimation/uiMain/'
TARGET_ART='Assets/Art/frameAnimation/default/mainView/'
SOURCE_DATA='Assets/ResourceData/uiMain/formationEvents/spawn-footprints.json'
TARGET_DATA='Assets/ResourceData/default/mainView/formationEvents/spawn-footprints.json'
ART_NAMES={f'formationBackground/{name}.{ext}' for name in ['fog-a','fog-b','star'] for ext in ['png','json']}|{f'formationEvents/{name}.{ext}' for name in ['frigate-rear','meteor'] for ext in ['png','json']}
KEYS={'ui_formationBackground','ui_formationEvents'}
def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def safe(path):
 if not isinstance(path,str) or not path.startswith('Assets/') or '\\' in path or any(p in ['','.','..'] or ':' in p or p.endswith((' ','.')) for p in path.split('/')):raise ValueError('Unsafe relocation path')
 return path
def expected_target(source):
 safe(source)
 if source.startswith(SOURCE_ART) and source[len(SOURCE_ART):] in ART_NAMES:
  suffix=source[len(SOURCE_ART):];key='ui_formationBackground' if suffix.startswith('formationBackground/') else 'ui_formationEvents'
  return TARGET_ART+suffix,key
 if source==SOURCE_DATA:return TARGET_DATA,'ui_formationEvents'
 raise ValueError('Resource is outside exact uiMain relocation scope')
def expected_modes(manifest,base):
 modes=dict(base)
 if manifest.get('resourceRelocation') is not None:modes['default']='PackTogetherByLabel'
 return modes
def active(manifest,guid):
 scope=manifest.get('resourceRelocation') or {}
 for item in scope.get('assets',[]):
  if item.get('guid')==guid:return any(r.get('guid')==guid and r.get('state')=='target' and r.get('target',{}).get('path')==item.get('target') for r in manifest.get('records',[]))
 return False
def validate_contract(manifest):
 scope=manifest.get('resourceRelocation')
 if scope is None:return None
 if scope.get('revision')!=REVISION or not all(scope.get(k) for k in ['scopeId','batchId','approvalEvidence']):raise ValueError('Missing exact relocation identity/approval')
 if not re.fullmatch('[a-f0-9]{64}',scope.get('planSha256','')):raise ValueError('Missing exact relocation plan hash')
 assets=scope.get('assets')
 if not isinstance(assets,list) or len(assets)!=11:raise ValueError('Relocation must contain exact eleven resource files')
 rows={r['guid']:r for r in manifest['records']};ids=set();sources=set();targets=set();states=set()
 for item in assets:
  guid=item.get('guid');source=item.get('source');target=item.get('target')
  if not re.fullmatch('[a-f0-9]{32}',guid or '') or guid in ids:raise ValueError('Duplicate/invalid relocation GUID')
  ids.add(guid);safe(source);safe(target)
  if source.casefold() in sources or target.casefold() in targets:raise ValueError('Duplicate relocation source/target')
  sources.add(source.casefold());targets.add(target.casefold())
  wanted,key=expected_target(source);before={'group':'uiMain','address':source,'labels':[key]};after={'group':'default','address':target,'labels':[key]}
  if target!=wanted or item.get('beforeEntry')!=before or item.get('afterEntry')!=after:raise ValueError('Relocation path/group/label differs from approved typed mapping')
  if not all(re.fullmatch('[a-f0-9]{64}',item.get(k,'')) for k in ['sha256','metaSha256']):raise ValueError('Missing relocation bytes/importer freeze')
  row=rows.get(guid)
  if not row or row.get('state')!='target':raise ValueError('Relocation may only move already-target resources; no hold activation')
  target_row=row.get('target',{});old_target={'path':source,'group':'uiMain','bundleKey':key};new_target={'path':target,'group':'default','bundleKey':key};projection={k:target_row.get(k) for k in ['path','group','bundleKey']}
  if projection==old_target:states.add('before')
  elif projection==new_target:
   states.add('after')
   if not row.get('transitionEvidence','').startswith(scope['batchId']+'/'):raise ValueError('Relocated target requires this exact committed transition evidence')
   if not any(h.get('target')==old_target for h in row.get('targetHistory',[])):raise ValueError('Relocation lost original target provenance')
  else:raise ValueError('Manifest target differs from relocation before/after contract')
  if any(o.get('guid')==guid for o in manifest.get('packingOverrides',[])):raise ValueError('Relocation cannot silently replace packing overrides')
 if sources!={s.casefold() for s in ({SOURCE_ART+x for x in ART_NAMES}|{SOURCE_DATA})}:raise ValueError('Relocation omits or duplicates a required pair/auxiliary resource')
 if len(states)!=1:raise ValueError('Partial relocated metadata is not a valid committed state')
 declared=manifest.get('testScopes',{}).get(scope['scopeId'],{}).get('allowedGuids',[])
 if len(declared)!=11 or set(declared)!=ids:raise ValueError('Recorded testScope differs from exact relocation')
 if scope.get('inactiveGuids'):raise ValueError('Relocation cannot add activation/exclusion side scopes')
 if manifest.get('groupModes',{}).get('default')!='PackTogetherByLabel' or manifest.get('currentGroupModes',{}).get('Default Local Group')!='PackSeparately':raise ValueError('Relocation must preserve declared final/legacy packing modes')
 return scope
def preflight(project,manifest,plan,entries,plan_sha256):
 root=Path(project).resolve();scope=validate_contract(manifest)
 if scope is None:raise ValueError('No exact relocation contract')
 if plan.get('kind')!='resource-relocation' or any(plan.get(k)!=scope[k] for k in ['batchId','approvalEvidence']) or plan.get('testScope')!=scope['scopeId']:raise ValueError('Plan identity differs from exact relocation scope')
 if not plan_sha256 or plan_sha256!=scope['planSha256']:raise ValueError('Relocation plan hash changed')
 if plan.get('deletes') or plan.get('regroupings'):raise ValueError('Relocation does not authorize deletion or unrelated regrouping')
 assets={a['guid']:a for a in scope['assets']};moves=plan.get('moves',[])
 if len(moves)!=11 or len({m['guid'] for m in moves})!=11 or set(m['guid'] for m in moves)!=set(assets):raise ValueError('Move set differs from exact relocation')
 if len(plan.get('packTransitionGuids',[]))!=11 or set(plan['packTransitionGuids'])!=set(assets):raise ValueError('AA transition set differs from relocation')
 rows={r['guid']:r for r in manifest['records']};actual={}
 for entry in entries:
  if entry['guid'] in assets:actual.setdefault(entry['guid'],[]).append({k:entry[k] for k in ['group','address','labels']})
 for move in moves:
  item=assets[move['guid']]
  if any(move.get(k)!=item[k] for k in ['guid','source','target','sha256','metaSha256']):raise ValueError('Move differs from frozen relocation asset')
  row=rows[move['guid']]
  if row['target']['path']!=item['source']:raise ValueError('Relocation is not at original admitted source')
  if actual.get(move['guid'])!=[item['beforeEntry']]:raise ValueError('Original AA registration changed or duplicated')
  source=root/safe(item['source']);target=root/safe(item['target']);meta=Path(str(source)+'.meta')
  for path in [source,target]:
   if not path.resolve().is_relative_to(root/'Assets') or any(x.is_symlink() or hasattr(x,'is_junction') and x.is_junction() for x in [path]+list(path.parents) if x!=root):raise ValueError('Linked or escaping relocation path')
  if not source.is_file() or not meta.is_file() or target.exists() or Path(str(target)+'.meta').exists():raise ValueError('Relocation source missing or destination occupied')
  found=re.search(r'^guid: ([a-f0-9]{32})\s*$',meta.read_text('utf-8-sig'),re.M)
  if not found or found[1]!=move['guid'] or digest(source)!=item['sha256'] or digest(meta)!=item['metaSha256']:raise ValueError('Relocation GUID/content/importer changed')
 return {'batchId':plan['batchId'],'moveCount':11,'relocationCount':11,'planReady':True,'executed':False}
