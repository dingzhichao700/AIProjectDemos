"""Card-suite completeness and two-source lifecycle checks. Sole CLI extension, read-only."""
import json,hashlib,re,collections
from pathlib import Path

MANIFEST='ProjectSettings/ResourcePackManifest.json'
SOURCE_DIRECTIONS={2,4}
ACTION_NAMES={1:'Move',2:'Death',3:'Attack1',4:'Idle'}
USAGES={'cardFace','portrait','displayAnimation','skillIcons','fragmentIcon','battleReveal','unitActions','productionBuilding','ruin','projectileAndEffects'}
STATES={'configured','missing','candidate','not-applicable'}
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def fingerprint(unit):return hashlib.sha256(json.dumps({'appearances':unit.get('appearances',[]),'firePoints':unit.get('firePoints',[])},ensure_ascii=False,sort_keys=True,separators=(',',':')).encode('utf-8')).hexdigest()
def table(project,name):
 p=Path(project)/'Assets/ConfigBin'/('cfgobj_'+name+'obj.json')
 return read(p) if p.exists() else []

def audit_card_suites(project,policy,records):
 if not policy.get('cardSuiteGovernance'):return []
 root=Path(project);rule=policy['cardSuiteGovernance'];out=[];default='Assets/ConfigBin/cfgobj_unitobj.json'
 def add(code,message,path=default,severity='error'):out.append(dict(code=code,path=path,guid=records.get(path,{}).get('guid'),severity=severity,message=message))
 if rule!={'schemaVersion':1,'manifest':MANIFEST,'revision':'RESOURCE-PACK/R2'}:raise ValueError('Invalid card suite policy')
 data=read(root/MANIFEST);contract=data.get('cardSuites')
 if not isinstance(contract,dict) or contract.get('revision')!='RESOURCE-PACK/R2' or contract.get('sourceDirections')!=[2,4]:raise ValueError('Missing/invalid two-source suite contract')
 cards=table(root,'card');cardmap={c['id']:c for c in cards if c.get('showInLibrary')};units={u['id']:u for u in table(root,'unit')};skills={s['id']:s for s in table(root,'skill')};prod={p['unitId']:p for p in table(root,'unitproduction')};buildings={b['id']:b for b in table(root,'building')};arts={a['id']:a for a in table(root,'cardart')}
 suites=contract.get('cards',[])
 if not isinstance(suites,list) or len({c['cardId'] for c in suites})!=len(suites) or {c['cardId'] for c in suites}!=set(cardmap):raise ValueError('Suite matrix must match current visible cards exactly')
 exceptions=contract.get('legacyUnits',[])
 if not isinstance(exceptions,list) or len({e['unitId'] for e in exceptions})!=len(exceptions):raise ValueError('Duplicate/invalid legacy unit exceptions')
 legacy={e['unitId']:e for e in exceptions};legacy_paths=set();active_paths=set();bound_units=set()
 for suite in suites:
  cid=suite['cardId'];card=cardmap[cid];uses=suite.get('usages',{})
  if set(uses)!=USAGES or suite.get('readiness') not in ['partial','complete','display-only']:raise ValueError('Incomplete suite usage schema: '+str(cid))
  for name,use in uses.items():
   if use.get('status') not in STATES or not isinstance(use.get('evidence'),str) or not use['evidence'].strip():raise ValueError('Usage requires explicit state and evidence: '+str(cid)+'/'+name)
  if suite['readiness']=='complete' and any(u['status'] in ['missing','candidate'] for u in uses.values()):add('SUITE_FALSE_COMPLETE','Card marked complete despite missing/candidate usages: '+str(cid))
  if suite['readiness']=='complete' and any(uses[u]['status']!='configured' for u in ['cardFace','portrait','unitActions','productionBuilding','skillIcons','fragmentIcon']):add('SUITE_FALSE_COMPLETE','Required card usages cannot be marked not-applicable: '+str(cid))
  aid=card.get('artId');art=arts.get(aid)
  for name,field in [('cardFace','cardImage'),('portrait','portrait')]:
   if uses[name]['status']=='configured' and (not art or not art.get(field) or not (root/art[field]).is_file()):add('SUITE_DISPLAY','Configured display asset missing: '+str(cid)+'/'+name,'Assets/ConfigBin/cfgobj_cardartobj.json')
  uid=card.get('content',{}).get('unitId');unit=units.get(uid)
  if suite.get('unitId')!=uid or suite.get('artId')!=aid:add('SUITE_IDENTITY','Card/Unit/Art explicit mapping drift: '+str(cid),'Assets/ConfigBin/cfgobj_cardobj.json')
  if unit is None:
   if uses['unitActions']['status']=='configured' or suite['readiness']=='complete':add('SUITE_UNIT','Battle-ready claim lacks unit: '+str(cid))
   continue
  bound_units.add(uid)
  p=prod.get(uid);bid=p.get('buildingId') if p else None
  if suite.get('buildingId')!=bid or bid not in buildings:add('SUITE_BUILDING','Production building mapping missing/drift: '+str(cid),'Assets/ConfigBin/cfgobj_unitproductionobj.json')
  appearances=unit.get('appearances',[]);seen=collections.Counter((a['action'],a['direction']) for a in appearances)
  if any(n!=1 for n in seen.values()):add('SUITE_DUPLICATE_DIRECTION','Duplicate action/direction: '+str(uid))
  required={1,2,4}
  for sid in unit.get('skillIds',[]):
   if sid not in skills:add('SUITE_SKILL','Missing actual skill: '+str(sid),'Assets/ConfigBin/cfgobj_skillobj.json')
   else:required.add(skills[sid]['action'])
  for action in required:
   for direction in SOURCE_DIRECTIONS:
    matches=[a for a in appearances if a['action']==action and a['direction']==direction]
    if len(matches)!=1:add('SUITE_REQUIRED_ACTION','Missing/nonunique required two-source action: '+str((uid,action,direction)))
  exception=legacy.get(uid)
  if exception:
   if exception.get('fingerprint')!=fingerprint(unit) or not exception.get('evidence') or not exception.get('remediationBatch'):add('SUITE_LEGACY_DRIFT','Legacy appearance/fire-point contract changed: '+str(uid))
   declared=exception.get('assets',[]);expected={f"Assets/Art/frameAnimation/{a['resource']}{suffix}" for a in appearances for suffix in ['.png','.json']}
   if len({a['path'] for a in declared})!=len(declared) or {a['path'] for a in declared}!=expected:add('SUITE_LEGACY_ASSETS','Legacy assets no longer exactly cover declared appearances: '+str(uid))
   for asset in declared:
    path=asset['path'];legacy_paths.add(path)
    if not (root/path).is_file() or records.get(path,{}).get('guid')!=asset['guid'] or sha(root/path)!=asset['sha256']:add('SUITE_LEGACY_ASSETS','Retained legacy identity/content drift: '+path,path)
   add('SUITE_LEGACY_DIRECTIONS','Exact retained historical contract; not a new two-source complete delivery: '+str(uid),severity='legacy')
  elif any(a['direction'] not in SOURCE_DIRECTIONS for a in appearances):add('SUITE_SOURCE_DIRECTIONS','New/current unit actions must use right-up/right-down only: '+str(uid))
  for a in appearances:
   if not exception and not a['resource'].endswith('_'+('UpRight' if a['direction']==2 else 'DownRight')):add('SUITE_DIRECTION_NAME','Source resource suffix differs from configured direction: '+str(uid))
   if a.get('loop')!=(a['action'] in [1,4]):add('SUITE_ACTION_LOOP','Movement/idle loop; attack/death do not: '+str((uid,a['action'])))
   for suffix in ['.png','.json']:
    path=f"Assets/Art/frameAnimation/{a['resource']}{suffix}";active_paths.add(path)
    if not (root/path).is_file():add('SUITE_ACTION_ASSET','Configured action missing: '+path,path)
  if not exception and any(f['direction'] not in SOURCE_DIRECTIONS for f in unit.get('firePoints',[])):add('SUITE_FIRE_DIRECTION','Fire points must follow two source directions: '+str(uid))
 for uid in legacy:
  if uid not in bound_units:add('SUITE_STALE_EXCEPTION','Legacy unit exception no longer matches a bound unit: '+str(uid))
 candidates=contract.get('candidateAssets',[]);candidate_paths=set()
 for item in candidates:
  path=item['path'];cid=item['cardId'];direction=item['direction'];action=item['action'];artid=item['artId']
  if cid not in cardmap or cardmap[cid].get('artId')!=artid or direction not in SOURCE_DIRECTIONS or action not in ACTION_NAMES or item.get('state') not in ['candidate','active','retained'] or not item.get('sourceEvidence'):raise ValueError('Invalid candidate owner/action/state/provenance')
  expected=f"Assets/Art/frameAnimation/cardBattle/{cid}/{artid}-{ACTION_NAMES[action]}_{'UpRight' if direction==2 else 'DownRight'}{Path(path).suffix}"
  if path!=expected or Path(path).suffix not in ['.png','.json'] or path in candidate_paths or path in legacy_paths:raise ValueError('Unsafe/colliding candidate identity: '+path)
  candidate_paths.add(path)
  if path not in records or records[path].get('guid')!=item['guid'] or sha(root/path)!=item['sha256']:add('SUITE_CANDIDATE_ASSET','Candidate identity/content missing/drift: '+path,path)
  if item['state']=='candidate' and path in active_paths:add('SUITE_CANDIDATE_ACTIVE','Candidate was activated without lifecycle transition: '+path,path)
  if item['state']=='active' and path not in active_paths:add('SUITE_ACTIVE_UNUSED','Active candidate has no runtime configuration consumer: '+path,path)
  if item['state']=='retained' and path in active_paths:add('SUITE_RETAINED_ACTIVE','Retained old version still active: '+path,path)
 for path in records:
  if path.startswith('Assets/Art/frameAnimation/cardBattle/') and path.endswith(('.png','.json')) and path not in active_paths|candidate_paths|legacy_paths:add('SUITE_UNBOUND_ANIMATION','New unit action must be declared by active config or lifecycle/provenance: '+path,path)
 return out
