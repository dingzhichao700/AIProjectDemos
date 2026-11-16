import copy,json,tempfile,unittest,uuid
from pathlib import Path
import pack_rules as pr
import resource_governance as rg

class PackTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);(self.root/'Assets').mkdir()
  self.policy={'packGovernance':{'schemaVersion':1,'manifest':pr.MANIFEST},'protectedVendorRoots':['Assets/TextMesh Pro/']}
  self.data={'schemaVersion':1,'approvalId':'RESOURCE-PACK/R1','migrationEnabled':True,'groupModes':pr.MODES.copy(),'groupModules':{'uiMain':['mainView'],'uiCommon':['controls'],'uiDebug':['gm'],'battleCommon':['hud','projectile']},'currentGroupModes':{'Default Local Group':'PackTogether'},'records':[]}
  for name,rows in [('card',[dict(id=2101,content={'$type':'UnitCardContent','unitId':2101})]),('cardart',[dict(id=2101)]),('battlescene',[dict(id=1001)])]:self.write('Assets/ConfigBin/cfgobj_'+name+'obj.json',rows)
 def tearDown(self):self.temp.cleanup()
 def write(self,path,obj):
  f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_text(json.dumps(obj),encoding='utf-8');return f
 def asset(self,path,text='fixture'):
  f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_text(text,encoding='utf-8');g=uuid.uuid4().hex;Path(str(f)+'.meta').write_text('guid: '+g+'\n');return g
 def record(self,path='Assets/Art/unpack/default/card/a.png',group='Default Local Group',labels=None):
  g=self.asset(path);row=dict(guid=g,state='current',batch='P4',basis='isolated fixture approval',migratable=True,current=dict(path=path,entries=[dict(group=group,address=path,labels=labels or ['main-content'])]),target=dict(path='Assets/Art/unpack/cardDisplay/cardFaces/2101.png',group='cardDisplay',bundleKey='face_2101'))
  self.data['records'].append(row);return row
 def entries(self,row):return [dict(copy.deepcopy(e),guid=row['guid']) for e in row['current']['entries']]
 def codes(self,entries=None,pending=False):
  self.write(pr.MANIFEST,self.data);records,_=rg.inventory(self.root)
  return {e['code'] for e in pr.audit_packs(self.root,self.policy,records,entries or [],pending)}
 def test_current_exact_old_state_allowed(self):r=self.record();self.assertFalse(self.codes(self.entries(r)))
 def test_current_wrong_group_rejected(self):r=self.record();e=self.entries(r);e[0]['group']='scene';self.assertIn('PACK_REGISTRATION',self.codes(e))
 def test_extra_label_changes_bundle_rejected(self):r=self.record();e=self.entries(r);e[0]['labels']+=['unexpected'];self.assertIn('PACK_REGISTRATION',self.codes(e))
 def test_removed_current_label_rejected(self):r=self.record();e=self.entries(r);e[0]['labels']=[];self.assertIn('PACK_REGISTRATION',self.codes(e))
 def test_current_address_alias_cannot_change(self):r=self.record();e=self.entries(r);e[0]['address']='alias';self.assertIn('PACK_REGISTRATION',self.codes(e))
 def test_current_unregistered_cannot_silently_register(self):r=self.record();r['current']['entries']=[];self.assertIn('PACK_REGISTRATION',self.codes([dict(guid=r['guid'],group='Default Local Group',address=r['current']['path'],labels=[])]))
 def test_move_without_transition_rejected(self):r=self.record();src=self.root/r['current']['path'];dest=self.root/r['target']['path'];dest.parent.mkdir(parents=True);src.rename(dest);Path(str(src)+'.meta').rename(str(dest)+'.meta');self.assertIn('PACK_STATE',self.codes(self.entries(r)))
 def test_missing_manifest_asset_not_deletion_permission(self):r=self.record();(self.root/r['current']['path']).unlink();self.assertIn('PACK_MISSING',self.codes())
 def test_guid_replacement_rejected(self):r=self.record();Path(str(self.root/r['current']['path'])+'.meta').write_text('guid: '+uuid.uuid4().hex);self.assertIn('PACK_GUID',self.codes(self.entries(r)))
 def test_duplicate_runtime_entries_rejected(self):r=self.record();self.assertIn('PACK_DUPLICATE_ENTRY',self.codes(self.entries(r)*2))
 def test_new_default_not_grandfathered(self):self.asset('Assets/Art/unpack/default/card/new.png');self.assertIn('PACK_UNCLASSIFIED',self.codes())
 def test_new_scene_not_grandfathered(self):self.asset('Assets/Art/unpack/scene/battle/2101/new.png');self.assertIn('PACK_UNCLASSIFIED',self.codes())
 def test_new_unknown_card_rejected(self):self.asset('Assets/Art/frameAnimation/cardBattle/9999/Idle.png');self.assertIn('PACK_IDENTITY',self.codes())
 def test_new_unit_id_not_accepted_as_map(self):self.asset('Assets/Art/unpack/map/2101/ground.png');self.assertIn('PACK_IDENTITY',self.codes())
 def test_new_card_valid_identity_pending_assignment(self):self.asset('Assets/Art/frameAnimation/cardBattle/2101/Idle.png');self.assertFalse(self.codes(pending=True))
 def test_new_card_requires_runtime_registration(self):self.asset('Assets/Art/frameAnimation/cardBattle/2101/Idle.png');self.assertIn('PACK_REGISTRATION',self.codes())
 def test_new_card_has_one_packing_label(self):p='Assets/Art/frameAnimation/cardBattle/2101/Idle.png';g=self.asset(p);self.assertFalse(self.codes([dict(guid=g,group='cardBattle',address=p,labels=['card_2101'])]))
 def test_new_card_wrong_label_rejected(self):
  p='Assets/Art/frameAnimation/cardBattle/2101/Idle.png';g=self.asset(p);entries=[dict(guid=g,group='cardBattle',address=p,labels=['card_2101','main-content'])];self.assertIn('PACK_REGISTRATION',self.codes(entries))
 def test_flat_display_art_identity(self):
  p='Assets/Art/unpack/cardDisplay/portraits/2101.png';g=self.asset(p);entries=[dict(guid=g,group='cardDisplay',address=p,labels=['portrait_2101'])];self.assertFalse(self.codes(entries))
 def test_nested_static_display_rejected(self):self.asset('Assets/Art/unpack/cardDisplay/portraits/2101/portrait.png');self.assertIn('PACK_IDENTITY',self.codes())
 def test_unknown_ui_module_rejected(self):self.asset('Assets/Art/unpack/uiMain/unknown/a.png');self.assertIn('PACK_IDENTITY',self.codes())
 def test_source_cannot_register(self):
  p='Assets/Art/atlasSource/uiMain/mainView/a.png';g=self.asset(p);entries=[dict(guid=g,group='uiMain',address=p,labels=['ui_mainView'])];self.assertIn('PACK_REGISTRATION',self.codes(entries))
 def test_source_without_registration_valid(self):self.asset('Assets/Art/atlasSource/uiMain/mainView/a.png');self.assertFalse(self.codes())
 def test_unknown_shared_building_requires_explicit_owner(self):self.asset('Assets/Art/unpack/battleCommon/buildings/new.png');self.assertIn('PACK_IDENTITY',self.codes())
 def test_schema_together_cannot_replace_by_label(self):self.write('Assets/AddressableAssetsData/AssetGroups/Schemas/cardBattle_BundledAssetGroupSchema.asset','x');(self.root/'Assets/AddressableAssetsData/AssetGroups/Schemas/cardBattle_BundledAssetGroupSchema.asset').write_text('  m_BundleMode: 0\n');self.assertIn('PACK_SCHEMA',self.codes())
 def test_new_group_local_profile_rejected(self):
  f=self.write('Assets/AddressableAssetsData/AssetGroups/Schemas/cardBattle_BundledAssetGroupSchema.asset','x');f.write_text('  m_BundleMode: 2\n  m_BuildPath:\n    m_Id: local\n  m_LoadPath:\n    m_Id: local\n');self.data['remoteProfileIds']={'build':'remote','load':'remote'};self.assertIn('PACK_REMOTE',self.codes())
 def test_new_group_remote_profile_valid(self):
  f=self.write('Assets/AddressableAssetsData/AssetGroups/Schemas/cardBattle_BundledAssetGroupSchema.asset','x');f.write_text('  m_BundleMode: 2\n  m_BuildPath:\n    m_Id: remote\n  m_LoadPath:\n    m_Id: remote\n');self.data['remoteProfileIds']={'build':'remote','load':'remote'};self.assertFalse(self.codes())
 def test_common_cannot_pull_map(self):
  target='Assets/Art/unpack/map/1001/ground.png';g=self.asset(target);source='Assets/Prefab/uiCommon/controls/Widget.prefab';s=self.asset(source,'  texture: {fileID: 1, guid: '+g+', type: 3}');es=[dict(guid=g,group='map',address=target,labels=['map_1001']),dict(guid=s,group='uiCommon',address=source,labels=['ui_controls'])];self.assertIn('PACK_DEPENDENCY',self.codes(es))
 def test_one_card_cannot_pull_another_card(self):
  self.write('Assets/ConfigBin/cfgobj_cardobj.json',[dict(id=2101),dict(id=2104)]);target='Assets/Art/frameAnimation/cardBattle/2104/Idle.png';g=self.asset(target);source='Assets/Prefab/cardBattle/2101/unit.prefab';s=self.asset(source,'  texture: {fileID: 1, guid: '+g+', type: 3}');es=[dict(guid=g,group='cardBattle',address=target,labels=['card_2104']),dict(guid=s,group='cardBattle',address=source,labels=['card_2101'])];self.assertIn('PACK_DEPENDENCY',self.codes(es))
 def test_own_card_dependency_valid(self):
  target='Assets/Art/frameAnimation/cardBattle/2101/Idle.png';g=self.asset(target);source='Assets/Prefab/cardBattle/2101/unit.prefab';s=self.asset(source,'  texture: {fileID: 1, guid: '+g+', type: 3}');es=[dict(guid=g,group='cardBattle',address=target,labels=['card_2101']),dict(guid=s,group='cardBattle',address=source,labels=['card_2101'])];self.assertFalse(self.codes(es))
 def test_bad_manifest_schema_rejected(self):self.data['schemaVersion']=2;self.assertRaises(ValueError,self.codes)
 def test_unknown_group_mode_rejected(self):self.data['groupModes']={};self.assertRaises(ValueError,self.codes)
 def test_duplicate_manifest_guid_rejected(self):r=self.record();self.data['records'].append(copy.deepcopy(r));self.assertRaises(ValueError,self.codes)
 def test_target_needs_transition_evidence(self):r=self.record();r['state']='target';self.assertRaises(ValueError,self.codes)
 def test_retired_preserves_file_until_separate_delete(self):r=self.record();r['state']='retired';r['current']['entries']=[];self.assertFalse(self.codes());self.assertTrue((self.root/r['current']['path']).exists())
 def transition(self,row,**extra):
  self.write(pr.MANIFEST,self.data);plan=dict(batchId='P4-fixture',packTransitionGuids=[row['guid']],moves=[dict(source=row['current']['path'],target=row['target']['path'],guid=row['guid'])]);plan.update(extra);return plan
 def test_transition_exact_path_and_batch(self):r=self.record();self.assertIsNone(pr.validate_transitions(self.root,self.policy,self.transition(r)))
 def test_transition_wrong_batch_rejected(self):r=self.record();p=self.transition(r,batchId='P3-wrong');self.assertRaises(ValueError,pr.validate_transitions,self.root,self.policy,p)
 def test_p0_disables_production_migration(self):r=self.record();self.data['migrationEnabled']=False;p=self.transition(r);self.assertRaises(ValueError,pr.validate_transitions,self.root,self.policy,p)
 def test_implementation_gate_needs_evidence(self):r=self.record();r['implementationGate']='Verify actual consumers before moving';p=self.transition(r);self.assertRaises(ValueError,pr.validate_transitions,self.root,self.policy,p)
 def test_implementation_gate_evidence_allows_transition(self):r=self.record();r['implementationGate']='Verify actual consumers before moving';r['gateEvidence']='fixture-consumer-check.json';p=self.transition(r);self.assertIsNone(pr.validate_transitions(self.root,self.policy,p))
 def test_hold_not_migratable(self):r=self.record();r['state']='hold';p=self.transition(r);self.assertRaises(ValueError,pr.validate_transitions,self.root,self.policy,p)
 def test_transition_unapproved_target_rejected(self):r=self.record();p=self.transition(r);p['moves'][0]['target']='Assets/Art/unpack/cardDisplay/cardFaces/9999.png';self.assertRaises(ValueError,pr.validate_transitions,self.root,self.policy,p)
 def test_registration_only_transition_valid(self):r=self.record();r['target']['path']=r['current']['path'];p=self.transition(r,moves=[]);self.assertIsNone(pr.validate_transitions(self.root,self.policy,p))
 def test_current_old_registration_not_suppressed_by_pending_flag(self):r=self.record();e=self.entries(r);e[0]['group']='scene';self.assertIn('PACK_REGISTRATION',self.codes(e,pending=True))
 def test_target_transition_allows_only_exact_stale_registration(self):
  r=self.record();r['target']['path']=r['current']['path'];r['state']='target';r['transitionEvidence']='P4-test/hash';self.assertFalse(self.codes(self.entries(r),pending=True));self.assertIn('PACK_REGISTRATION',self.codes(self.entries(r)))
 def test_explicit_root_scope_matches_default(self):
  policy={'schemaVersion':1,'toolVersion':rg.VERSION,'groups':list(pr.OLD_GROUPS),'modules':[],'protectedVendorRoots':[],'globalResourceRoots':[],'artTypes':[],'bootstrapResources':[],'builtInVirtualGuids':[]}
  for scope in [['Assets'],['Assets/']]:self.assertEqual(rg.audit(self.root,policy)['errors'],rg.audit(self.root,policy,scopes=scope)['errors'])
 def test_scope_escape_rejected(self):
  policy={'schemaVersion':1,'toolVersion':rg.VERSION,'groups':list(pr.OLD_GROUPS),'modules':[],'protectedVendorRoots':[],'globalResourceRoots':[],'artTypes':[],'bootstrapResources':[],'builtInVirtualGuids':[]}
  self.assertRaises(ValueError,rg.audit,self.root,policy,scopes=['Assets/../outside'])
 def test_crlf_entry_labels_parsed(self):
  f=self.root/'Assets/AddressableAssetsData/AssetGroups/test.asset';f.parent.mkdir(parents=True);f.write_bytes(b'  m_GroupName: scene\r\n  - m_GUID: 123\r\n    m_Address: Assets/a.png\r\n    m_ReadOnly: 0\r\n    m_SerializedLabels:\r\n    - scene1001\r\n  m_ReadOnly: 0\r\n    - unrelated-schema\r\n');self.assertEqual(rg.aa_entries(self.root)[0]['labels'],['scene1001'])
 def native(self,entries,nodes):
  self.write(pr.MANIFEST,self.data);f=self.write('native.json',dict(schemaVersion=1,project=str(self.root),producer='AssetDatabase.GetDependencies',nodes=nodes));records,_=rg.inventory(self.root);return pr.audit_dependency_snapshot(self.root,self.policy,records,entries,f)
 def test_native_snapshot_cannot_omit_runtime_root(self):
  p='Assets/Prefab/uiCommon/controls/a.prefab';g=self.asset(p);e=[dict(guid=g,group='uiCommon',address=p,labels=['ui_controls'])];self.assertRaises(ValueError,self.native,e,{})
 def test_bundle_peer_dependency_detected(self):
  a='Assets/Prefab/uiCommon/controls/a.prefab';b='Assets/Prefab/uiCommon/controls/b.prefab';m='Assets/Art/unpack/map/1001/ground.png';ga=self.asset(a);gb=self.asset(b);gm=self.asset(m);es=[dict(guid=ga,group='uiCommon',address=a,labels=['ui_controls']),dict(guid=gb,group='uiCommon',address=b,labels=['ui_controls']),dict(guid=gm,group='map',address=m,labels=['map_1001'])];findings,stats=self.native(es,{a:[],b:[m],m:[]});self.assertTrue(any(f['path']==a and '[same Bundle]'in f['message'] for f in findings))
 def test_native_snapshot_recursive_nodes_required(self):
  p='Assets/Prefab/uiCommon/controls/a.prefab';g=self.asset(p);e=[dict(guid=g,group='uiCommon',address=p,labels=['ui_controls'])];self.assertRaises(ValueError,self.native,e,{p:['Assets/missing-dependency.mat']})
 def test_native_snapshot_keeps_legacy_scope_explicit(self):
  r=self.record();es=self.entries(r);findings,stats=self.native(es,{r['current']['path']:[]});self.assertFalse(findings);self.assertEqual(stats['legacyRootsPendingMigration'],1);self.assertTrue(stats['notActualBuildReport'])
 def test_missing_semantic_module_returns_environment_code_three(self):
  import subprocess,sys,shutil
  directory=self.root/'isolated-tool';directory.mkdir()
  for name in ['resource_governance.py','skill_icon_rules.py','item_icon_rules.py']:shutil.copyfile(Path(__file__).parent/name,directory/name)
  report=self.root/'missing-module.json';r=subprocess.run([sys.executable,str(directory/'resource_governance.py'),'--project',str(self.root),'--output',str(report)],cwd=self.root,capture_output=True,text=True);self.assertEqual(r.returncode,3);self.assertIn('module unavailable',json.loads(report.read_text(encoding='utf-8'))['error'])
 def data_texture(self):
  r=self.record();r['importContract']={'textureType':0,'sRGBTexture':0};f=Path(str(self.root/r['current']['path'])+'.meta');f.write_text(f.read_text()+'TextureImporter:\n  textureType: 0\n  mipmaps:\n    sRGBTexture: 0\n');return r,f
 def test_data_texture_contract_preserved(self):r,f=self.data_texture();self.assertNotIn('PACK_IMPORT_CONTRACT',self.codes(self.entries(r)))
 def test_data_texture_cannot_be_forced_to_sprite(self):r,f=self.data_texture();f.write_text(f.read_text().replace('textureType: 0','textureType: 8'));self.assertIn('PACK_IMPORT_CONTRACT',self.codes(self.entries(r)))
 def test_data_texture_cannot_enable_srgb(self):r,f=self.data_texture();f.write_text(f.read_text().replace('sRGBTexture: 0','sRGBTexture: 1'));self.assertIn('PACK_IMPORT_CONTRACT',self.codes(self.entries(r)))
 def stage(self):
  import hashlib
  r=self.record('Assets/Prefab/default/mainView/MainPanel.prefab');src='Assets/Editor/publisher.cs';consumer='Assets/Scripts/login/loader.cs';self.asset(src);self.asset(consumer)
  self.data['stageContract']=dict(mode='legacy-current-default',publicKey='main-content',mainRootGuid=r['guid'],producer=src,producerSha256=hashlib.sha256((self.root/src).read_bytes()).hexdigest(),consumer=consumer,consumerSha256=hashlib.sha256((self.root/consumer).read_bytes()).hexdigest(),scopeGroup='Default Local Group');return r
 def test_stage_current_contract_matches(self):r=self.stage();self.assertNotIn('PACK_STAGE_ROOT',self.codes(self.entries(r)));self.assertNotIn('PACK_STAGE_SOURCE',self.codes(self.entries(r)))
 def test_stage_consumer_change_requires_matching_freeze(self):r=self.stage();(self.root/self.data['stageContract']['consumer']).write_text('changed');self.assertIn('PACK_STAGE_SOURCE',self.codes(self.entries(r)))
 def test_stage_root_move_requires_contract_switch(self):r=self.stage();r['state']='target';r['transitionEvidence']='test';r['target']['path']=r['current']['path'];e=[dict(guid=r['guid'],group='cardDisplay',address=r['current']['path'],labels=['face_2101'])];self.assertIn('PACK_STAGE_ROOT',self.codes(e))
 def test_minimum_scope_rejects_extra_asset(self):r=self.record();self.data['supplementRevision']='RESOURCE-PACK-NORM/R1';self.data['testScopes']={'tiny':{'allowedGuids':[]}};p=self.transition(r);p['testScope']='tiny';self.assertRaises(ValueError,pr.validate_transitions,self.root,self.policy,p)
 def test_minimum_scope_accepts_declared_asset(self):r=self.record();self.data['supplementRevision']='RESOURCE-PACK-NORM/R1';self.data['testScopes']={'tiny':{'allowedGuids':[r['guid']]}};p=self.transition(r);p['testScope']='tiny';self.assertIsNone(pr.validate_transitions(self.root,self.policy,p))

 def main_root_stage(self):
  r=self.stage();self.data['currentGroupModes']['Default Local Group']='PackSeparately';self.data['stageContract'].update(mode='legacy-main-root-address',publicKey=r['current']['path'],transitionEvidence='approved exact loading patch');return r
 def test_main_root_exact_contract_valid(self):r=self.main_root_stage();self.assertFalse(self.codes(self.entries(r)))
 def test_main_root_rejects_whole_label(self):r=self.main_root_stage();self.data['stageContract']['publicKey']='main-content';self.assertIn('PACK_STAGE_ROOT',self.codes(self.entries(r)))
 def test_main_root_requires_separate_legacy_granularity(self):r=self.main_root_stage();self.data['currentGroupModes']['Default Local Group']='PackTogether';self.assertIn('PACK_STAGE_ROOT',self.codes(self.entries(r)))
 def test_main_root_requires_transition_evidence(self):r=self.main_root_stage();self.data['stageContract']['transitionEvidence']='';self.assertIn('PACK_STAGE_ROOT',self.codes(self.entries(r)))
 def test_main_root_rejects_consumer_drift(self):r=self.main_root_stage();(self.root/self.data['stageContract']['consumer']).write_text('changed');self.assertIn('PACK_STAGE_SOURCE',self.codes(self.entries(r)))

 def exact_shared_override(self):
  import hashlib
  r=self.record('Assets/Art/atlasSource/default/sharedPower/power-digit-0.png');r['current']['entries']=[];r['target'].update(path='Assets/Art/unpack/uiCommon/power/power-digit-0.png',group='uiCommon',bundleKey='ui_power')
  o=dict(guid=r['guid'],path=r['current']['path'],beforeEntries=[],entry=dict(group='uiCommon',address=r['current']['path'],labels=['ui_power']),assetSha256=hashlib.sha256((self.root/r['current']['path']).read_bytes()).hexdigest(),purpose='shared-dependency',batchId='exact-fixture',evidence='existing duplicate dependency observed');self.data['packingOverrides']=[o];return r,o,[dict(o['entry'],guid=r['guid'])]
 def test_exact_shared_override_valid(self):r,o,e=self.exact_shared_override();self.assertFalse(self.codes(e))
 def test_exact_shared_override_routes_producer(self):
  r,o,e=self.exact_shared_override();self.codes(e);records,_=rg.inventory(self.root);item=next(x for x in pr.routing_report(self.root,self.policy,records)['items'] if x['guid']==r['guid']);self.assertEqual(item['state'],'packing-override');self.assertEqual(item['desiredEntries'],[o['entry']])
 def test_override_does_not_exempt_other_source(self):r,o,e=self.exact_shared_override();self.assertTrue(pr.shared_source_override(o['path'],o['guid'],self.data));self.assertFalse(pr.shared_source_override(o['path']+'-other',o['guid'],self.data))
 def test_override_rejects_duplicate_identity(self):r,o,e=self.exact_shared_override();self.data['packingOverrides'].append(copy.deepcopy(o));self.assertRaises(ValueError,self.codes,e)
 def test_override_rejects_unknown_identity(self):r,o,e=self.exact_shared_override();o['guid']='0'*32;self.assertRaises(ValueError,self.codes,e)
 def test_override_rejects_path_drift(self):r,o,e=self.exact_shared_override();o['path']='Assets/Other.png';self.assertRaises(ValueError,self.codes,e)
 def test_override_rejects_history_replacement(self):r,o,e=self.exact_shared_override();o['beforeEntries']=[o['entry']];self.assertRaises(ValueError,self.codes,e)
 def test_override_rejects_wrong_owner(self):r,o,e=self.exact_shared_override();o['entry']['group']='uiMain';self.assertRaises(ValueError,self.codes,e)
 def test_override_rejects_extra_bundle_label(self):r,o,e=self.exact_shared_override();o['entry']['labels'].append('main-content');self.assertRaises(ValueError,self.codes,e)
 def test_override_rejects_content_drift(self):r,o,e=self.exact_shared_override();(self.root/o['path']).write_text('changed');self.assertIn('PACK_OVERRIDE_CONTENT',self.codes(e))
 def test_override_is_strict_after_registration(self):r,o,e=self.exact_shared_override();self.assertIn('PACK_REGISTRATION',self.codes([]))

if __name__=='__main__':unittest.main(verbosity=2)
