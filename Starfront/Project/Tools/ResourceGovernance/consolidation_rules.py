"""Exact, reversible consolidation of the nine approved obsolete AA groups. Read only."""
from pathlib import Path
import hashlib,re,collections
import generated_technical_rules as generated
REVISION='NINE-GROUPS/R3.1'
COUNTS={'Foundation':9,'HotUpdateCore':1,'Login':1,'Main':1,'cardDisplayAnimation':2,'common':4,'icons':83,'renderCommon':1,'uiCommon':16}
CODE={'HotUpdateCore','Login','Main'}
AOT={'UnityEngine.UI.dll','System.Core.dll','UnityEngine.CoreModule.dll','mscorlib.dll','Foundation.dll','DOTween.dll','Luban.Runtime.dll','wx-runtime.dll','Newtonsoft.Json.dll'}
ASSET_KEYS={'display_2101_burst','display_2102_display','icons_skill','icons_item','ui_power','ui_skillControls','ui_cardControls','render_DeckLockedFrame'}
KEY_COUNTS={'display_2101_burst':2,'display_2102_display':4,'icons_skill':64,'icons_item':19,'ui_power':13,'ui_skillControls':2,'ui_cardControls':1,'render_DeckLockedFrame':1}

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def safe(path):
 if not isinstance(path,str) or not path.startswith('Assets/') or '\\' in path or any(p in ['', '.', '..'] or ':' in p or p.endswith((' ','.')) for p in path.split('/')):raise ValueError('Unsafe consolidation asset path')
 return path
def members(manifest):return {a['guid']:a for a in (manifest.get('groupConsolidation') or {}).get('assets',[])}
def member(manifest,guid):return guid in members(manifest)
def active(manifest,guid):
 item=members(manifest).get(guid)
 if not item:return False
 row=next((r for r in manifest['records'] if r['guid']==guid),None)
 key=item['afterEntry']['labels'][0] if item['afterEntry']['labels'] else item['beforeRecord']['target']['bundleKey']
 return bool(row and row['state']=='target' and row['target'].get('path')==item['target'] and row['target'].get('group')==item['afterEntry']['group'] and row['target'].get('bundleKey')==key and row.get('transitionEvidence','').startswith(manifest['groupConsolidation']['batchId']+'/'))
def expected_modes(manifest,base):
 modes=dict(base)
 if manifest.get('groupConsolidation') is not None:modes.update(default='PackTogetherByLabel',hotUpdate='PackSeparately')
 return modes
def target_entry(manifest,row):
 item=members(manifest).get(row['guid'])
 return dict(item['afterEntry']) if item and active(manifest,row['guid']) else None
def approved_route(manifest,path,guid):
 item=members(manifest).get(guid)
 if not item or not active(manifest,guid) or item['target']!=path:return None
 return dict(group=item['afterEntry']['group'],bundleKey=item['afterEntry']['labels'][0] if item['afterEntry']['labels'] else item['beforeRecord']['target']['bundleKey'],runtime=True,kind='consolidated-exact-identity')

def remapped_duplicate_reviews(project,manifest,groups,records):
 """Carry an existing named review through exact active moves; never admit a new review."""
 moved={a['source']:a for a in members(manifest).values() if a['source']!=a['target'] and active(manifest,a['guid'])};out={}
 for group in groups:
  if not all(group.get(k) for k in ['id','paths','owner','reviewBatch','basis']):continue
  mapped=[];changes=[];valid=True
  for source in group['paths']:
   item=moved.get(source)
   if item:
    target=item['target'];asset=Path(project)/target;meta=Path(str(asset)+'.meta')
    if records.get(target,{}).get('guid')!=item['guid'] or not asset.is_file() or not meta.is_file() or digest(asset)!=item['sha256'] or digest(meta)!=item['metaSha256']:valid=False;break
    mapped.append(target);changes.append(dict(guid=item['guid'],source=source,target=target))
   else:mapped.append(source)
  if valid and changes and len(mapped)==len(set(mapped)):
   out[tuple(sorted(mapped))]=dict(id=group['id'],sourcePaths=list(group['paths']),mappings=changes)
 return out
def role(item):
 group=item['beforeEntry']['group']
 return 'technical' if group in CODE|{'Foundation'} else 'display-animation' if group in ['common','cardDisplayAnimation'] else 'shared-ui'

def validate_mapping(item):
 source,target=item['source'],item['target'];safe(source);safe(target);before,after=item['beforeEntry'],item['afterEntry'];g=before.get('group')
 if g not in COUNTS or set(before)!={'group','address','labels'} or set(after)!={'group','address','labels'}:raise ValueError('Invalid consolidation entry schema/group')
 if not isinstance(before['labels'],list) or not isinstance(after['labels'],list):raise ValueError('Consolidation labels must be exact lists')
 if g in CODE|{'Foundation'}:
  match=re.fullmatch(r'Assets/HotUpdateBytes/(code|aot)/([^/]+\.dll)\.bytes',source)
  if not match or source!=target:raise ValueError('Code/metadata identities or physical paths cannot change')
  kind,name=match.groups()
  if g=='Foundation':
   if kind!='aot' or name not in AOT:raise ValueError('Unexpected AOT assembly')
  elif kind!='code' or name!=g+'.dll':raise ValueError('Code group/assembly mismatch')
  address='hotupdate/'+kind+'/'+name
  if before['address']!=address or before['labels'] or after!={'group':'hotUpdate','address':address,'labels':[]}:raise ValueError('Hot-update public alias or entry isolation changed')
  return
 if before['address']!=source or after['group']!='default' or after['address']!=target or len(after['labels'])!=1 or after['labels'][0] not in ASSET_KEYS:raise ValueError('Asset target must use exact default Bundle identity')
 key=after['labels'][0]
 if g=='icons':
  match=re.fullmatch(r'Assets/Art/unpack/default/(skillIcons|itemIcons)/(\d+)\.png',source)
  if not match or target!=source or before['labels'] or key!=('icons_skill' if match[1]=='skillIcons' else 'icons_item'):raise ValueError('Icon ID/path/category changed')
 elif g=='cardDisplayAnimation':
  if not re.fullmatch(r'Assets/Art/frameAnimation/cardDisplayAnimation/2101/burst/muzzle-flash\.(png|json)',source) or target!=source.replace('/cardDisplayAnimation/','/default/cardDisplayAnimation/',1) or before['labels']!=['display_2101_burst'] or after['labels']!=before['labels']:raise ValueError('Marine burst identity changed')
 elif g=='common':
  match=re.fullmatch(r'Assets/(Art/skeletalAnimation|Prefab)/cardDisplayAnimation/2102/display/([^/]+)',source)
  names={f'Assets/Art/skeletalAnimation/cardDisplayAnimation/2102/display/{n}.png' for n in ['body','thruster-left','thruster-right']}|{'Assets/Prefab/cardDisplayAnimation/2102/display/MedivacDisplayRig.prefab'}
  if not match or source not in names or target!=source.replace('/cardDisplayAnimation/','/default/cardDisplayAnimation/',1) or before['labels']!=['display_2102_display'] or after['labels']!=before['labels']:raise ValueError('Medivac display identity changed')
 elif g=='renderCommon':
  if source!='Assets/Art/shader/DeckLockedFrame.shader' or target!='Assets/Art/shader/default/DeckLockedFrame.shader' or before['labels']!=['render_DeckLockedFrame'] or after['labels']!=before['labels']:raise ValueError('Shared shader ownership changed')
 elif g=='uiCommon':
  if before['labels']!=after['labels']:raise ValueError('Shared UI Bundle key changed')
  expected=None
  if source in {'Assets/Art/atlasSource/default/sharedPower/'+name+'.png' for name in ['power-missing','power-title']+['power-digit-'+str(i) for i in range(10)]}:expected=source.replace('/atlasSource/','/unpack/',1)
  elif source=='Assets/Art/atlasSource/default/cardCultivation/ui-power-icon.png':expected='Assets/Art/unpack/default/sharedPower/ui-power-icon.png'
  elif source in ['Assets/Art/atlasSource/default/cardSkillDetail/skillFrameSquareRim.png','Assets/Art/atlasSource/default/cardSkillDetail/skillOpeningSquareMask.png']:expected=source.replace('/atlasSource/','/unpack/',1).replace('/cardSkillDetail/','/skillControls/',1)
  elif source=='Assets/Art/material/DeckLockedFrame.mat' and target in ['Assets/Art/material/default/sharedRender/DeckLockedFrame.mat','Assets/Art/material/default/DeckLockedFrame.mat']:expected=target
  if not expected or target!=expected:raise ValueError('Shared UI source/type destination differs from approved shape')
  if '/sharedPower/' in target and key!='ui_power' or '/skillControls/' in target and key!='ui_skillControls' or target.endswith('.mat') and key!='ui_cardControls':raise ValueError('Shared UI typed label mismatch')

def validate_contract(manifest):
 scope=manifest.get('groupConsolidation')
 if scope is None:return None
 if scope.get('revision')!=REVISION or not all(scope.get(k) for k in ['scopeId','batchId','approvalEvidence']) or not re.fullmatch('[a-f0-9]{64}',scope.get('planSha256','')):raise ValueError('Missing exact consolidation approval/plan identity')
 if len(scope.get('groups',[]))!=9 or set(scope['groups'])!=set(COUNTS):raise ValueError('Consolidation must name exactly nine obsolete groups')
 assets=scope.get('assets')
 if not isinstance(assets,list) or len(assets)!=118:raise ValueError('Nine-group consolidation must account for all118 entries')
 rows={r['guid']:r for r in manifest['records']};ids=set();sources=set();targets=set();counts=collections.Counter();states=set();overrides=manifest.get('packingOverrides',[])
 for item in assets:
  guid=item.get('guid')
  if not re.fullmatch('[a-f0-9]{32}',guid or '') or guid in ids:raise ValueError('Duplicate/invalid consolidation GUID')
  ids.add(guid);validate_mapping(item)
  for path,seen in [(item['source'],sources),(item['target'],targets)]:
   if path.casefold() in seen:raise ValueError('Duplicate consolidation source/target')
   seen.add(path.casefold())
  if not all(re.fullmatch('[a-f0-9]{64}',item.get(k,'')) for k in ['sha256','metaSha256']):raise ValueError('Missing frozen content/importer hash')
  counts[item['beforeEntry']['group']]+=1;row=rows.get(guid);before=item.get('beforeRecord')
  if not row or not isinstance(before,dict) or set(before)!={'state','currentPath','target'} or before['state'] not in ['current','target','keep']:raise ValueError('Consolidation requires existing active record or explicit current technical baseline')
  actual_before={'state':row['state'],'currentPath':row['current']['path'],'target':row['target']}
  source_before=before['target']['path'] if before['state']=='target' else before['currentPath']
  if source_before!=item['source']:raise ValueError('Before record points to another source')
  before_overrides=item.get('beforeOverrides',[])
  if not isinstance(before_overrides,list) or any(o.get('guid')!=guid or o.get('path')!=item['source'] or o.get('assetSha256')!=item['sha256'] for o in before_overrides):raise ValueError('Original override provenance drift')
  active_overrides=[o for o in overrides if o.get('guid')==guid]
  if actual_before==before:
   states.add('before')
   if active_overrides!=before_overrides:raise ValueError('Current overrides differ from frozen baseline')
  elif active(manifest,guid):
   states.add('after')
   if row['current']['path']!=before['currentPath'] or not any(h.get('target')==before['target'] for h in row.get('targetHistory',[])):raise ValueError('Consolidation lost original target/history')
   if active_overrides:raise ValueError('Superseded consolidation override still active')
   for old in before_overrides:
    if not any(h.get('supersededBy')==scope['batchId'] and h.get('override')==old for h in manifest.get('packingOverrideHistory',[])):raise ValueError('Superseded override was not archived exactly')
  else:raise ValueError('Record differs from consolidation before/after state')
 if counts!=COUNTS:raise ValueError('Exact source-group entry counts differ')
 if collections.Counter(a['afterEntry']['labels'][0] for a in assets if a['afterEntry']['labels'])!=KEY_COUNTS:raise ValueError('Typed Bundle member counts differ')
 if len(states)!=1:raise ValueError('Mixed consolidation metadata cannot be committed')
 allowed=manifest.get('testScopes',{}).get(scope['scopeId'],{}).get('allowedGuids',[])
 if len(allowed)!=118 or set(allowed)!=ids:raise ValueError('Consolidation testScope differs')
 if manifest.get('groupModes',{}).get('default')!='PackTogetherByLabel' or manifest.get('groupModes',{}).get('hotUpdate')!='PackSeparately' or manifest.get('currentGroupModes',{}).get('Default Local Group')!='PackSeparately':raise ValueError('Final or legacy packing contract changed')
 closed=scope.get('closedGroups',[])
 if closed and (len(closed)!=9 or set(closed)!=set(COUNTS) or states!={'after'}):raise ValueError('Cannot retire occupied or unrelated groups')
 return scope

def preflight(project,manifest,plan,entries,plan_sha256):
 root=Path(project).resolve();scope=validate_contract(manifest)
 if scope is None or plan.get('kind')!='group-consolidation' or plan.get('testScope')!=scope['scopeId'] or any(plan.get(k)!=scope[k] for k in ['batchId','approvalEvidence']):raise ValueError('Consolidation plan identity differs')
 if plan_sha256!=scope['planSha256']:raise ValueError('Frozen consolidation plan changed')
 if plan.get('deletes') or plan.get('regroupings'):raise ValueError('Consolidation preflight does not authorize deletion')
 assets=members(manifest);selected=plan.get('packTransitionGuids',[])
 if len(selected)!=118 or set(selected)!=set(assets):raise ValueError('AA consolidation selection differs')
 actual=collections.defaultdict(list)
 for entry in entries:
  if entry['group'] in COUNTS and entry['guid'] not in assets:raise ValueError('Source obsolete group acquired an undeclared entry')
  if entry['group']=='hotUpdate' and entry['guid'] not in assets:raise ValueError('Unapproved technical hot-update entry')
  if entry['guid'] in assets:actual[entry['guid']].append({k:entry[k] for k in ['group','address','labels']})
 physical={g:a for g,a in assets.items() if a['source']!=a['target']};moves=plan.get('moves',[])
 if len(moves)!=len(physical) or len({m['guid'] for m in moves})!=len(moves) or {m['guid'] for m in moves}!=set(physical):raise ValueError('Physical move subset differs from exact consolidation')
 for move in moves:
  if any(move.get(k)!=physical[move['guid']][k] for k in ['guid','source','target','sha256','metaSha256']):raise ValueError('Consolidation move drift')
 rows={r['guid']:r for r in manifest['records']}
 for guid,item in assets.items():
  row=rows[guid];projection={'state':row['state'],'currentPath':row['current']['path'],'target':row['target']}
  if projection!=item['beforeRecord']:raise ValueError('Consolidation input is no longer at frozen source state')
  if actual[guid]!=[item['beforeEntry']]:raise ValueError('AA entry changed or duplicated')
  source=root/safe(item['source']);target=root/safe(item['target']);meta=Path(str(source)+'.meta')
  for path in [source,target]:
   if not path.resolve().is_relative_to(root/'Assets') or any(x.is_symlink() or hasattr(x,'is_junction') and x.is_junction() for x in [path]+list(path.parents) if x!=root):raise ValueError('Linked or escaped consolidation source/target')
  if not source.is_file() or not meta.is_file() or source!=target and (target.exists() or Path(str(target)+'.meta').exists()):raise ValueError('Missing source or occupied consolidation target')
  found=re.search(r'^guid: ([a-f0-9]{32})\s*$',meta.read_text('utf-8-sig'),re.M)
  if not found or found[1]!=guid or digest(source)!=item['sha256'] or digest(meta)!=item['metaSha256']:raise ValueError('Consolidation GUID/content/importer drift')
 return dict(batchId=scope['batchId'],consolidationCount=118,moveCount=len(physical),planReady=True,executed=False)

def closed_group_paths(groups):
 paths=[]
 for group in groups:
  for base in ['Assets/AddressableAssetsData/AssetGroups/'+group+'.asset']+['Assets/AddressableAssetsData/AssetGroups/Schemas/'+group+'_'+kind+'.asset' for kind in ['BundledAssetGroupSchema','ContentUpdateGroupSchema']]:
   paths.extend([base,base+'.meta'])
 return paths

def audit_closed_groups(project,manifest,records,entries):
 scope=manifest.get('groupConsolidation') or {};closed=set(scope.get('closedGroups',[]));out=[]
 outputs=generated.available_outputs(project,manifest);paths={r.get('guid'):path for path,r in records.items()}
 for path in closed_group_paths(closed):
  if (Path(project)/path).exists():out.append(dict(code='PACK_CLOSED_GROUP',path=path,guid=None,severity='error',message='Closed obsolete Group, Schema or importer was recreated'))
 for e in entries:
  if e['group'] in closed:out.append(dict(code='PACK_CLOSED_GROUP_ENTRY',path=e.get('file','Assets/AddressableAssetsData'),guid=e['guid'],severity='error',message='Runtime entry returned to closed group'))
  if scope and e['group']=='hotUpdate':
   item=members(manifest).get(e['guid']);path=paths.get(e['guid'])
   expected=outputs.get(path) or (item['afterEntry'] if item and item['afterEntry']['group']=='hotUpdate' else None)
   if not expected or any(e.get(k)!=expected[k] for k in ('group','address','labels')):out.append(dict(code='PACK_HOTUPDATE_SCOPE',path=e.get('file','Assets/AddressableAssetsData'),guid=e['guid'],severity='error',message='Technical entry is neither an exact migrated identity nor a verified current producer output'))
 return out

def audit_identities(project,manifest,records):
 out=[];outputs=generated.available_outputs(project,manifest)
 for guid,item in members(manifest).items():
  path=item['target'] if active(manifest,guid) else item['source'];asset=Path(project)/path;meta=Path(str(asset)+'.meta')
  regenerated=active(manifest,guid) and role(item)=='technical' and path in outputs
  if not asset.is_file() or not meta.is_file() or records.get(path,{}).get('guid')!=guid or (digest(asset)!=item['sha256'] and not regenerated) or digest(meta)!=item['metaSha256']:
   out.append(dict(code='PACK_CONSOLIDATION_IDENTITY',path=path,guid=guid,severity='error',message='Consolidated GUID, bytes or importer differs from frozen source'))
 return out
