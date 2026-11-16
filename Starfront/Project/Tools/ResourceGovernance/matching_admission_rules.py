"""Exact matching-page admission scope. No generic default ownership or writes."""
import hashlib,json,re
from pathlib import Path

REVISION='MATCHING-ADMISSION/R3.1'
PAGE_KEY='ui_playerMatching'
SOURCE_ART='Assets/Art/unpack/uiBattle/battle/playerMatching/'
SOURCE_PREFAB='Assets/Prefab/uiBattle/battle/playerMatching/'
TARGET_ART='Assets/Art/unpack/default/playerMatching/'
TARGET_PREFAB='Assets/Prefab/default/playerMatching/'
SOURCE_FONT='Assets/Art/font/material/playerMatchingProgressOutline.mat'
TARGET_FONT='Assets/Art/font/fonts/material/playerMatchingProgressOutline.mat'
FORBIDDEN_NAMES={'Profession_support.prefab','Profession_heavy.prefab','Profession_firepower.prefab'}

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def read(path):return json.loads(Path(path).read_text('utf-8-sig'))
def safe(path):
 if not isinstance(path,str) or not path.startswith('Assets/') or '\\' in path or any(p in ['','.','..'] or ':' in p or p.endswith((' ','.')) for p in path.split('/')):raise ValueError('Unsafe admission asset path')
 return path
def expected_target(source):
 safe(source)
 if Path(source).name in FORBIDDEN_NAMES:raise ValueError('Unused profession candidate cannot be activated')
 if source.startswith(SOURCE_ART) and Path(source).suffix=='.png':return TARGET_ART+source[len(SOURCE_ART):],'default',[PAGE_KEY]
 if source.startswith(SOURCE_PREFAB) and Path(source).suffix=='.prefab':return TARGET_PREFAB+source[len(SOURCE_PREFAB):],'default',[PAGE_KEY]
 if source==SOURCE_FONT:return TARGET_FONT,'fonts',[]
 raise ValueError('Resource is outside exact matching input roots/types')
def active(manifest,guid):
 scope=manifest.get('matchingAdmission') or {}
 return any(a.get('guid')==guid for a in scope.get('assets',[])) and any(r.get('guid')==guid and r.get('state')=='target' for r in manifest.get('records',[]))
def expected_modes(manifest,base):
 modes=dict(base)
 if manifest.get('matchingAdmission') is not None:modes['default']='PackTogetherByLabel'
 return modes

def validate_contract(manifest):
 scope=manifest.get('matchingAdmission')
 if scope is None:return None
 if scope.get('revision')!=REVISION or not scope.get('scopeId') or not scope.get('batchId') or not scope.get('approvalEvidence'):raise ValueError('Missing exact matching admission approval')
 if not re.fullmatch(r'[a-f0-9]{64}',scope.get('inventorySha256','')) or not re.fullmatch(r'[a-f0-9]{64}',scope.get('planSha256','')) or not Path(scope.get('inventoryPath','')).is_absolute():raise ValueError('Missing frozen inventory/plan identity')
 assets=scope.get('assets');inactive=scope.get('inactiveGuids')
 if not isinstance(assets,list) or not assets or len(assets)>58 or not isinstance(inactive,list) or len(set(inactive))!=len(inactive):raise ValueError('Invalid selected/inactive matching scope')
 rows={r['guid']:r for r in manifest['records']};ids=set();sources=set();targets=set();root=False
 for item in assets:
  guid=item.get('guid');source=item.get('source');target=item.get('target')
  if not re.fullmatch(r'[a-f0-9]{32}',guid or '') or guid in ids or guid in inactive:raise ValueError('Duplicate/forbidden admission GUID')
  ids.add(guid);safe(source);safe(target)
  if source.casefold() in sources or target.casefold() in targets:raise ValueError('Duplicate admission source/target')
  sources.add(source.casefold());targets.add(target.casefold())
  wanted,group,labels=expected_target(source)
  if target!=wanted or item.get('entry')!={'group':group,'address':target,'labels':labels}:raise ValueError('Admission target/group/labels differ from approved typed owner')
  if not all(re.fullmatch(r'[a-f0-9]{64}',item.get(k,'')) for k in ['sha256','metaSha256']):raise ValueError('Missing admission content/importer freeze')
  row=rows.get(guid)
  if not row or row['state'] not in ['hold','target'] or row['current']['path']!=source or row['current'].get('entries'):raise ValueError('Admission must start from exact inactive candidate')
  if row['target']['path']!=target or row['target'].get('group')!=group or (group=='default' and row['target'].get('bundleKey')!=PAGE_KEY):raise ValueError('Manifest target differs from exact admission')
  if any(o.get('guid')==guid for o in manifest.get('packingOverrides',[])):raise ValueError('Admission must not bypass an existing override')
  if source==SOURCE_PREFAB+'PlayerMatchingPanel.prefab':root=True
 if not root:raise ValueError('Matching main root is missing')
 for row in manifest['records']:
  if row['target'].get('group')=='default' and row['target'].get('bundleKey')==PAGE_KEY and row['state']=='target' and row['guid'] not in ids:raise ValueError('Undeclared resource entered matching Bundle')
 if set(manifest.get('testScopes',{}).get(scope['scopeId'],{}).get('allowedGuids',[]))!=ids:raise ValueError('Recorded test scope differs from exact admission')
 for guid in inactive:
  row=rows.get(guid)
  if not row or row['state']!='hold' or row['current'].get('entries') or any(o.get('guid')==guid for o in manifest.get('packingOverrides',[])):raise ValueError('Unselected candidate is not inactive')
 if manifest.get('currentGroupModes',{}).get('Default Local Group')!='PackSeparately':raise ValueError('Legacy default packing must stay unchanged')
 return scope

def preflight(project,manifest,plan,entries,plan_sha256):
 root=Path(project).resolve();scope=validate_contract(manifest)
 if scope is None:raise ValueError('Matching admission has no approved contract')
 if plan.get('kind')!='matching-admission' or plan.get('testScope')!=scope['scopeId'] or plan.get('batchId')!=scope['batchId'] or plan.get('approvalEvidence')!=scope['approvalEvidence']:raise ValueError('Plan admission identity differs from approved scope')
 if not plan_sha256 or plan_sha256!=scope['planSha256'] or plan.get('inventorySha256')!=scope['inventorySha256']:raise ValueError('Exact frozen plan/inventory hash mismatch')
 if plan.get('deletes') or plan.get('regroupings'):raise ValueError('Admission cannot delete or regroup other resources')
 frozen=Path(scope['inventoryPath'])
 if not frozen.is_file() or digest(frozen)!=scope['inventorySha256']:raise ValueError('Frozen matching inventory changed')
 inv=read(frozen);allrows=inv.get('assets',[]);frozen_by_guid={a['guid']:a for a in allrows}
 if len(frozen_by_guid)!=len(allrows):raise ValueError('Duplicate frozen inventory identity')
 selected={a['guid']:a for a in scope['assets']};inactive=set(scope['inactiveGuids'])
 if set(frozen_by_guid)!=set(selected)|inactive:raise ValueError('Inventory contains undeclared/unaccounted matching candidates')
 if len(plan.get('packTransitionGuids',[]))!=len(selected) or set(plan['packTransitionGuids'])!=set(selected):raise ValueError('Pack registration scope differs from admission')
 moves=plan.get('moves',[])
 if len(moves)!=len(selected) or len({m['guid'] for m in moves})!=len(moves):raise ValueError('Move scope differs from admission')
 rows={r['guid']:r for r in manifest['records']}
 for move in moves:
  expected=selected.get(move['guid'])
  if expected is None or any(move.get(k)!=expected[k] for k in ['guid','source','target','sha256','metaSha256']):raise ValueError('Move differs from exact matching contract')
  if rows[move['guid']]['state']!='hold':raise ValueError('Admission is not at inactive source state')
  item=frozen_by_guid[move['guid']]
  if item['path']!=move['source'] or any(item.get(k)!=move[k] for k in ['sha256','metaSha256']):raise ValueError('Selected input differs from frozen UI inventory')
  source=root/safe(move['source']);target=root/safe(move['target']);meta=Path(str(source)+'.meta')
  for path in [source,target]:
   if not path.resolve().is_relative_to(root/'Assets') or any(x.is_symlink() or hasattr(x,'is_junction') and x.is_junction() for x in [path]+list(path.parents) if x!=root):raise ValueError('Linked or escaping admission path')
  if not source.is_file() or not meta.is_file() or target.exists() or Path(str(target)+'.meta').exists():raise ValueError('Missing source or occupied matching destination')
  actual_guid=re.search(r'^guid: ([a-f0-9]{32})\s*$',meta.read_text('utf-8-sig'),re.M)
  if not actual_guid or actual_guid[1]!=move['guid'] or digest(source)!=move['sha256'] or digest(meta)!=move['metaSha256']:raise ValueError('Admission source GUID/content/importer changed')
 for entry in entries:
  if entry['guid'] in set(selected)|inactive:raise ValueError('Candidate already has runtime registration')
 return {'batchId':plan['batchId'],'moveCount':len(moves),'admissionCount':len(selected),'inactiveCount':len(inactive),'planReady':True,'executed':False}
