"""Exact approved retirement of the five historical skill images. Read-only validator."""
from pathlib import Path
import hashlib,json,re

ALLOWED_RETIRED = {'678639ec4bc436d468347081f8200316': {'path': 'Assets/Art/atlas/default/skillIcons.png', 'sha256': 'c6a59ba00c2178d0b22fc72eab989e1b3d08c2ca2dae6c64deb4474f6376fd95', 'metaSha256': 'd5466a4d0201f62f8cacd0f18bb7b0dd7705fa6dd03163db71c73e80061e07b0', 'originalRecordSha256': '4aa149b11aab8d6565cccdfd9164f6d6548452d17aa4a70971af129176ebf063'}, '51cbc911c4925c018fa3764f7f63aca5': {'path': 'Assets/Art/atlasSource/default/cardCultivation/skill-v11-active.png', 'sha256': '70f8a8cdc0d326fdadb7d9c060f3456de68dbf5a74c6a951735ff20e9a083000', 'metaSha256': 'a607b07af51a66ee6f5842811d9128cb53040d6443e11992f254fe58ffd9c5f4', 'originalRecordSha256': 'ef0a708c84f36f1b0ea83d7b11686f40100c1837c4229f2fb52c7fc3e955a6bb'}, '813dea84b76c5b7682aa037c928ff4aa': {'path': 'Assets/Art/atlasSource/default/cardCultivation/skill-v11-basic.png', 'sha256': '72556b728ea5e9b817e9ca915e8b12ea982318891fea65e73c71283d8f22797b', 'metaSha256': 'bc0319e8b2f872898c0d5f6f674f7b87a18717e6e62248d9b4af3a2acd10df57', 'originalRecordSha256': '61eccc0ea22daf48863124da8607cc4fde99c2f26d81fb456eb3d8decbb28540'}, '2816aef02a225acca42b566585ec035c': {'path': 'Assets/Art/atlasSource/default/cardCultivation/skill-v11-passive.png', 'sha256': 'bbbb23f8a7879473b91e5cf06a2cc52da1b5e3c024b8180d3b391063501df560', 'metaSha256': '6d44e117b4734fd11b0cdca1d9eef4e1c7890cc6a37ea9ee95b1b29530c89892', 'originalRecordSha256': 'a91696c0970570abf1c0f786899b210ed3fefaae7c52fd174a64dfc3060f72f5'}, '388931c2b12c5616b8ccb0b12f6d40a5': {'path': 'Assets/Art/atlasSource/default/cardCultivation/skill-v11-ultimate.png', 'sha256': '264f5322addbdf5b783483b155818d1328cdcc7615a385fe3c2e90cb47672ca7', 'metaSha256': 'bcf945989165778b4c29b88c146edd93f6096c8ff86a061693dd575d7c071b55', 'originalRecordSha256': 'b71924918d88bdee0f32704728fefebb089dc142078de632f872b58099ba376c'}}

def canonical_hash(row):
 return hashlib.sha256(json.dumps(row,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode('utf8')).hexdigest()

def validate_metadata(manifest,policy):
 tombstones=manifest.get('deletionTombstones',[])
 if not isinstance(tombstones,list):raise ValueError('Deletion tombstones must be a list')
 active={r['guid']:r for r in manifest.get('records',[])};retired={r.get('guid'):r for r in policy.get('retiredRuntimeAssets',[])};seen=set()
 for item in tombstones:
  guid=item.get('guid');allowed=ALLOWED_RETIRED.get(guid)
  if not allowed or guid in seen:raise ValueError('Unknown or duplicate exact retirement identity')
  seen.add(guid)
  if any(item.get(k)!=allowed[k] for k in ['path','sha256','metaSha256']):raise ValueError('Deleted resource identity/hash differs from exact approved list')
  if not isinstance(item.get('approvalEvidence'),str) or not item['approvalEvidence'].strip():raise ValueError('Deleted resource needs explicit approval evidence')
  original=item.get('originalRecord')
  if not isinstance(original,dict) or original.get('guid')!=guid or original.get('state')!='retired' or original.get('current',{}).get('path')!=allowed['path'] or original.get('current',{}).get('entries'):
   raise ValueError('Only an exact previously inactive retired record may become a tombstone')
  if canonical_hash(original)!=allowed['originalRecordSha256']:raise ValueError('Original retired record evidence changed')
  if guid in active:raise ValueError('Deleted GUID cannot remain in active Manifest records')
  if any(r.get('current',{}).get('path')==allowed['path'] or r.get('target',{}).get('path')==allowed['path'] for r in active.values()):raise ValueError('Deleted path was reused by an active Manifest record')
  if sum(r.get('guid')==guid for r in policy.get('retiredRuntimeAssets',[]))!=1:raise ValueError('Policy deleted identity must be unique')
  retained=retired.get(guid)
  if not retained or retained.get('path')!=allowed['path'] or retained.get('state')!='deleted' or not retained.get('reason'):raise ValueError('Policy retirement reason/state must agree with deletion tombstone')
 return tombstones

def audit_deleted_assets(project,manifest,records,entries):
 out=[];root=Path(project)
 for item in manifest.get('deletionTombstones',[]):
  guid=item['guid'];path=item['path'];reasons=[]
  if (root/path).exists() or Path(str(root/path)+'.meta').exists():reasons.append('deleted path or meta reappeared')
  reused=[p for p,r in records.items() if r.get('guid')==guid]
  if reused:reasons.append('deleted GUID reused: '+', '.join(reused))
  if any(e.get('guid')==guid for e in entries):reasons.append('deleted GUID re-registered in Addressables')
  if reasons:out.append(dict(code='PACK_DELETED_RESURRECTED',path=path,guid=guid,severity='error',message='; '.join(reasons)))
  for consumer,row in records.items():
   if not consumer.startswith('Assets/') or consumer.startswith('Assets/Editor/') or Path(consumer).suffix not in ['.prefab','.unity','.mat','.asset','.json','.cs']:continue
   file=root/consumer
   if not file.is_file():continue
   text=file.read_text('utf-8-sig',errors='replace')
   if guid in text:out.append(dict(code='PACK_DELETED_REFERENCE',path=consumer,guid=row.get('guid'),severity='error',message='Runtime source still references deleted GUID: '+guid,relatedPaths=[path]))
 return out
