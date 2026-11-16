"""Exact approved common-source cleanup. Validates metadata/files; never deletes or packs."""
from pathlib import Path
import hashlib,json,re,struct
REVISION='COMMON-CLEANUP/R3.1'
ROOT='Assets/Art/atlasSource/default/common'
ATLAS='Assets/Art/atlas/default/common.png'
NAMES={'bg_black1','bg_gray','btn_common1','btn_green','btn_orange'}
APPROVED_DUPLICATE_REVIEW_SHA256='2e865a8dcc3e4b35738263f89c289a4eadf8f61ef3f1b4cc210736519baf09ea'
APPROVED_SCOPE_SHA256='ea40b529d64d62834b8ea0df60ff903899b0d0a8d6b61ac5d2c96b51c3e1b6ab'
KEEP_RECORD_HASHES={'705d852c4689ff44b98c9e74ec631d5d': '7fa85d02b633c520abc3943751c077e45fd0dd9d003ac99a56aea528fe36f17c', 'a8d59bbf88d8a4f499073f78431d0090': '671a755dc340ff9d38492bf62b6b7cc8e372d159f423c929a34022b830444654', '800adcf5912247545bfb8db0a10ba451': 'fb75b43623230269fe274093fd73ebd17b8d7c6870cd43c07c36fcde5089567b', '6985bdb538837ba4786c06449039016b': '462e8d38b68a2c3a51297ac81b36f28b3edec7d91dfb103beec56e19d65e0dd4', '5c1f20b1f3b473c438db850f29a28d1c': 'c453ad2286344f1e039b1e19f1f9789a0363c41637b4ec8969a04c053c5fa099'}
ATLAS_RECORD_HASH='2f5e79d27760fca21c76d753738031bcb4adf26f5d62a1f9e040ce92715f20e5'

def canonical_hash(value):return hashlib.sha256(json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode('utf8')).hexdigest()
def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def remaining_duplicate_reviews(project,manifest,groups,records):
 scope=manifest.get('sourceCleanup');out={}
 if not scope or scope['phase']=='prepared':return out
 removed={a['path']:a for a in scope['deleteAssets']}
 for group in groups:
  if canonical_hash(group)!=APPROVED_DUPLICATE_REVIEW_SHA256:continue
  deleted=[removed[p] for p in group['paths'] if p in removed];kept=[p for p in group['paths'] if p not in removed]
  if len(deleted)!=1 or len(kept)!=2 or any(p not in records or not (Path(project)/p).is_file() or digest(Path(project)/p)!=deleted[0]['sha256'] for p in kept):continue
  out[tuple(sorted(kept))]=dict(id=group['id'],sourcePaths=group['paths'])
 return out
def frozen(scope):
 return dict(sourceDirectory=scope['sourceDirectory'],deleteAssets=[{k:a[k] for k in ['path','guid','sha256','metaSha256','originalManifestRecord']} for a in sorted(scope['deleteAssets'],key=lambda a:a['path'])],keptAssets=[{k:a[k] for k in ['path','guid','sha256','metaSha256','sourceContract','atlasSpriteContract','expectedAfter']} for a in sorted(scope['keptAssets'],key=lambda a:a['path'])],atlasContract=scope['atlasContract'])
def sprite_metadata(text):
 sprites={}
 for block in re.split(r'^    - serializedVersion: \d+\s*$',text,flags=re.M)[1:]:
  name=re.search(r'^      name: (.*)$',block,re.M);sid=re.search(r'^      spriteID: ([a-f0-9]{32})\s*$',block,re.M);iid=re.search(r'^      internalID: (-?\d+)\s*$',block,re.M)
  if not name or not sid or not iid:continue
  key=name[1].strip().strip("'").replace("''", "'")
  if key in sprites:raise ValueError('Duplicate atlas Sprite name')
  rect=re.search(r'^      rect:\s*\n(?:        serializedVersion: \d+\s*\n)?((?:        (?:x|y|width|height): [^\n]+\n){4})',block,re.M)
  pair=lambda label:{k:float(v) for k,v in re.findall(r'([xyzw]):\s*(-?[\d.eE+]+)',re.search(r'^      '+label+r': \{([^}]+)\}',block,re.M)[1])}
  sprites[key]=dict(name=key,fileID=iid[1],spriteID=sid[1],rect={k:float(v) for k,v in re.findall(r'(x|y|width|height):\s*(-?[\d.eE+]+)',rect[1])} if rect else {},border=pair('border'),pivot=pair('pivot'))
 table=re.search(r'^    nameFileIdTable:\s*\n((?:      [^\n]*\n)*)',text,re.M);ids={}
 if table:
  for name,iid in re.findall(r'^      (.+): (-?\d+)\s*$',table[1],re.M):ids[name.strip().strip("'")]=iid
 if set(ids)!=set(sprites) or any(ids[name]!=s['fileID'] for name,s in sprites.items()):raise ValueError('Atlas Sprite rect/name-to-fileID registration mismatch')
 return sprites

def validate_metadata(manifest):
 scope=manifest.get('sourceCleanup')
 if scope is None:return None
 if scope.get('revision')!=REVISION or scope.get('phase') not in ['prepared','sources-pruned','complete'] or not scope.get('batchId') or not scope.get('approvalEvidence') or not re.fullmatch('[a-f0-9]{64}',scope.get('planSha256','')):raise ValueError('Missing exact common cleanup approval/phase/plan identity')
 if scope.get('sourceDirectory')!=ROOT or len(scope.get('deleteAssets',[]))!=227 or len(scope.get('keptAssets',[]))!=5:raise ValueError('Common cleanup scope must be exactly approved 227 + 5')
 if canonical_hash(frozen(scope))!=APPROVED_SCOPE_SHA256:raise ValueError('Approved common source identity/content/record set changed')
 keep=scope['keptAssets'];removed=scope['deleteAssets'];all_items=keep+removed;guids=[a['guid'] for a in all_items];paths=[a['path'] for a in all_items]
 if len(set(guids))!=232 or len(set(paths))!=232 or {Path(a['path']).stem for a in keep}!=NAMES:raise ValueError('Duplicate identity or protected common source omitted')
 for a in all_items:
  if not re.fullmatch(re.escape(ROOT)+r'/[^/]+\.png',a['path']) or not re.fullmatch('[a-f0-9]{32}',a['guid']) or any(not re.fullmatch('[a-f0-9]{64}',a.get(k,'')) for k in ['sha256','metaSha256']):raise ValueError('Invalid exact common source')
 rows={r['guid']:r for r in manifest['records']};active_paths={r[k]['path'] for r in manifest['records'] for k in ['current','target']}
 for a in keep:
  row=rows.get(a['guid'])
  if not row or canonical_hash(row)!=KEEP_RECORD_HASHES.get(a['guid']):raise ValueError('Protected common record/history changed')
 for a in removed:
  old=a['originalManifestRecord']
  if old.get('guid')!=a['guid'] or old.get('state')!='current' or old['current'].get('path')!=a['path'] or old['current'].get('entries') or old['target'].get('path')!=a['path'] or old['target'].get('group') is not None:raise ValueError('Only exact nonruntime common source records may be archived')
  if scope['phase']=='prepared':
   if rows.get(a['guid'])!=old:raise ValueError('Cleanup prepared state differs from original current records')
  elif a['guid'] in rows or a['path'] in active_paths:raise ValueError('Deleted common source remains active or its path was reused')
 atlas=scope['atlasContract'];row=rows.get(atlas['guid'])
 if atlas['path']!=ATLAS or not row or canonical_hash(row)!=ATLAS_RECORD_HASH:raise ValueError('Common atlas registration/record changed; this cleanup does not migrate Group')
 if any(o['guid'] in guids or o['guid']==atlas['guid'] for o in manifest.get('packingOverrides',[])):raise ValueError('Cleanup cannot install or replace a packing override')
 after=scope.get('afterAtlas')
 if scope['phase']=='complete':
  if not isinstance(after,dict) or after.get('path')!=ATLAS or after.get('guid')!=atlas['guid'] or any(not re.fullmatch('[a-f0-9]{64}',after.get(k,'')) for k in ['sha256','metaSha256']) or any(not isinstance(after.get(k),int) or after[k]<=0 for k in ['width','height']):raise ValueError('Complete cleanup needs frozen repacked atlas identity/dimensions')
  sprites=after.get('sprites',[])
  if len(sprites)!=5 or {s['name'] for s in sprites}!=NAMES:raise ValueError('Repacked atlas must contain exactly five protected Sprites')
  for s in sprites:
   expected=next(a['expectedAfter'] for a in keep if a['name']==s['name'])
   if str(s['fileID'])!=str(expected['internalID']) or s['spriteID']!=expected['spriteID'] or s['border']!=expected['border'] or s['pivot']!=expected['pivot'] or s['pixelsPerUnit']!=100 or {k:s['rect'][k] for k in ['width','height']}!=expected['rectSize']:raise ValueError('Protected Sprite ID/geometry/PPU contract changed')
 elif after is not None:raise ValueError('Atlas output cannot be accepted before complete phase')
 return scope

def audit(project,manifest,records,entries):
 scope=validate_metadata(manifest)
 if scope is None:return []
 root=Path(project);out=[];phase=scope['phase'];removed={a['guid']:a for a in scope['deleteAssets']};keep={a['guid']:a for a in scope['keptAssets']};all_items={**removed,**keep}
 def add(code,path,message):out.append(dict(code=code,path=path,guid=records.get(path,{}).get('guid'),severity='error',message=message,relatedPaths=[ROOT]))
 expected=all_items if phase=='prepared' else keep;wanted={a['path'] for a in expected.values()};actual={p.relative_to(root).as_posix() for p in (root/ROOT).rglob('*') if p.is_file() and p.suffix!='.meta'}
 if actual!=wanted:add('PACK_SOURCE_CLEANUP_SET',ROOT,'Current source folder differs from exact phase set')
 for guid,a in expected.items():
  f=root/a['path'];meta=Path(str(f)+'.meta')
  if not f.is_file() or not meta.is_file() or records.get(a['path'],{}).get('guid')!=guid or digest(f)!=a['sha256'] or digest(meta)!=a['metaSha256']:add('PACK_SOURCE_CLEANUP_IDENTITY',a['path'],'Protected source GUID/bytes/importer changed')
 if any(e['guid'] in all_items for e in entries):add('PACK_SOURCE_CLEANUP_AA',ROOT,'Atlas source registered for runtime')
 if phase!='prepared':
  for guid,a in removed.items():
   if (root/a['path']).exists() or Path(str(root/a['path'])+'.meta').exists() or any(v.get('guid')==guid for v in records.values()):add('PACK_SOURCE_CLEANUP_RESURRECTED',a['path'],'Deleted source path/meta/GUID was reused')
 atlas=scope['atlasContract'];f=root/ATLAS;meta=Path(str(f)+'.meta');approved=scope['afterAtlas'] if phase=='complete' else atlas
 if not f.is_file() or not meta.is_file() or records.get(ATLAS,{}).get('guid')!=atlas['guid'] or digest(f)!=approved['sha256'] or digest(meta)!=approved['metaSha256']:add('PACK_SOURCE_CLEANUP_ATLAS',ATLAS,'Atlas differs from exact before/output snapshot for this phase')
 else:
  try:
   text=meta.read_text('utf-8-sig');sprites=sprite_metadata(text);ppu=re.search(r'^  spritePixelsToUnits: ([^\s]+)',text,re.M);width,height=struct.unpack('>II',f.read_bytes()[16:24])
   if not ppu or float(ppu[1])!=100:raise ValueError('Atlas PPU must remain 100')
   if phase=='complete' and (set(sprites)!=NAMES or (width,height)!=(approved['width'],approved['height'])):raise ValueError('Final five Sprite set or image dimensions changed')
   for a in keep.values():
    old=a['expectedAfter'];s=sprites.get(a['name'])
    if not s or s['fileID']!=str(old['internalID']) or s['spriteID']!=old['spriteID'] or s['border']!=old['border'] or s['pivot']!=old['pivot'] or {k:s['rect'][k] for k in ['width','height']}!=old['rectSize']:raise ValueError('Kept atlas Sprite identity/geometry changed')
    if phase=='complete':
     captured=next(x for x in approved['sprites'] if x['name']==a['name'])
     if any(captured.get(k)!=s[k] for k in ['name','fileID','spriteID','rect','border','pivot']):raise ValueError('Captured output Sprite differs from importer')
  except (ValueError,KeyError,AttributeError,struct.error) as e:add('PACK_SOURCE_CLEANUP_SPRITES',ATLAS,str(e))
 row=next(r for r in manifest['records'] if r['guid']==atlas['guid']);expected_entries=row['current']['entries'];actual_entries=[{k:e[k] for k in ['group','address','labels']} for e in entries if e['guid']==atlas['guid']]
 if actual_entries!=expected_entries:add('PACK_SOURCE_CLEANUP_ATLAS_AA',ATLAS,'Cleanup changed common atlas Group/address/labels')
 pattern=re.compile('|'.join(map(re.escape,removed)));reference=re.compile(r'fileID:\s*(-?\d+)\s*,\s*guid:\s*'+atlas['guid']);allowed={str(a['expectedAfter']['internalID']) for a in keep.values()}|{'0','2800000'}
 for path,asset in records.items():
  if '/Editor/' in path or Path(path).suffix not in ['.cs','.json','.prefab','.unity','.mat','.asset']:continue
  text=(root/path).read_text('utf-8-sig',errors='replace')
  match=pattern.search(text)
  if match:add('PACK_SOURCE_CLEANUP_REFERENCE',path,'Runtime source references approved deleted source GUID: '+match[0])
  if any(iid not in allowed for iid in reference.findall(text)):add('PACK_SOURCE_CLEANUP_SPRITE_REFERENCE',path,'Runtime source references a removed common atlas Sprite fileID')
 return out

def preflight(project,manifest,records,entries,plan_sha256):
 scope=validate_metadata(manifest)
 if scope is None or scope['phase']!='prepared' or scope['planSha256']!=plan_sha256:raise ValueError('Exact prepared cleanup plan identity required')
 errors=audit(project,manifest,records,entries)
 if errors:raise ValueError('Common cleanup preflight failed: '+json.dumps(errors,ensure_ascii=False))
 return dict(planReady=True,executed=False,deleteCount=227,keepCount=5)
