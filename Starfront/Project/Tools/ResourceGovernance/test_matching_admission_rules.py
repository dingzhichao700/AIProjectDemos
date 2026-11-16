import copy,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import matching_admission_rules as ma
import pack_rules as pr
import domain_rules as dr
import resource_governance as rg

class MatchingAdmissionTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name)/'Project';self.root.mkdir();self.rows=[];self.assets=[];self.inventory=[]
  sources=[ma.SOURCE_PREFAB+'PlayerMatchingPanel.prefab',ma.SOURCE_ART+'background-space.png',ma.SOURCE_FONT]
  for i,source in enumerate(sources):
   guid=('%032x'%(i+1));file=self.root/source;file.parent.mkdir(parents=True,exist_ok=True);file.write_bytes(('candidate-'+str(i)).encode());Path(str(file)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\n','utf8')
   target,group,labels=ma.expected_target(source);asset=dict(guid=guid,source=source,target=target,sha256=ma.digest(file),metaSha256=ma.digest(str(file)+'.meta'),entry=dict(group=group,address=target,labels=labels));self.assets.append(asset)
   self.inventory.append(dict(guid=guid,path=source,sha256=asset['sha256'],metaSha256=asset['metaSha256']))
   self.rows.append(dict(guid=guid,state='hold',current=dict(path=source,entries=[]),target=dict(path=target,group=group,bundleKey=ma.PAGE_KEY if group=='default' else 'font_matching')))
  self.inactive=[]
  for i,name in enumerate(sorted(ma.FORBIDDEN_NAMES)):
   guid='%032x'%(10+i);self.inactive.append(guid);source=ma.SOURCE_PREFAB+'components/'+name
   self.inventory.append(dict(guid=guid,path=source,sha256='a'*64,metaSha256='b'*64));self.rows.append(dict(guid=guid,state='hold',current=dict(path=source,entries=[]),target=dict(path=source,group=None,bundleKey=None)))
  frozen=Path(self.temp.name)/'freeze.json';frozen.write_text(json.dumps(dict(assets=self.inventory)),'utf8')
  self.plan=dict(schemaVersion=1,kind='matching-admission',batchId='MATCH-ADMIT-01',testScope='matching-admission-r31',approvalEvidence='human explicit minimum migration approval',inventorySha256=ma.digest(frozen),moves=[{k:a[k] for k in ['guid','source','target','sha256','metaSha256']} for a in self.assets],packTransitionGuids=[a['guid'] for a in self.assets],deletes=[])
  planfile=Path(self.temp.name)/'plan.json';planfile.write_text(json.dumps(self.plan),'utf8');self.planhash=ma.digest(planfile)
  self.scope=dict(revision=ma.REVISION,scopeId=self.plan['testScope'],batchId=self.plan['batchId'],approvalEvidence=self.plan['approvalEvidence'],inventoryPath=str(frozen),inventorySha256=ma.digest(frozen),planSha256=self.planhash,assets=self.assets,inactiveGuids=self.inactive)
  self.manifest=dict(records=self.rows,matchingAdmission=self.scope,testScopes={self.plan['testScope']:dict(allowedGuids=[a['guid'] for a in self.assets])},packingOverrides=[],currentGroupModes={'Default Local Group':'PackSeparately'},groupModes=ma.expected_modes({'matchingAdmission':{}},pr.MODES),resourceDomainPolicy=dict(revision=dr.REVISION,approvalId=dr.REVISION,bundleRoles={}))
 def check_preflight(self,plan=None,entries=None):return ma.preflight(self.root,self.manifest,plan or self.plan,entries or [],self.planhash)
 def test_exact_frozen_minimum_moves_pass(self):self.assertEqual(self.check_preflight()['admissionCount'],3)
 def test_only_default_mode_changes(self):
  modes=ma.expected_modes(self.manifest,pr.MODES);self.assertEqual({g for g in modes if modes[g]!=pr.MODES[g]},{'default'});self.assertEqual(self.manifest['currentGroupModes']['Default Local Group'],'PackSeparately')
 def test_nonadmission_modes_unchanged(self):self.assertEqual(ma.expected_modes({},pr.MODES),pr.MODES)
 def test_prototype_profession_is_always_blocked(self):self.assertRaises(ValueError,ma.expected_target,ma.SOURCE_PREFAB+'components/Profession_heavy.prefab')
 def test_missing_main_root_rejected(self):
  self.scope['assets']=self.assets[1:];self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_wrong_label_rejected(self):
  self.assets[0]['entry']['labels']=['main-content'];self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_wrong_group_rejected(self):
  self.assets[0]['entry']['group']='common';self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_wrong_target_directory_rejected(self):
  self.assets[0]['target']='Assets/Prefab/default/other/PlayerMatchingPanel.prefab';self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_font_entry_cannot_have_page_label(self):
  self.assets[-1]['entry']['labels']=[ma.PAGE_KEY];self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_inactive_preview_cannot_have_entries(self):
  self.rows[-1]['current']['entries']=[dict(group='default',address='x',labels=[ma.PAGE_KEY])];self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_inventory_extra_guid_rejected(self):
  file=Path(self.scope['inventoryPath']);data=ma.read(file);data['assets'].append(dict(guid='f'*32,path='Assets/a'));file.write_text(json.dumps(data),'utf8');self.scope['inventorySha256']=ma.digest(file);self.plan['inventorySha256']=self.scope['inventorySha256'];self.assertRaises(ValueError,self.check_preflight)
 def test_duplicate_selected_guid_rejected(self):
  self.scope['assets'].append(copy.deepcopy(self.assets[0]));self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_test_scope_broadening_rejected(self):
  self.manifest['testScopes'][self.plan['testScope']]['allowedGuids'].append('f'*32);self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_exact_plan_bytes_required(self):self.assertRaises(ValueError,ma.preflight,self.root,self.manifest,self.plan,[],'c'*64)
 def test_file_hash_drift_rejected(self):
  (self.root/self.assets[0]['source']).write_bytes(b'changed by UI after freeze');self.assertRaises(ValueError,self.check_preflight)
 def test_meta_hash_drift_rejected(self):
  Path(str(self.root/self.assets[0]['source'])+'.meta').write_text('guid: '+self.assets[0]['guid']+'\nimporter: changed','utf8');self.assertRaises(ValueError,self.check_preflight)
 def test_guid_drift_rejected(self):
  Path(str(self.root/self.assets[0]['source'])+'.meta').write_text('guid: '+'f'*32+'\n','utf8');self.assertRaises(ValueError,self.check_preflight)
 def test_occupied_destination_rejected(self):
  target=self.root/self.assets[0]['target'];target.parent.mkdir(parents=True);target.write_text('other');self.assertRaises(ValueError,self.check_preflight)
 def test_candidate_double_registration_rejected(self):self.assertRaises(ValueError,self.check_preflight,entries=[dict(guid=self.assets[0]['guid'])])
 def test_inactive_candidate_registration_rejected(self):self.assertRaises(ValueError,self.check_preflight,entries=[dict(guid=self.inactive[0])])
 def test_move_scope_expansion_rejected(self):
  self.plan['moves'].append(dict(self.plan['moves'][0]));self.assertRaises(ValueError,self.check_preflight)
 def test_delete_rejected(self):
  self.plan['deletes']=['Assets/other.png'];self.assertRaises(ValueError,self.check_preflight)
 def test_existing_legacy_schema_change_rejected(self):
  self.manifest['currentGroupModes']['Default Local Group']='PackTogetherByLabel';self.assertRaises(ValueError,ma.validate_contract,self.manifest)
 def test_post_move_contract_uses_recorded_source_not_existing_source_file(self):
  for row,asset in zip(self.rows,self.assets):
   source=self.root/asset['source'];target=self.root/asset['target'];target.parent.mkdir(parents=True,exist_ok=True);source.rename(target);Path(str(source)+'.meta').rename(Path(str(target)+'.meta'));row['state']='target'
  self.assertIsNotNone(ma.validate_contract(self.manifest));self.assertTrue(ma.active(self.manifest,self.assets[0]['guid']));self.assertRaises(ValueError,self.check_preflight)
 def test_new_matching_label_generated(self):self.assertEqual(pr.labels_for('default',ma.PAGE_KEY),[ma.PAGE_KEY])
 def test_matching_is_ui_page(self):self.assertEqual(dr.role('default',ma.PAGE_KEY,self.manifest['resourceDomainPolicy']),'ui-page')
 def schema_findings(self,default_mode=2,legacy_mode=1):
  row=self.rows[0];row['state']='target';path=row['target']['path'];target=self.root/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_text('matching root fixture','utf8')
  base=self.root/'Assets/AddressableAssetsData/AssetGroups/Schemas';base.mkdir(parents=True,exist_ok=True)
  (base/'default_BundledAssetGroupSchema.asset').write_text('  m_BundleMode: '+str(default_mode)+'\n','utf8')
  (base/'Default Local Group_BundledAssetGroupSchema.asset').write_text('  m_BundleMode: '+str(legacy_mode)+'\n','utf8')
  self.manifest['groupModules']={};records={path:dict(guid=row['guid'])};entries=[dict(guid=row['guid'],group='default',address=path,labels=[ma.PAGE_KEY])]
  with patch.object(pr,'load_manifest',return_value=self.manifest):return pr.audit_packs(self.root,{},records,entries)
 def test_admitted_actual_default_schema_by_label_alongside_legacy_separate(self):
  findings=self.schema_findings();self.assertFalse(any(x['code']=='PACK_SCHEMA' for x in findings))
 def test_admitted_default_cannot_revert_to_together(self):
  findings=self.schema_findings(default_mode=0);self.assertTrue(any(x['code']=='PACK_SCHEMA' and x['path'].endswith('/default_BundledAssetGroupSchema.asset') for x in findings))
 def test_admission_does_not_allow_legacy_default_mode_change(self):
  findings=self.schema_findings(legacy_mode=0);self.assertTrue(any(x['code']=='PACK_SCHEMA' and x['path'].endswith('/Default Local Group_BundledAssetGroupSchema.asset') for x in findings))
 def test_native_matching_root_cannot_pull_other_page(self):
  row=self.rows[0];row['state']='target';path=row['target']['path'];foreign='Assets/Prefab/uiMain/mainView/MainPanel.prefab';fg='e'*32
  self.manifest['records'].append(dict(guid=fg,state='current',current=dict(path=foreign,entries=[dict(group='uiMain',address=foreign,labels=['ui_mainView'])]),target=dict(path=foreign,group='uiMain',bundleKey='ui_mainView')))
  records={path:dict(guid=row['guid']),foreign:dict(guid=fg)};entries=[dict(guid=row['guid'],group='default',address=path,labels=[ma.PAGE_KEY]),dict(guid=fg,group='uiMain',address=foreign,labels=['ui_mainView'])]
  snapshot=Path(self.temp.name)/'native.json';snapshot.write_text(json.dumps(dict(schemaVersion=1,project=str(self.root),producer='AssetDatabase.GetDependencies',nodes={path:[foreign],foreign:[]})),'utf8')
  with patch.object(pr,'load_manifest',return_value=self.manifest):findings,stats=pr.audit_dependency_snapshot(self.root,{},records,entries,snapshot)
  self.assertTrue(any(x['code']=='PACK_NATIVE_DEPENDENCY' and x['path']==path for x in findings))
 def test_ordinary_preflight_signature_still_accepts_two_args(self):
  plan=dict(schemaVersion=1,batchId='empty',approvalEvidence='test');self.assertRaisesRegex(ValueError,'Empty move',rg.preflight_plan,self.root,plan)

if __name__=='__main__':unittest.main()
