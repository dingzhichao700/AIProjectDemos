"""RESOURCE-PACK/R1 identity, registration and migration-state rules. Read only."""
import json,re,collections,hashlib
import domain_rules as dr
import matching_admission_rules as ma
import relocation_rules as rr
import retirement_rules as retirement
import consolidation_rules as gc
import source_cleanup_rules as sc
from pathlib import Path

MANIFEST='ProjectSettings/ResourcePackManifest.json'
NEW_GROUPS={'common','cardBattle','map','battleCommon','cardDisplay','cardDisplayAnimation','icons','uiCommon','uiMain','uiTeam','uiCultivation','uiSkillDetail','uiItem','uiBattle','uiDebug','renderCommon','audioCommon'}
OLD_GROUPS={'opening','title','default','scene','cutScene'}
MODES={g:'PackTogetherByLabel' for g in NEW_GROUPS|{'scene','cutScene'}}
MODES.update({g:'PackSeparately' for g in ['icons','configData','fonts','Foundation','HotUpdateCore','Login','Main']})
MODES.update(opening='PackTogether',title='PackTogether',default='PackTogether')
MANAGED=('Assets/Art/','Assets/Prefab/','Assets/ResourceData/')
def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def safe(value):
 if not isinstance(value,str) or not value.startswith('Assets/') or '\\' in value or any(p in ['','.','..'] or ':' in p or p.endswith((' ','.')) for p in value.split('/')):raise ValueError('Unsafe pack path: '+str(value))
 return value
def load_manifest(project,policy):
 rule=policy.get('packGovernance')
 if rule is None:return None
 if not isinstance(rule,dict) or rule.get('schemaVersion')!=1 or rule.get('manifest')!=MANIFEST:raise ValueError('Invalid packGovernance schema/manifest')
 data=read(Path(project)/MANIFEST)
 if data.get('schemaVersion')!=1 or data.get('approvalId')!='RESOURCE-PACK/R1' or not isinstance(data.get('records'),list):raise ValueError('Invalid pack manifest identity/schema')
 modes=gc.expected_modes(data,rr.expected_modes(data,ma.expected_modes(data,MODES)))
 if data.get('groupModes')!=modes:raise ValueError('Pack manifest group modes differ from approved contract')
 if not isinstance(data.get('groupModules'),dict) or any(not isinstance(v,list) or len(v)!=len(set(v)) for v in data['groupModules'].values()):raise ValueError('Invalid group module ownership')
 if rule.get('supplementRevision')=='RESOURCE-PACK-NORM/R1':
  if data.get('supplementRevision')!=rule['supplementRevision'] or not isinstance(data.get('stageContract'),dict) or not isinstance(data.get('testScopes'),dict):raise ValueError('Missing approved stage/minimum-test contracts')
 layout=data.get('atlasBusinessLayout')
 if layout is not None:
  if layout.get('revision')!='ART-BUSINESS-LAYOUT/R1' or not layout.get('approval') or not isinstance(layout.get('businesses'),dict):raise ValueError('Invalid atlas business layout approval')
  for identity,owner in layout['businesses'].items():
   parts=identity.split('/')
   if len(parts)!=2 or parts[0] not in OLD_GROUPS or not re.fullmatch('[A-Za-z][A-Za-z0-9_]*',parts[1]) or set(owner)!={'group','bundleKey'} or owner['group'] not in MODES or not re.fullmatch('[A-Za-z0-9_.-]+',owner['bundleKey']):raise ValueError('Invalid explicit atlas business owner')
 dr.validate_policy(data.get('resourceDomainPolicy'))
 ids=set();paths=set();targets=set()
 for r in data['records']:
  if not isinstance(r,dict) or not re.fullmatch('[a-f0-9]{32}',r.get('guid','')) or r['guid'] in ids:raise ValueError('Duplicate/invalid pack GUID')
  ids.add(r['guid'])
  if r.get('state') not in ['current','target','hold','retired','keep'] or not r.get('batch') or not r.get('basis'):raise ValueError('Pack record needs state/batch/basis')
  source=safe(r['current']['path']);target=safe(r['target']['path'])
  if source.casefold() in paths:raise ValueError('Duplicate pack source path')
  paths.add(source.casefold())
  if target.casefold() in targets:raise ValueError('Duplicate pack target path')
  targets.add(target.casefold())
  for e in r['current'].get('entries',[]):
   if set(e)!= {'group','address','labels'} or not isinstance(e['labels'],list) or len(set(e['labels']))!=len(e['labels']):raise ValueError('Invalid current pack entry')
  t=r['target'];g=t.get('group');key=t.get('bundleKey')
  if g is not None and (g not in modes or not isinstance(key,str) or not key or not re.fullmatch('[A-Za-z0-9_.-]+',key)):raise ValueError('Unknown target group or invalid bundleKey')
  if r['state']=='target' and not r.get('transitionEvidence'):raise ValueError('Target state requires committed transition evidence')
  if r['state']=='target' and r.get('implementationGate') and not (isinstance(r.get('gateEvidence'),str) and r['gateEvidence'].strip()):raise ValueError('Target state has unresolved implementation gate')
  contract=r.get('importContract',{})
  if not isinstance(contract,dict) or set(contract)-{'textureType','sRGBTexture','filterMode','isReadable'}:raise ValueError('Unsupported importer contract')
 # Overrides apply only to exact existing identities and preserve original current/target records.
 rows={r['guid']:r for r in data['records']};seen_overrides=set()
 for o in data.get('packingOverrides',[]):
  g=o.get('guid');row=rows.get(g)
  if not row or g in seen_overrides or not o.get('batchId') or not o.get('evidence') or o.get('purpose') not in ['shared-dependency','card-isolation','retained-legacy','domain-consolidation']:raise ValueError('Invalid exact packing override identity/approval')
  seen_overrides.add(g);path=row['target']['path'] if row['state']=='target' else row['current']['path'];original=[target_entry(row,data)] if row['state']=='target' and row['target'].get('group') else [] if row['state']=='target' else row['current'].get('entries',[])
  if o.get('path')!=path or o.get('beforeEntries')!=original or not re.fullmatch('[a-f0-9]{64}',o.get('assetSha256','')):raise ValueError('Packing override path/history/content contract drift')
  entry=o.get('entry',{});group=entry.get('group');labels=entry.get('labels')
  if set(entry)!={'group','address','labels'} or entry['address']!=path or group not in MODES or not isinstance(labels,list) or len(set(labels))!=len(labels):raise ValueError('Invalid packing override registration')
  if o['purpose'] in ['shared-dependency','card-isolation']:
   if group!=row['target']['group'] or labels!=labels_for(group,row['target']['bundleKey']):raise ValueError('Packing override must follow approved typed target owner/key')
   if o['purpose']=='shared-dependency' and (row['state']!='current' or original or group not in ['uiCommon','renderCommon','battleCommon']):raise ValueError('Shared override only admits exact previously implicit legacy dependencies')
   if o['purpose']=='card-isolation' and (group!='cardBattle' or not original):raise ValueError('Card isolation requires existing registered card resource')
  elif o['purpose']=='domain-consolidation':dr.validate_override(row,o,data.get('resourceDomainPolicy'))
  elif group not in {row['target'].get('group')}|{e['group'] for e in original} or labels!=['retained_'+row['target']['bundleKey']] or not original:raise ValueError('Retained legacy override must preserve its exact owner and address')
 dr.validate_nonconsumer_roots(data)
 ma.validate_contract(data)
 rr.validate_contract(data)
 gc.validate_contract(data)
 retirement.validate_metadata(data,policy)
 sc.validate_metadata(data)
 return data

def packing_overrides(manifest):return {o['guid']:o for o in (manifest or {}).get('packingOverrides',[])}
def shared_source_override(path,guid,manifest):
 o=packing_overrides(manifest).get(guid);return bool(o and o['path']==path and (o['purpose']=='shared-dependency' or o['purpose']=='domain-consolidation' and o.get('previousOverride',{}).get('purpose')=='shared-dependency'))

def domains(project):
 def table(name):
  f=Path(project)/'Assets/ConfigBin'/('cfgobj_'+name+'obj.json')
  return read(f) if f.exists() else []
 layout=read(Path(project)/MANIFEST).get('atlasBusinessLayout',{}).get('businesses') if (Path(project)/MANIFEST).exists() else None;cards=table('card');bindings=read(Path(project)/MANIFEST).get('cardSuites',{}).get('buildingAssets',[]) if (Path(project)/MANIFEST).exists() else [];return {'domainPolicy':read(Path(project)/MANIFEST).get('resourceDomainPolicy') if (Path(project)/MANIFEST).exists() else None,'atlasBusinesses':layout,'buildingAssets':bindings,'buildings':{r['id'] for r in table('building')},'cards':{r['id'] for r in cards},'art':{r['id'] for r in table('cardart')},'maps':{r['id'] for r in table('battlescene')},'units':{r['content']['unitId'] for r in cards if r.get('content',{}).get('$type')=='UnitCardContent'}}

def route(path,domain=None):
 """Only explicit approved roots. No generic default/scene fallback."""
 safe(path);p=path.split('/');ext=Path(path).suffix.lower();domain=domain or {}
 def result(group,key,runtime=True,kind='resource'):
  group,key=dr.owner(group,key,path,domain.get('domainPolicy'))
  return {'group':group if runtime else None,'bundleKey':key if runtime else None,'runtime':runtime,'kind':kind}
 for module,prefix in [('skillIcons','skill'),('itemIcons','item')]:
  m=re.fullmatch(r'Assets/Art/unpack/default/'+module+r'/(\d+)\.png',path)
  if m:return result('icons',prefix+'_'+m[1],kind='icon')
 m=re.fullmatch(r'Assets/Art/unpack/cardDisplay/(cardFaces|portraits)/(\d+)\.png',path)
 if m:
  if domain.get('art') is not None and int(m[2]) not in domain['art']:raise ValueError('Unknown artId')
  return result('cardDisplay',('face_' if m[1]=='cardFaces' else 'portrait_')+m[2],kind='display')
 if path.startswith('Assets/Art/') and len(p)>=5:
  typ,g,rest=p[2],p[3],p[4:]
 elif path.startswith(('Assets/Prefab/','Assets/ResourceData/')) and len(p)>=5:typ,g,rest=p[1],p[2],p[3:]
 else:return None
 if typ in ['atlasSource','atlas'] and domain.get('atlasBusinesses') is not None and g not in OLD_GROUPS:
  raise ValueError('New atlas production paths must use base/business-atlas, not Addressables Group directories')
 if typ in ['atlasSource','atlas'] and g in OLD_GROUPS and domain.get('atlasBusinesses') is not None:
  if typ=='atlasSource':
   if len(rest)!=2 or ext!='.png':raise ValueError('Atlas source needs base/business-atlas/*.png without child directories')
   module=rest[0]
  else:
   if len(rest)!=1 or ext!='.png':raise ValueError('Atlas product needs base/business-atlas.png')
   module=Path(rest[0]).stem
  owner=domain['atlasBusinesses'].get(g+'/'+module)
  if not owner:raise ValueError('Atlas business module has no approved explicit owner')
  return result(owner['group'],owner['bundleKey'],typ=='atlas','source' if typ=='atlasSource' else 'atlas')
 if dr.enabled(domain.get('domainPolicy')) and g=='scene':
  allowed={'unpack':{'.png','.jpg','.jpeg','.tga','.webp','.psd'},'frameAnimation':{'.png','.json'},'material':{'.mat'},'Prefab':{'.prefab'},'ResourceData':{'.json','.bytes'}}
  if typ not in allowed or ext not in allowed[typ]:raise ValueError('Scene domain needs a supported resource type')
  if rest[0]=='common':
   if len(rest)<3:raise ValueError('Shared scene assets need explicit purpose and file')
   if rest[1]=='projectile':
    if len(rest)<4 or not rest[2].isdigit():raise ValueError('Shared projectile needs its actual identity')
    return result('scene','scene_common_projectile_'+rest[2],kind='scene-shared')
   if rest[1] not in domain.get('sceneModules',[]):raise ValueError('Unknown shared scene purpose')
   return result('scene','scene_common_'+rest[1],kind='scene-shared')
  if not rest[0].isdigit() or len(rest)<2 or int(rest[0]) not in domain.get('maps',set()):raise ValueError('Scene identity must be an existing mapId')
  return result('scene','scene_'+rest[0],kind='scene-map')
 if g in ['default','scene','cutScene']:return None
 if g not in NEW_GROUPS|{'opening','title'}:return None
 if typ=='atlasSource':
  if len(rest)!=2 or ext!='.png':raise ValueError('Atlas source must be a flat PNG module')
  if domain.get('modules') is not None and rest[0] not in domain['modules'].get(g,[]):raise ValueError('Unknown source module')
  return result(g,'ui_'+rest[0],False,'source')
 if typ=='atlas':
  if len(rest)!=1 or ext!='.png':raise ValueError('UI atlas must be one PNG per module')
  if domain.get('modules') is not None and Path(rest[0]).stem not in domain['modules'].get(g,[]):raise ValueError('Unknown atlas module')
  return result(g,'ui_'+Path(rest[0]).stem,kind='atlas')
 if typ=='unpack' and (len(rest)<2 or ext not in ['.png','.jpg','.jpeg','.tga','.webp','.psd']):raise ValueError('Independent-image type/layout mismatch')
 if typ=='frameAnimation' and (len(rest)<2 or ext not in ['.png','.json']):raise ValueError('Frame type/layout mismatch')
 if typ=='Prefab' and ext!='.prefab':raise ValueError('Prefab type mismatch')
 if typ=='ResourceData' and ext not in ['.json','.bytes']:raise ValueError('Data type mismatch')
 if typ=='material' and ext!='.mat':raise ValueError('Material type mismatch')
 if typ=='skeletalAnimation' and ext not in ['.png','.asset','.anim','.controller','.overrideController','.psb','.json']:raise ValueError('Skeletal type mismatch')
 if typ not in ['unpack','frameAnimation','Prefab','ResourceData','material','skeletalAnimation','audio']:return None
 if g=='cardBattle':
  if typ=='unpack' and len(rest)==2 and rest[0]=='buildings':
   binding=[b for b in domain.get('buildingAssets',[]) if b.get('path')==path]
   if len(binding)!=1:raise ValueError('Flat building needs exact explicit owner binding')
   b=binding[0];owners=b.get('ownerCardIds',[])
   if len(owners)!=1 or owners[0] not in domain.get('cards',set()) or b.get('buildingId') not in domain.get('buildings',set()) or not re.fullmatch(str(b['buildingId'])+r'-[a-z][a-z0-9-]*\.png',rest[1]):raise ValueError('Flat building identity/owner invalid or shared; use common ownership')
   return result(g,'card_'+str(owners[0]),kind='card-building')
  if len(rest)<2 or not rest[0].isdigit() or typ not in ['unpack','frameAnimation','Prefab']:raise ValueError('Card battle requires cardId and supported type')
  if domain.get('cards') is not None and int(rest[0]) not in domain['cards']:raise ValueError('Unknown cardId')
  return result(g,'card_'+rest[0],kind='cardBattle')
 if g=='map':
  if len(rest)<2 or not rest[0].isdigit() or typ not in ['unpack','frameAnimation','material','Prefab','ResourceData']:raise ValueError('Map requires mapId and supported type')
  if domain.get('maps') is not None and int(rest[0]) not in domain['maps']:raise ValueError('Unknown mapId (a unit ID is not a map)')
  return result(g,'map_'+rest[0],kind='map')
 if g=='cardDisplayAnimation':
  if rest[0]=='textures' and typ=='skeletalAnimation':
   m=re.fullmatch(r'(\d+)-([A-Za-z][A-Za-z0-9_-]*)\.png',rest[-1])
   if len(rest)!=2 or not m:raise ValueError('Flat skin texture needs artId-action.png')
   identity,action=m.groups()
  else:
   if len(rest)<3 or not rest[0].isdigit():raise ValueError('Display animation requires artId/action')
   identity,action=rest[:2]
  if domain.get('art') is not None and int(identity) not in domain['art']:raise ValueError('Unknown display artId')
  return result(g,'display_'+identity+'_'+action,kind='display-animation')
 if g in ['cardDisplay','icons']:raise ValueError('Identity assets must use the exact flat ID routes')
 if len(rest)<2:raise ValueError('Missing module')
 if domain.get('modules') is not None and rest[0] not in domain['modules'].get(g,[]):raise ValueError('Unknown module ownership')
 if g=='battleCommon':
  if rest[0]=='projectile' and len(rest)>=3 and rest[1].isdigit():return result(g,'projectile_'+rest[1],kind='shared-projectile')
  if rest[0]=='hud':return result(g,'battle_hud',kind='shared-hud')
  raise ValueError('Shared building/appearance ownership requires an explicit manifest record')
 return result(g,('ui_' if g.startswith('ui') else g+'_')+rest[0],kind=typ)

def entry_signature(e):return (e['group'].strip(),e['address'],tuple(sorted(e.get('labels',[]))))
def labels_for(group,key):return [key] if MODES[group]=='PackTogetherByLabel' or (group=='default' and key in ({ma.PAGE_KEY}|rr.KEYS)) else []
def dependency_violation(source,target):
 decision=dr.violation(source,target) if source.get('domainApplied',True) else None
 if decision is not None:return decision
 sg,tg=source.get('group'),target.get('group')
 if not tg or not sg:return False
 if sg in ['opening','title']:
  return tg not in [sg,'uiCommon','renderCommon','audioCommon','fonts','configData']
 if sg=='uiCommon':return tg not in ['uiCommon','renderCommon','audioCommon','fonts','configData','icons']
 if sg in ['cardBattle','map'] and tg in ['cardBattle','map']:
  return sg!=tg or source.get('bundleKey')!=target.get('bundleKey')
 if sg in ['cardBattle','map'] and tg in ['Default Local Group','default','scene']:return True
 if sg.startswith('ui') and sg!='uiDebug' and tg.startswith('ui') and tg not in [sg,'uiCommon']:return True
 return False
def target_entry(row,manifest=None):
 if manifest:
  exact=gc.target_entry(manifest,row)
  if exact is not None:return exact
 t=row['target'];return {'group':t['group'],'address':t['path'],'labels':labels_for(t['group'],t['bundleKey'])} if t.get('group') else None
def permitted_stale(path,e,manifest):
 if not manifest:return False
 return any(r['state']=='target' and r['target']['path']==path and entry_signature(e) in [entry_signature(x) for x in r['current'].get('entries',[])] for r in manifest['records']) or any(o['path']==path and entry_signature(e) in [entry_signature(x) for x in o['beforeEntries']] for o in manifest.get('packingOverrides',[]))

def routing_report(project,policy,records):
 data=load_manifest(project,policy)
 if data is None:return None
 byguid={r['guid']:r for r in data['records']};domain=domains(project);domain['modules']=data['groupModules'];domain['domainPolicy']=data.get('resourceDomainPolicy');domain['sceneModules']=data.get('resourceDomainPolicy',{}).get('sceneModules',[]);domain['atlasBusinesses']=data.get('atlasBusinessLayout',{}).get('businesses');items=[]
 for path,asset in records.items():
  if not path.startswith(MANAGED) and not gc.member(data,asset['guid']):continue
  row=byguid.get(asset['guid'])
  if row:
   desired=[target_entry(row,data)] if row['state']=='target' and row['target'].get('group') else [] if row['state']=='target' else row['current'].get('entries',[])
   override=packing_overrides(data).get(row['guid'])
   if override:desired=[override['entry']]
   items.append(dict(path=path,guid=asset['guid'],state='packing-override' if override else row['state'],migrationState=row['state'],desiredEntries=desired))
  else:
   try:r=route(path,domain)
   except ValueError:r=None
   if r and r.get('group') in (data.get('groupConsolidation') or {}).get('closedGroups',[]):raise ValueError('New asset routes to a closed obsolete group: '+path)
   if r:items.append(dict(path=path,guid=asset['guid'],state='new',desiredEntries=[dict(group=r['group'],address=path,labels=labels_for(r['group'],r['bundleKey']))] if r['runtime'] else []))
 return dict(schemaVersion=1,approvalId=data['approvalId'],groupModes=data['groupModes'],items=items)

def audit_packs(project,policy,records,entries,allow_pending=False):
 manifest=load_manifest(project,policy)
 if manifest is None:return []
 domain=domains(project);domain['modules']=manifest['groupModules'];domain['domainPolicy']=manifest.get('resourceDomainPolicy');domain['sceneModules']=manifest.get('resourceDomainPolicy',{}).get('sceneModules',[]);domain['atlasBusinesses']=manifest.get('atlasBusinessLayout',{}).get('businesses');out=[];byguid={r['guid']:r for r in manifest['records']};bypath={r['current']['path']:r for r in manifest['records']};bytarget={r['target']['path']:r for r in manifest['records']};seen=set();es=collections.defaultdict(list)
 for e in entries:es[e['guid']].append(e)
 def add(code,path,message):out.append(dict(code=code,path=path,guid=records.get(path,{}).get('guid'),severity='error',message=message))
 for path,asset in records.items():
  if not path.startswith(MANAGED) and not gc.member(manifest,asset['guid']):continue
  row=byguid.get(asset['guid']);expected=None
  if row:
   seen.add(row['guid']);want=row['target']['path'] if row['state']=='target' else row['current']['path']
   if path!=want:add('PACK_STATE',path,'Asset path differs from explicit '+row['state']+' state: '+want);continue
   expected=[target_entry(row,manifest)] if row['state']=='target' and row['target'].get('group') else [] if row['state']=='target' else row['current'].get('entries',[])
  elif path in bypath or path in bytarget:add('PACK_GUID',path,'Recorded identity replaced by another GUID');continue
  else:
   if path.startswith(tuple(policy.get('protectedVendorRoots',[]))):continue
   try:rule=route(path,domain)
   except ValueError as e:add('PACK_IDENTITY',path,str(e));continue
   if rule is None:add('PACK_UNCLASSIFIED',path,'New managed asset has no approved ownership route; no default/scene fallback');continue
   if rule.get('group') in (manifest.get('groupConsolidation') or {}).get('closedGroups',[]):add('PACK_CLOSED_GROUP_ROUTE',path,'New asset cannot recreate a retired Group; explicit current ownership required');continue
   expected=[{'group':rule['group'],'address':path,'labels':labels_for(rule['group'],rule['bundleKey'])}] if rule['runtime'] else []
  override=packing_overrides(manifest).get(asset['guid'])
  if override:
   expected=[override['entry']]
   if hashlib.sha256((Path(project)/path).read_bytes()).hexdigest()!=override['assetSha256']:add('PACK_OVERRIDE_CONTENT',path,'Exact retained-path dependency changed content')
  actual=es.get(asset['guid'],[])
  if row and row.get('importContract'):
   meta=Path(str(Path(project)/path)+'.meta').read_text(encoding='utf-8-sig')
   for field,import_expected in row['importContract'].items():
    found=re.search(r'^\s*'+re.escape(field)+r':\s*(-?\d+)\s*$',meta,re.M)
    if not found or int(found[1])!=import_expected:add('PACK_IMPORT_CONTRACT',path,'Importer '+field+' differs from declared data/display contract')
  if len(actual)>1:add('PACK_DUPLICATE_ENTRY',path,'Same GUID has multiple runtime entries')
  if allow_pending and not actual and expected:continue
  if allow_pending and row and actual and permitted_stale(path,actual[0],manifest):continue
  if sorted(entry_signature(e) for e in actual)!=sorted(entry_signature(e) for e in expected):add('PACK_REGISTRATION',path,'Group/address/full label set differs from approved state')
 for row in manifest['records']:
  if row['guid'] not in seen:add('PACK_MISSING',row['current']['path'],'Manifest asset missing; no deletion authorized by pack approval')
 stage=manifest.get('stageContract')
 if stage:
  mode=stage.get('mode')
  if mode not in ['legacy-current-default','legacy-main-root-address','current-root-address'] or (mode!='current-root-address' and stage.get('scopeGroup')!='Default Local Group') or (mode=='current-root-address' and stage.get('scopeGroup') not in set(MODES)|{'Default Local Group'}):raise ValueError('Unimplemented stage transition; freeze producer/consumer/root contract first')
  root=byguid.get(stage.get('mainRootGuid'));root_entries=es.get(stage.get('mainRootGuid'),[])
  valid_root=bool(root and root['state'] in ['current','keep','target'] and len(root_entries)==1 and root_entries[0]['group']==stage['scopeGroup'])
  if mode=='legacy-current-default':
   valid_root=valid_root and stage.get('publicKey')=='main-content' and stage['publicKey'] in root_entries[0].get('labels',[])
  elif mode=='current-root-address':
   root_path=root['target']['path'] if root['state']=='target' else root['current']['path']
   valid_root=valid_root and stage.get('publicKey')==root_path==root_entries[0]['address'] and bool(stage.get('transitionEvidence')) and manifest['groupModes'].get(stage['scopeGroup'],manifest.get('currentGroupModes',{}).get(stage['scopeGroup'])) in ['PackSeparately','PackTogetherByLabel']
  else:
   valid_root=valid_root and stage.get('publicKey')==root['current']['path']==root_entries[0]['address'] and manifest.get('currentGroupModes',{}).get(stage['scopeGroup'])=='PackSeparately' and bool(stage.get('transitionEvidence'))
  if not valid_root:add('PACK_STAGE_ROOT',root['current']['path'] if root else 'Assets','Main/UI exact root or its declared packing contract drifted')
  for kind in ['producer','consumer']:
   src=stage.get(kind);safe(src);f=Path(project)/src
   if not f.is_file() or hashlib.sha256(f.read_bytes()).hexdigest()!=stage.get(kind+'Sha256'):add('PACK_STAGE_SOURCE',src,'Stage producer/consumer changed without a frozen matching contract')
 # Validate actual schemas, not only entry labels. Preserve current schemas until their batch changes them.
 expected_modes=dict(manifest.get('currentGroupModes',{}))
 effective_modes=gc.expected_modes(manifest,rr.expected_modes(manifest,ma.expected_modes(manifest,MODES)))
 for row in manifest['records']:
  if row['state']=='target' and row['target'].get('group'):expected_modes[row['target']['group']]=effective_modes[row['target']['group']]
 for group in NEW_GROUPS:expected_modes[group]=effective_modes[group]
 if manifest.get('groupConsolidation'):expected_modes['hotUpdate']=effective_modes['hotUpdate']
 for group,mode in expected_modes.items():
  if group=='Built In Data':continue
  f=Path(project)/'Assets/AddressableAssetsData/AssetGroups/Schemas'/(group+'_BundledAssetGroupSchema.asset')
  if not f.exists():continue
  text=f.read_text(encoding='utf-8-sig');m=re.search(r'm_BundleMode: (\d+)',text)
  if not m or {'0':'PackTogether','1':'PackSeparately','2':'PackTogetherByLabel'}.get(m[1]) != mode:add('PACK_SCHEMA',f.relative_to(project).as_posix(),'Group BundleMode changed outside its declared state')
  if group in NEW_GROUPS or group=='hotUpdate' and manifest.get('groupConsolidation'):
   ids=manifest.get('remoteProfileIds',{})
   for field,key in [('m_BuildPath','build'),('m_LoadPath','load')]:
    found=re.search(field+r':\s*\n\s+m_Id: ([^\s]+)',text)
    if not ids.get(key) or not found or found[1]!=ids[key]:add('PACK_REMOTE',f.relative_to(project).as_posix(),'New business group must use approved Remote '+key+' profile variable')
 # New or migrated runtime roots cannot acquire another card/map/page through serialized assets.
 active={}
 for path,asset in records.items():
  row=byguid.get(asset['guid'])
  if row:
   if row['state']=='target' and row['target'].get('group'):active[asset['guid']]={'path':path,**row['target'],'new':True}
   elif row['current'].get('entries'):active[asset['guid']]={'path':path,'group':row['current']['entries'][0]['group'],'bundleKey':None,'new':False}
  else:
   try:rule=route(path,domain)
   except ValueError:rule=None
   if rule and rule['runtime']:active[asset['guid']]={'path':path,**rule,'new':True}
 if dr.enabled(manifest.get('resourceDomainPolicy')):
  for guid,item in active.items():
   row=byguid.get(guid);override=packing_overrides(manifest).get(guid)
   if override:item.update(group=override['entry']['group'],bundleKey=override['entry']['labels'][0] if override['entry']['labels'] else None,new=True)
   base=row['target'] if row else item
   view=dr.annotated(base.get('group'),base.get('bundleKey'),manifest['resourceDomainPolicy'],item['path'],base.get('group'))
   item['domainRole']=view['domainRole']
   item['domainApplied']=bool(not row or override and override['purpose']=='domain-consolidation' or ma.active(manifest,guid) or rr.active(manifest,guid) or gc.active(manifest,guid))
   if gc.active(manifest,guid):item['domainRole']=gc.role(gc.members(manifest)[guid])
   if override and override['purpose']=='domain-consolidation':item['domainRole']=dr.role(item['group'],item['bundleKey'],manifest['resourceDomainPolicy'])
   if guid in manifest['resourceDomainPolicy'].get('nonConsumerRoots',{}):item['new']=False
 for source in active.values():
  if not source['new'] or Path(source['path']).suffix not in ['.prefab','.mat','.asset','.unity']:continue
  for guid in set(re.findall(r'\bguid:\s*([a-f0-9]{32})',(Path(project)/source['path']).read_text(encoding='utf-8-sig',errors='replace'))):
   target=active.get(guid)
   if not target:continue
   sg,tg=source['group'],target['group'];bad=dependency_violation(source,target)
   if bad:
    add('PACK_DEPENDENCY',source['path'],'Forbidden static package dependency: '+sg+' -> '+tg+' / '+target['path']);out[-1]['relatedPaths']=[target['path']]
 out.extend(retirement.audit_deleted_assets(project,manifest,records,entries))
 out.extend(gc.audit_closed_groups(project,manifest,records,entries))
 out.extend(gc.audit_identities(project,manifest,records))
 out.extend(sc.audit(project,manifest,records,entries))
 return out

def audit_dependency_snapshot(project,policy,records,entries,snapshot):
 manifest=load_manifest(project,policy)
 if manifest is None:raise ValueError('Dependency snapshot needs enabled pack policy')
 data=read(snapshot)
 if data.get('schemaVersion')!=1 or str(Path(data.get('project','')).resolve()).casefold()!=str(Path(project).resolve()).casefold() or data.get('producer')!='AssetDatabase.GetDependencies' or not isinstance(data.get('nodes'),dict):raise ValueError('Invalid/incomplete native dependency snapshot')
 nodes=data['nodes']
 if any(not isinstance(v,list) or any(not isinstance(x,str) for x in v) for v in nodes.values()):raise ValueError('Invalid dependency adjacency list')
 guidpaths={r['guid']:p for p,r in records.items() if r['guid']};byguid={r['guid']:r for r in manifest['records']};owners={};peers=collections.defaultdict(list);member={};out=[]
 for e in entries:
  path=guidpaths.get(e['guid'])
  if not path:continue
  row=byguid.get(e['guid']);new=(not row and path.startswith(MANAGED)) or bool(row and (row['state']=='target' or row['guid'] in packing_overrides(manifest)));key=row['target'].get('bundleKey') if row and new else e.get('labels',[None])[0] if new and len(e.get('labels',[]))==1 else None
  owners[path]=dict(path=path,group=e['group'],bundleKey=key,new=new)
  if dr.enabled(manifest.get('resourceDomainPolicy')):
   override=packing_overrides(manifest).get(e['guid'])
   if override:key=e.get('labels',[None])[0] if len(e.get('labels',[]))==1 else None;owners[path]['bundleKey']=key
   base=row['target'] if row else owners[path]
   view=dr.annotated(base.get('group'),base.get('bundleKey'),manifest['resourceDomainPolicy'],path,base.get('group'));owners[path]['domainRole']=view['domainRole']
   owners[path]['domainApplied']=bool(not row or override and override['purpose']=='domain-consolidation' or ma.active(manifest,e['guid']) or rr.active(manifest,e['guid']) or gc.active(manifest,e['guid']))
   if gc.active(manifest,e['guid']):owners[path]['domainRole']=gc.role(gc.members(manifest)[e['guid']])
   if override and override['purpose']=='domain-consolidation':owners[path]['domainRole']=dr.role(e['group'],key,manifest['resourceDomainPolicy'])
   if e['guid'] in manifest['resourceDomainPolicy'].get('nonConsumerRoots',{}):owners[path]['new']=False

  mode=manifest['groupModes'].get(e['group'],manifest['currentGroupModes'].get(e['group'],'PackSeparately'))
  if row and not new:mode=manifest['currentGroupModes'].get(e['group'],mode)
  groupkey=(e['group'],tuple(sorted(e.get('labels',[]))) if mode=='PackTogetherByLabel' else path if mode=='PackSeparately' else None)
  member[path]=groupkey;peers[groupkey].append(path)
 if any(p not in nodes for p in owners):raise ValueError('Native dependency snapshot omits registered runtime roots')
 roots=[p for p,o in owners.items() if o['new'] or o['group'] in ['opening','title']]
 for root in roots:
  origin=owners[root];pending=collections.deque([(root,[root])]);seen=set()
  while pending:
   path,chain=pending.popleft()
   if path in seen:continue
   seen.add(path);target=owners.get(path)
   if target and dependency_violation(origin,target):out.append(dict(code='PACK_NATIVE_DEPENDENCY',path=root,guid=records[root]['guid'],severity='error',message='Forbidden native/bundle-peer dependency: '+' -> '.join(chain),relatedPaths=[path]));continue
   if path.startswith('Assets/Art/font/'):continue
   if path not in nodes:raise ValueError('Native dependency snapshot omits recursive node: '+path)
   for dependency in nodes[path]:
    if dependency!=path and not dependency.endswith('.cs'):pending.append((dependency,chain+[dependency]))
   for sibling in peers.get(member.get(path),[]):
    if sibling!=path:pending.append((sibling,chain+['[same Bundle] '+sibling]))
 return out,dict(runtimeRoots=len(owners),strictRoots=len(roots),legacyRootsPendingMigration=len(owners)-len(roots),nodeCount=len(nodes),notActualBuildReport=True)

def validate_transitions(project,policy,plan):
 manifest=load_manifest(project,policy)
 if manifest is None:return
 rows={r['guid']:r for r in manifest['records']};moves=plan.get('moves',[]);selected=set(plan.get('packTransitionGuids',[]))|{m['guid'] for m in moves if m['guid'] in rows}
 production=any(g in rows and rows[g]['batch']!='P0TEST' for g in selected)
 if manifest.get('supplementRevision')=='RESOURCE-PACK-NORM/R1' and production:
  scope=manifest['testScopes'].get(plan.get('testScope'))
  if not scope or not selected.issubset(set(scope['allowedGuids'])):raise ValueError('Batch exceeds its declared minimum-test/governance scope')
 if len(set(plan.get('packTransitionGuids',[])))!=len(plan.get('packTransitionGuids',[])):raise ValueError('Repeated pack transition GUID')
 for guid in selected:
  row=rows.get(guid)
  if not row or row['state']!='current' or not row.get('migratable') or not row['batch'] or not plan['batchId'].startswith(row['batch']+'-'):raise ValueError('Unapproved pack transition/state/batch')
  if not manifest.get('migrationEnabled',False) and not (row['batch']=='P0TEST' and row['current']['path'].startswith('Assets/ResourceData/uiDebug/gm/p0-fixture-')):raise ValueError('P0 awaiting user acceptance; production migrations disabled')
  if row.get('implementationGate') and not (isinstance(row.get('gateEvidence'),str) and row['gateEvidence'].strip()):raise ValueError('Unresolved implementation gate: '+row['current']['path'])
  move=next((m for m in moves if m['guid']==guid),None)
  if row['current']['path']!=row['target']['path'] and (not move or move['source']!=row['current']['path'] or move['target']!=row['target']['path']):raise ValueError('Pack move differs from approved source/target')
  if row['current']['path']==row['target']['path'] and move:raise ValueError('Registration-only transition must not fabricate a move')
 for move in moves:
  if move['guid'] not in rows:raise ValueError('Move has no explicit approved manifest identity')
