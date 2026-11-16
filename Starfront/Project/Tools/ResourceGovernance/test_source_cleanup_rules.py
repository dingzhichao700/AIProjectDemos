import copy,hashlib,json,struct,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import source_cleanup_rules as sc

class SourceCleanupTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name);self.records={};self.rows=[];keep=[];delete=[];self.geometry={}
  for i,name in enumerate(sorted(sc.NAMES)+[f'obsolete_{n:03}' for n in range(227)]):
   path=sc.ROOT+'/'+name+'.png';guid=hashlib.md5(path.encode()).hexdigest();f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b'image:'+name.encode());meta=Path(str(f)+'.meta');meta.write_text('guid: '+guid+'\n','utf8');row=dict(guid=guid,state='current',batch='old',basis='approved old source',current=dict(path=path,entries=[]),target=dict(path=path,group=None,bundleKey=None),targetHistory=[{'target':{'path':'old/'+name}}]);self.rows.append(row);self.records[path]={'guid':guid};item=dict(name=name,path=path,guid=guid,sha256=sc.digest(f),metaSha256=sc.digest(meta))
   if name in sc.NAMES:
    shape=dict(name=name,internalID=str(i+100),spriteID=hashlib.md5(name.encode()).hexdigest(),border=dict(x=0,y=0,z=0,w=0),pivot=dict(x=.5,y=.5),pixelsPerUnit=100,rectSize=dict(width=32,height=16));self.geometry[name]=shape;item.update(sourceContract=dict(name=name,dimensions=dict(width=32,height=16)),atlasSpriteContract=copy.deepcopy(shape),expectedAfter=copy.deepcopy(shape));keep.append(item)
   else:item['originalManifestRecord']=copy.deepcopy(row);delete.append(item)
  self.atlasGuid='a'*32;self.write_atlas(sorted(sc.NAMES)+[f'extra_{n:03}' for n in range(219)],512,512);f=self.root/sc.ATLAS;meta=Path(str(f)+'.meta');atlas=dict(path=sc.ATLAS,guid=self.atlasGuid,sha256=sc.digest(f),metaSha256=sc.digest(meta));row=dict(guid=self.atlasGuid,state='current',current=dict(path=sc.ATLAS,entries=[dict(group='Default Local Group',address=sc.ATLAS,labels=['main-content'])]),target=dict(path=sc.ATLAS,group='uiCommon',bundleKey='ui_common'));self.rows.append(row);self.records[sc.ATLAS]={'guid':self.atlasGuid};self.entries=[dict(guid=self.atlasGuid,group='Default Local Group',address=sc.ATLAS,labels=['main-content'])];self.manifest=dict(records=self.rows,sourceCleanup=dict(revision=sc.REVISION,batchId='COMMON-01',approvalEvidence='explicit approved exact list',planSha256='a'*64,phase='prepared',sourceDirectory=sc.ROOT,deleteAssets=delete,keptAssets=keep,atlasContract=atlas),deletionTombstones=[{'untouched':'prior-five'}],groupConsolidation={'untouched':'old-nine-118'})
  self.scope=self.manifest['sourceCleanup']
  for name,value in [('APPROVED_SCOPE_SHA256',sc.canonical_hash(sc.frozen(self.scope))),('KEEP_RECORD_HASHES',{a['guid']:sc.canonical_hash(next(r for r in self.rows if r['guid']==a['guid'])) for a in keep}),('ATLAS_RECORD_HASH',sc.canonical_hash(row))]:
   p=patch.object(sc,name,value);p.start();self.addCleanup(p.stop)
 def write_atlas(self,names,width,height):
  f=self.root/sc.ATLAS;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b'\x89PNG\r\n\x1a\n'+b'\x00\x00\x00\rIHDR'+struct.pack('>II',width,height)+b'test-pixels');blocks=[];table=[]
  for i,name in enumerate(names):
   g=self.geometry.get(name,dict(internalID=str(9000+i),spriteID=hashlib.md5(name.encode()).hexdigest()));blocks.append('    - serializedVersion: 2\n      name: '+name+'\n      rect:\n        serializedVersion: 2\n        x: 0\n        y: 0\n        width: 32\n        height: 16\n      border: {x: 0, y: 0, z: 0, w: 0}\n      pivot: {x: 0.5, y: 0.5}\n      spriteID: '+g['spriteID']+'\n      internalID: '+g['internalID']+'\n');table.append('      '+name+': '+g['internalID']+'\n')
  Path(str(f)+'.meta').write_text('guid: '+self.atlasGuid+'\nTextureImporter:\n  spritePixelsToUnits: 100\n  spriteSheet:\n    sprites:\n'+''.join(blocks)+'    nameFileIdTable:\n'+''.join(table)+'  userData: \n','utf8')
 def prune(self):
  guids={a['guid'] for a in self.scope['deleteAssets']};self.manifest['records']=[r for r in self.rows if r['guid'] not in guids]
  for a in self.scope['deleteAssets']:
   f=self.root/a['path'];f.unlink();Path(str(f)+'.meta').unlink();self.records.pop(a['path'])
  self.scope['phase']='sources-pruned'
 def finish(self):
  self.prune();self.write_atlas(sorted(sc.NAMES),256,128);self.scope['phase']='complete';meta=Path(str(self.root/sc.ATLAS)+'.meta');sprites=list(sc.sprite_metadata(meta.read_text('utf8')).values())
  for sprite in sprites:sprite['pixelsPerUnit']=100
  self.scope['afterAtlas']=dict(path=sc.ATLAS,guid=self.atlasGuid,sha256=sc.digest(self.root/sc.ATLAS),metaSha256=sc.digest(meta),width=256,height=128,sprites=sprites)
 def audit(self):return sc.audit(self.root,self.manifest,self.records,self.entries)
 def reject_metadata(self):self.assertRaises(ValueError,sc.validate_metadata,self.manifest)
 def test_prepared_exact_227_deletion_preflight_is_readonly(self):
  result=sc.preflight(self.root,self.manifest,self.records,self.entries,'a'*64);self.assertEqual(result['deleteCount'],227);self.assertFalse(result['executed']);self.assertTrue((self.root/self.scope['deleteAssets'][0]['path']).is_file())
 def test_sources_pruned_prepack_accepts_exact_old_224_atlas_and_five_sources(self):self.prune();self.assertEqual(self.audit(),[])
 def test_complete_accepts_exact_five_sprites_and_stable_registration(self):self.finish();self.assertEqual(self.audit(),[])
 def test_old_five_tombstones_and_nine_group_history_are_not_mutated(self):
  before={k:copy.deepcopy(self.manifest[k]) for k in ['deletionTombstones','groupConsolidation']};self.finish();self.assertEqual(self.audit(),[]);self.assertEqual({k:self.manifest[k] for k in before},before)
 def test_changing_count_to_make_gate_pass_is_rejected(self):self.scope['deleteAssets'].pop();self.reject_metadata()
 def test_same_count_different_delete_guid_is_rejected(self):self.scope['deleteAssets'][0]['guid']='f'*32;self.reject_metadata()
 def test_same_count_different_original_record_is_rejected(self):self.scope['deleteAssets'][0]['originalManifestRecord']['basis']='rewritten';self.reject_metadata()
 def test_one_of_five_cannot_be_swapped_for_another_source(self):self.scope['keptAssets'][0]['path']=self.scope['deleteAssets'][0]['path'];self.reject_metadata()
 def test_pruned_phase_requires_removed_records_archived_not_left_active(self):
  self.prune();self.manifest['records'].append(self.scope['deleteAssets'][0]['originalManifestRecord']);self.reject_metadata()
 def test_protected_keep_record_and_atlas_group_cannot_change(self):
  self.rows[-1]['current']['entries'][0]['group']='default';self.reject_metadata()
 def test_source_meta_or_pixels_change_blocks_packing(self):
  self.prune();f=self.root/self.scope['keptAssets'][0]['path'];f.write_bytes(b'changed');self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_IDENTITY' for e in self.audit()))
 def test_new_unapproved_source_blocks_packing(self):
  self.prune();(self.root/sc.ROOT/'unexpected.png').write_bytes(b'new');self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_SET' for e in self.audit()))
 def test_orphan_deleted_meta_resurrection_is_rejected(self):
  self.prune();Path(str(self.root/self.scope['deleteAssets'][0]['path'])+'.meta').write_text('orphan');self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_RESURRECTED' for e in self.audit()))
 def test_deleted_guid_cannot_reappear_under_another_path(self):
  self.prune();self.records['Assets/Art/unpack/default/alias.png']={'guid':self.scope['deleteAssets'][0]['guid']};self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_RESURRECTED' for e in self.audit()))
 def test_pruned_before_atlas_cannot_be_silently_modified(self):
  self.prune();(self.root/sc.ATLAS).write_bytes(b'changed');errors=self.audit();self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_ATLAS' and sc.ROOT in e['relatedPaths'] for e in errors))
 def test_final_atlas_requires_frozen_output_before_acceptance(self):self.prune();self.scope['phase']='complete';self.reject_metadata()
 def test_final_extra_sprite_is_rejected_even_if_output_hash_is_refrozen(self):
  self.finish();self.write_atlas(sorted(sc.NAMES)+['extra'],256,128);self.scope['afterAtlas']['metaSha256']=sc.digest(Path(str(self.root/sc.ATLAS)+'.meta'));self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_SPRITES' for e in self.audit()))
 def test_sprite_fileid_change_is_rejected(self):self.finish();self.scope['afterAtlas']['sprites'][0]['fileID']='999999';self.reject_metadata()
 def test_sprite_guid_change_is_rejected(self):self.finish();self.scope['afterAtlas']['sprites'][0]['spriteID']='f'*32;self.reject_metadata()
 def test_sprite_border_pivot_ppu_or_size_change_is_rejected(self):
  self.finish();self.scope['afterAtlas']['sprites'][0]['pixelsPerUnit']=101;self.reject_metadata()
 def test_runtime_deleted_source_reference_is_rejected(self):
  self.prune();path='Assets/Prefab/default/gm/Bad.prefab';f=self.root/path;f.parent.mkdir(parents=True);f.write_text('guid: '+self.scope['deleteAssets'][0]['guid']);self.records[path]={'guid':'f'*32};self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_REFERENCE' for e in self.audit()))
 def test_runtime_removed_common_sprite_fileid_is_rejected(self):
  self.prune();path='Assets/Prefab/default/gm/Bad.prefab';f=self.root/path;f.parent.mkdir(parents=True);f.write_text('m_Sprite: {fileID: 999999, guid: '+self.atlasGuid+', type: 3}');self.records[path]={'guid':'f'*32};self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_SPRITE_REFERENCE' for e in self.audit()))
 def test_valid_existing_gm_sprite_reference_remains_legal(self):
  self.finish();path='Assets/Prefab/default/gm/Good.prefab';f=self.root/path;f.parent.mkdir(parents=True);f.write_text('m_Sprite: {fileID: '+self.geometry['bg_black1']['internalID']+', guid: '+self.atlasGuid+', type: 3}');self.records[path]={'guid':'f'*32};self.assertEqual(self.audit(),[])
 def test_sources_cannot_be_registered_as_aa_runtime_inputs(self):
  self.prune();self.entries.append(dict(guid=self.scope['keptAssets'][0]['guid'],group='default',address=self.scope['keptAssets'][0]['path'],labels=[]));self.assertTrue(any(e['code']=='PACK_SOURCE_CLEANUP_AA' for e in self.audit()))
 def review_fixture(self):
  deleted=self.scope['deleteAssets'][0];payload=(self.root/deleted['path']).read_bytes();paths=['Assets/Art/atlasSource/default/gm/progressBar_bg1.png','Assets/Art/atlasSource/title/title/progressBar_bg1.png']
  for path in paths:
   f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(payload);self.records[path]={'guid':hashlib.md5(path.encode()).hexdigest()}
  review=dict(id='DUP-014',paths=[deleted['path']]+paths,owner='old owner',reviewBatch='B1',basis='original pending byte-identity review');p=patch.object(sc,'APPROVED_DUPLICATE_REVIEW_SHA256',sc.canonical_hash(review));p.start();self.addCleanup(p.stop);return review,paths
 def test_existing_three_copy_review_maps_to_exact_two_survivors(self):
  review,paths=self.review_fixture();self.prune();mapped=sc.remaining_duplicate_reviews(self.root,self.manifest,[review],self.records);self.assertEqual(mapped[tuple(sorted(paths))]['id'],'DUP-014');self.assertEqual(mapped[tuple(sorted(paths))]['sourcePaths'],review['paths'])
 def test_survivor_review_never_admits_third_copy_or_changed_original_review(self):
  review,paths=self.review_fixture();self.prune();mapped=sc.remaining_duplicate_reviews(self.root,self.manifest,[review],self.records);self.assertNotIn(tuple(sorted(paths+['Assets/Art/third.png'])),mapped);review['id']='unapproved';self.assertFalse(sc.remaining_duplicate_reviews(self.root,self.manifest,[review],self.records))
 def test_survivor_review_rejects_changed_content(self):
  review,paths=self.review_fixture();self.prune()
  for path in paths:(self.root/path).write_bytes(b'new equal pixels')
  self.assertFalse(sc.remaining_duplicate_reviews(self.root,self.manifest,[review],self.records))

if __name__=='__main__':unittest.main()
