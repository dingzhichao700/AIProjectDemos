"""Shared resource audit/preflight. Python standard library only; never mutates assets."""
import argparse, collections, hashlib, json, os, re, sys, struct
from pathlib import Path
BOOT_ERROR=None
try:
 import consolidation_rules as gc
 import source_cleanup_rules as sc
 from card_suite_rules import audit_card_suites
 from skill_icon_rules import audit_skill_icons
 from item_icon_rules import audit_item_icons
 from pack_rules import NEW_GROUPS, OLD_GROUPS, load_manifest, audit_packs, route, domains, validate_transitions, permitted_stale, routing_report, audit_dependency_snapshot, shared_source_override
except ImportError as error:
 BOOT_ERROR=str(error);OLD_GROUPS={'opening','title','default','scene','cutScene'};NEW_GROUPS=set()
VERSION='1.0.0'
GROUPS=OLD_GROUPS|NEW_GROUPS
IMAGES={'.png','.jpg','.jpeg','.tga','.webp','.psd'}
VERSION_NAME=re.compile(r'(?:V|R)\d+(?=$|[-_A-Z])|(?:^|[-_])(?:v|r)\d+(?=$|[-_])|Trial|^temp-|^(?:legacy|current)$')
def digest(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as f:
  for chunk in iter(lambda:f.read(1048576),b''):h.update(chunk)
 return h.hexdigest()
def load_json(path):return json.loads(Path(path).read_text('utf-8-sig'))
def safe_path(project,value):
 if not isinstance(value,str) or not value.startswith('Assets/') or '\\' in value:raise ValueError('Expected Assets/ with forward slashes')
 parts=value.split('/')
 if any(x in ['', '.', '..'] or ':' in x or x.endswith((' ','.')) or re.match(r'^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)',x,re.I) for x in parts):raise ValueError('Unsafe path: '+value)
 root=Path(project).resolve();path=root.joinpath(*parts)
 if not path.resolve().is_relative_to(root/'Assets'):raise ValueError('Path escapes Assets')
 for item in [path]+list(path.parents):
  if item==root:break
  if item.is_symlink() or (hasattr(item,'is_junction') and item.is_junction()):raise ValueError('Linked asset path: '+value)
 return path
def guid_for(path):
 meta=Path(str(path)+'.meta')
 if not meta.exists():return None
 m=re.search(r'^guid: ([a-f0-9]{32})\s*$',meta.read_text('utf-8-sig'),re.M);return m[1] if m else None
def validate_policy(p):
 if BOOT_ERROR:raise ValueError('Governance module unavailable: '+BOOT_ERROR)
 if not isinstance(p,dict):raise ValueError('Policy must be a JSON object')
 if p.get('schemaVersion')!=1 or p.get('toolVersion')!=VERSION or not OLD_GROUPS.issubset(set(p.get('groups',[]))) or not set(p.get('groups',[])).issubset(GROUPS):raise ValueError('Unsupported policy/tool/groups')
 ids=set()
 for e in p.get('legacyIssues',[]):
  if e['id'] in ids or not all(e.get(k) for k in ['code','path','guid','owner','basis','reviewBatch','exitCondition']) or '*' in e['path'] or '?' in e['path']:raise ValueError('Invalid legacy exception ownership/scope')
  ids.add(e['id'])
def inventory(project):
 records={};folders=[]
 for root,dirs,files in os.walk(Path(project)/'Assets',followlinks=False):
  d=Path(root);dirs[:]=sorted(x for x in dirs if not (d/x).is_symlink() and not (hasattr(d/x,'is_junction') and (d/x).is_junction()));folders.append(d)
  for name in sorted(files):
   f=d/name
   if f.suffix=='.meta':continue
   rel=f.relative_to(project).as_posix();safe_path(project,rel);records[rel]={'guid':guid_for(f),'ext':f.suffix.lower()}
 return records,folders
def aa_entries(project):
 out=[]
 for f in sorted((Path(project)/'Assets/AddressableAssetsData/AssetGroups').glob('*.asset')):
  t=f.read_text('utf-8-sig');m=re.search(r'^  m_GroupName: (.*)$',t,re.M);group=m[1].strip() if m else f.stem
  for block in re.split(r'^  - m_GUID: ',t,flags=re.M)[1:]:
   guid=block.splitlines()[0].strip();a=re.search(r'^    m_Address: (.*)$',block,re.M)
   if a:out.append({'guid':guid,'group':group,'address':a[1].strip().strip('"'),'file':f.relative_to(project).as_posix(),'labels':[v.strip() for v in re.findall(r'^    - ([^\r\n]+)',re.split(r'^  m_ReadOnly:',block,maxsplit=1,flags=re.M)[0],re.M)]})
 return out
def audit(project,policy,scopes=None,journal_root=None,active_journal=None,allow_pending_registration=False,dependency_snapshot=None):
 project=Path(project).resolve();validate_policy(policy);records,folders=inventory(project);findings=[]
 manifest=load_manifest(project,policy);domain=domains(project) if manifest else {};target_paths={r['target']['path'] for r in manifest['records']} if manifest else set()
 vendor=tuple(policy['protectedVendorRoots']);global_roots=tuple(policy['globalResourceRoots']);modules=set(policy['modules']);dupes=collections.defaultdict(list);guid_paths=collections.defaultdict(list)
 def add(code,path,message,severity='error'):findings.append({'code':code,'path':path,'guid':records.get(path,{}).get('guid'),'severity':severity,'message':message})
 for rel,row in records.items():
  path=project/rel;parts=rel.split('/');ext=row['ext']
  if row['guid']:guid_paths[row['guid']].append(rel)
  if rel.startswith(vendor):continue
  if '/Resources/' in rel and rel not in policy['bootstrapResources']:add('BUSINESS_RESOURCES',rel,'Undeclared business Resources asset')
  if rel.startswith('Assets/StreamingAssets/') and rel not in policy.get('streamingAssets',[]):add('STREAMING_UNDECLARED',rel,'Undeclared StreamingAssets bypass')
  if rel.startswith('Assets/Art/'):
   if any(VERSION_NAME.search(p) for p in parts[3:]):add('PRODUCTION_VERSION_NAME',rel,'Production version/trial in runtime address')
   if ext=='.png':
    dupes[digest(path)].append(rel)
    with path.open('rb') as image:header=image.read(24)
    if len(header)!=24 or header[:8]!=b'\x89PNG\r\n\x1a\n' or header[12:16]!=b'IHDR' or not all(struct.unpack('>II',header[16:24])):add('PNG_ENCODING',rel,'PNG extension does not contain a valid PNG header/dimensions')
   if rel.startswith(global_roots):continue
   category=parts[2]
   if manifest:
    try:new_route=gc.approved_route(manifest,rel,row['guid']) or route(rel,domain)
    except ValueError:new_route=None
    if new_route is not None:continue
    if rel in target_paths and parts[3] in NEW_GROUPS:continue
   if category not in policy['artTypes']:add('ART_TYPE',rel,'Undeclared Art type');continue
   if len(parts)<5:add('MATERIAL_LAYOUT' if category=='material' else 'ART_LAYOUT',rel,'Missing Group/module');continue
   if parts[3] not in GROUPS:add('FRAME_GROUP' if category=='frameAnimation' else 'ART_GROUP',rel,'Missing declared loading Group');continue
   module=parts[4]
   if category=='atlas':
    if len(parts)!=5 or ext!='.png':add('ATLAS_LAYOUT',rel,'UI atlas must be Art/atlas/Group/atlasName.png')
    module=Path(parts[4]).stem
   elif category=='atlasSource':
    if len(parts)!=6 or ext!='.png':add('SOURCE_LAYOUT',rel,'UI source requires Group/atlasName/*.png without children')
   elif category=='unpack':
    if len(parts)<6:add('UNPACK_LAYOUT',rel,'Missing independent-image module')
    if ext not in IMAGES:add('UNPACK_TYPE',rel,'Prefab/playback/encoded data needs its own type')
   elif category=='frameAnimation':
    if len(parts)<6 or ext not in ['.png','.json']:add('FRAME_LAYOUT',rel,'Frames need Group/module and PNG/JSON or declared auxiliary data')
   elif category=='skeletalAnimation':
    if len(parts)<8 or ext not in policy['skeletalExtensions']:add('SKELETAL_LAYOUT',rel,'Native skeletal assets need Group/module/ID/action and allowed type')
   elif category=='audio':
    if len(parts)<6 or ext not in policy['audioExtensions']:add('AUDIO_LAYOUT',rel,'Audio needs Group/module and supported type')
   elif category=='material':
    if len(parts)<6 or ext!='.mat':add('MATERIAL_LAYOUT',rel,'Material needs Group/module or a declared global root')
   if '.' not in module and module not in modules:add('UNKNOWN_MODULE',rel,'Unregistered module ownership')
  elif rel.startswith('Assets/Prefab/'):
   if manifest and (parts[2] in NEW_GROUPS):continue
   if len(parts)<5 or parts[2] not in GROUPS:add('PREFAB_LAYOUT',rel,'Prefab needs Group/module or stable bootstrap exception')
   if ext!='.prefab':add('PREFAB_TYPE',rel,'Non-prefab in Prefab directory')
  elif rel.startswith('Assets/ResourceData/'):
   if manifest and (parts[2] in NEW_GROUPS):
    if ext=='.json':
     try:load_json(path)
     except (ValueError,UnicodeError):add('DATA_JSON',rel,'Malformed presentation JSON')
    continue
   if len(parts)<5 or parts[2] not in GROUPS or parts[3] not in modules or ext not in ['.json','.bytes']:add('RESOURCE_DATA_LAYOUT',rel,'Data needs Group/module and JSON/bytes')
   if ext=='.json':
    try:load_json(path)
    except (ValueError,UnicodeError):add('DATA_JSON',rel,'Malformed presentation JSON')
 for guid,paths in guid_paths.items():
  if len(paths)>1:add('DUPLICATE_GUID',paths[0],'GUID collision: '+', '.join(paths));findings[-1]['relatedPaths']=paths
 for rel,row in records.items():
  if not rel.startswith(vendor) and rel.startswith(('Assets/Art/','Assets/Prefab/','Assets/ResourceData/')) and not row['guid']:add('MISSING_META_GUID',rel,'Managed asset lacks meta/GUID')
 for d in folders:
  rel=d.relative_to(project).as_posix()
  if rel.startswith('Assets/Art/atlasSource/') and len(rel.split('/'))==5 and any(x.is_dir() for x in d.iterdir()):add('SOURCE_CHILD_DIRECTORY',rel,'UI source folder contains child directories')
 for rel,row in records.items():
  if not rel.startswith('Assets/Art/frameAnimation/'):continue
  path=project/rel
  if row['ext']=='.png' and not path.with_suffix('.json').exists():add('FRAME_PAIR',rel,'Missing same-stem playback JSON')
  if row['ext']=='.json' and rel not in policy.get('frameAuxiliaryJson',[]):
   try:
    data=load_json(path)
    if not isinstance(data,dict) or not all(k in data for k in ['frames','meta','pivot']) or not data['frames'] or not path.with_suffix('.png').exists():add('FRAME_SCHEMA',rel,'Missing paired image/frames/meta/pivot')
    else:
     frames=data['frames'].values() if isinstance(data['frames'],dict) else data['frames']
     if any(frame.get('rotated') for frame in frames):add('FRAME_ROTATION',rel,'Rotated frames not supported by current contract')
   except (ValueError,UnicodeError,AttributeError,TypeError):add('FRAME_JSON',rel,'Malformed playback JSON')
 entries=aa_entries(project)
 for e in entries:
  if e['group']=='Built In Data' and e['guid'] in policy['builtInVirtualGuids']:continue
  paths=guid_paths.get(e['guid'],[])
  if not paths:add('AA_MISSING_GUID',e['file'],'Missing asset GUID: '+e['guid']);continue
  for path in paths:
   expected=path
   for alias in policy.get('generatedAddressRules',[]):
    if path.startswith(alias['assetPrefix']) and path.endswith(alias['stripSuffix']):expected=alias['addressPrefix']+path[len(alias['assetPrefix']):-len(alias['stripSuffix'])];break
   if e['address']!=expected and not (allow_pending_registration and permitted_stale(path,e,manifest)):add('AA_ADDRESS',path,'Address differs from declared identity: '+e['address'])
   if path.startswith('Assets/Art/atlasSource/') and not shared_source_override(path,e['guid'],manifest):add('SOURCE_RUNTIME',path,'Editor-only source registered for runtime')
 known_duplicates={tuple(sorted(g['paths'])) for g in policy.get('duplicateReviewGroups',[])}
 mapped_duplicates=gc.remapped_duplicate_reviews(project,manifest,policy.get('duplicateReviewGroups',[]),records) if manifest else {}
 if manifest:mapped_duplicates.update(sc.remaining_duplicate_reviews(project,manifest,policy.get('duplicateReviewGroups',[]),records))
 known_duplicates.update(mapped_duplicates)
 for paths in dupes.values():
  if len(paths)>1:add('DUPLICATE_CONTENT_REVIEW',sorted(paths)[0],'PNG bytes match; inspect importer/lifetime/derivation: '+', '.join(sorted(paths)),'warning' if tuple(sorted(paths)) in known_duplicates else 'error');findings[-1]['relatedPaths']=paths
  if len(paths)>1 and tuple(sorted(paths)) in mapped_duplicates:findings[-1].update(duplicateReviewId=mapped_duplicates[tuple(sorted(paths))]['id'],duplicateReviewSourcePaths=mapped_duplicates[tuple(sorted(paths))]['sourcePaths'])
 def strings(value):
  if isinstance(value,dict):
   for child in value.values():yield from strings(child)
  elif isinstance(value,list):
   for child in value:yield from strings(child)
  elif isinstance(value,str) and value.startswith('Assets/'):yield value
 for config in (project/'Assets/ConfigBin').glob('*.json'):
  try:data=load_json(config)
  except (ValueError,UnicodeError):add('CONFIG_JSON',config.relative_to(project).as_posix(),'Malformed generated JSON');continue
  for address in set(strings(data)):
   if address.startswith('Assets/Art/atlas/') and not address.endswith('.png'):
    atlas,sprite=address.rsplit('/',1);meta=project/(atlas+'.png.meta')
    if not meta.exists() or not re.search(r'^\s+'+re.escape(sprite)+r':\s+-?\d+\s*$',meta.read_text('utf-8-sig'),re.M):add('CONFIG_SPRITE',config.relative_to(project).as_posix(),'Unresolved atlas sprite: '+address)
   elif address not in records:add('CONFIG_ADDRESS',config.relative_to(project).as_posix(),'Unresolved address: '+address)
 findings.extend(audit_packs(project,policy,records,entries,allow_pending_registration))
 dependency_stats=None
 if dependency_snapshot:
  native_findings,dependency_stats=audit_dependency_snapshot(project,policy,records,entries,dependency_snapshot);findings.extend(native_findings)
 findings.extend(audit_card_suites(project,policy,records))
 findings.extend(audit_skill_icons(project,policy,records))
 findings.extend(audit_item_icons(project,policy,records))
 legacy={(e['code'],e['path'],e['guid']):e for e in policy.get('legacyIssues',[])}
 for item in findings:
  exception=legacy.get((item['code'],item['path'],item['guid']))
  if exception and item['severity']=='error':item.update(severity='legacy',exceptionId=exception['id'],reviewBatch=exception['reviewBatch'])
 if journal_root:
  for path in Path(journal_root).glob('*/journal.json'):
   journal=load_json(path)
   own_verify=active_journal and path.resolve()==Path(active_journal).resolve() and journal.get('state')=='verifying'
   if journal.get('project')==str(project) and journal.get('state') not in ['committed','rolled-back'] and not own_verify:add('UNFINISHED_TRANSACTION','Assets','Resolve durable journal: '+str(path))
 if scopes:
  for scope in scopes:
   if scope not in ['Assets','Assets/']:safe_path(project,scope.rstrip('/'))
  findings=[f for f in findings if any(path==s.rstrip('/') or path.startswith(s.rstrip('/')+'/') for path in [f['path']]+f.get('relatedPaths',[]) for s in scopes) or f['code']=='UNFINISHED_TRANSACTION']
 return {'packDependencyStats':dependency_stats,'packRouting':routing_report(project,policy,records),'toolVersion':VERSION,'project':str(project),'scope':scopes or ['Assets'],'assetCount':len(records),'addressableEntryCount':len(entries),'errors':[f for f in findings if f['severity']=='error'],'legacy':[f for f in findings if f['severity']=='legacy'],'warnings':[f for f in findings if f['severity']=='warning'],'limitations':['Source audit is not runtime reachability or a Bundle report','No audit authorizes deletion','Legacy exceptions are exact GUID/path with ownership and batch exit conditions']}
def preflight_plan(project,plan,plan_sha256=None):
 if plan.get('schemaVersion')!=1 or not plan.get('batchId') or not plan.get('approvalEvidence'):raise ValueError('Missing batch identity/approval evidence')
 if plan.get('kind')=='group-consolidation':
  import consolidation_rules as gc
  manifest=load_manifest(project,load_json(Path(project)/'ProjectSettings/ResourceGovernance.json'))
  return gc.preflight(project,manifest,plan,aa_entries(project),plan_sha256)
 if plan.get('kind')=='resource-relocation':
  import relocation_rules as rr
  manifest=load_manifest(project,load_json(Path(project)/'ProjectSettings/ResourceGovernance.json'))
  return rr.preflight(project,manifest,plan,aa_entries(project),plan_sha256)
 if plan.get('kind')=='matching-admission':
  import matching_admission_rules as ma
  manifest=load_manifest(project,load_json(Path(project)/'ProjectSettings/ResourceGovernance.json'))
  return ma.preflight(project,manifest,plan,aa_entries(project),plan_sha256)
 if plan.get('kind')=='domain-regroup':return preflight_domain_regroup(project,plan)
 policy_path=Path(project)/'ProjectSettings/ResourceGovernance.json'
 if policy_path.exists():validate_transitions(project,load_json(policy_path),plan)
 targets=set();sources=set()
 for move in plan.get('moves',[]):
  source=safe_path(project,move['source']);target=safe_path(project,move['target'])
  if move['source'] in sources or move['target'].casefold() in targets:raise ValueError('Repeated source/target or case collision')
  sources.add(move['source']);targets.add(move['target'].casefold())
  if not source.is_file() or target.exists() or Path(str(target)+'.meta').exists():raise ValueError('Missing source/occupied target')
  if guid_for(source)!=move['guid'] or digest(source)!=move['sha256']:raise ValueError('Source GUID/hash changed')
 if plan.get('deletes'):raise ValueError('Deletion disabled: a separate exact list needs user approval')
 if not sources and not plan.get('packTransitionGuids'):raise ValueError('Empty move/registration plan')
 return {'batchId':plan['batchId'],'moveCount':len(sources),'planReady':True,'executed':False}

def preflight_domain_regroup(project,plan):
 import domain_rules as dr
 if plan.get('moves') or plan.get('deletes'):raise ValueError('Regrouping cannot move or delete physical resources')
 manifest=load_manifest(project,load_json(Path(project)/'ProjectSettings/ResourceGovernance.json'))
 policy=manifest.get('resourceDomainPolicy');dr.validate_policy(policy)
 rows={r['guid']:r for r in manifest['records']};entries=collections.defaultdict(list)
 for e in aa_entries(project):entries[e['guid']].append({k:e[k] for k in ['group','address','labels']})
 selected=plan.get('regroupings',[]);approved=[];current={}
 if sorted(plan.get('packTransitionGuids',[]))!=sorted(r['guid'] for r in selected):raise ValueError('Journal selection differs from regroup selection')
 for item in selected:
  row=rows.get(item['guid'])
  if not row:raise ValueError('Missing governed resource')
  path=row['target']['path'] if row['state']=='target' else row['current']['path'];source=safe_path(project,path)
  if guid_for(source)!=row['guid'] or len(entries[row['guid']])!=1:raise ValueError('Missing identity or ambiguous AA registration')
  group,key=dr.owner(row['target'].get('group'),row['target'].get('bundleKey'),path,policy)
  expected={'group':group,'address':path,'labels':[key]}
  dr.validate_override(row,{'path':path,'entry':expected},policy)
  observed={'guid':row['guid'],'path':path,'sha256':digest(source),'before':entries[row['guid']][0]}
  current[row['guid']]=observed
  approved.append(dict(observed,after=expected))
 count=dr.validate_regroup_selection(selected,approved,current)
 return {'batchId':plan['batchId'],'regroupCount':count,'moveCount':0,'planReady':True,'executed':False}
def verify_lock(project):
 root=Path(project).resolve();lock=load_json(root/'Tools/ResourceGovernance/ResourceGovernance.lock.json')
 if lock.get('toolVersion')!=VERSION or lock.get('apiVersion')!='1.0':raise ValueError('Tool lock version mismatch')
 for name,expected in lock['files'].items():
  path=(root/name).resolve()
  if not path.is_relative_to(root) or not path.is_file() or digest(path)!=expected:raise ValueError('Locked governance input missing/changed: '+name)
def main(argv=None):
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',required=True);p.add_argument('--policy');p.add_argument('--scope',action='append');p.add_argument('--output');p.add_argument('--journal-root');p.add_argument('--active-journal');p.add_argument('--allow-pending-registration',action='store_true');p.add_argument('--dependency-snapshot');p.add_argument('--preflight-plan');args=p.parse_args(argv)
 try:
  if BOOT_ERROR:raise ValueError('Governance module unavailable: '+BOOT_ERROR)
  verify_lock(args.project)
  if args.policy and Path(args.policy).resolve()!=(Path(args.project)/'ProjectSettings/ResourceGovernance.json').resolve():raise ValueError('Production CLI requires the canonical project policy; use isolated fixtures for alternate policies')
  if args.preflight_plan:result=preflight_plan(args.project,load_json(args.preflight_plan),digest(args.preflight_plan));code=0
  else:result=audit(args.project,load_json(args.policy or Path(args.project)/'ProjectSettings/ResourceGovernance.json'),args.scope,args.journal_root,args.active_journal,args.allow_pending_registration,args.dependency_snapshot);code=2 if result['errors'] else 0
 except (OSError,ValueError,KeyError,TypeError) as error:result={'toolVersion':VERSION,'error':str(error),'state':'evidence-or-schema-invalid'};code=3
 if args.output:
  f=Path(args.output);f.parent.mkdir(parents=True,exist_ok=True);f.write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf8')
 print(json.dumps({'exitCode':code,'errors':len(result.get('errors',[])),'legacy':len(result.get('legacy',[])),'warnings':len(result.get('warnings',[])),'error':result.get('error'),'output':args.output},ensure_ascii=True));return code
if __name__=='__main__':sys.exit(main())
