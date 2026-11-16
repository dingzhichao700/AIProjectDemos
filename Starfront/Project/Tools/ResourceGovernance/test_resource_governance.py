import copy,json,tempfile,unittest,uuid,os,base64
from pathlib import Path
import resource_governance as rg

class GovernanceTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);(self.root/'Assets').mkdir();local=Path(__file__).parent/'ResourceGovernance.json';policy=Path(os.environ['RG_POLICY_PATH']) if os.environ.get('RG_POLICY_PATH') else local if local.exists() else Path(__file__).parents[2]/'ProjectSettings/ResourceGovernance.json';self.policy=rg.load_json(policy);self.policy.pop('packGovernance',None);self.policy.pop('cardSuiteGovernance',None);self.policy['legacyIssues']=[];self.policy['duplicateReviewGroups']=[]
 def tearDown(self):self.temp.cleanup()
 def asset(self,path,data=b'fixture',guid=None):
  if path.endswith('.png'):data=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jk1kAAAAASUVORK5CYII=')+data
  f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(data);guid=guid or uuid.uuid4().hex;Path(str(f)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\n','utf8');return guid
 def codes(self):return {x['code'] for x in rg.audit(self.root,self.policy)['errors']}
 def entry(self,guid,address,group='Default Local Group'):
  f=self.root/'Assets/AddressableAssetsData/AssetGroups/group.asset';f.parent.mkdir(parents=True,exist_ok=True);f.write_text('  m_GroupName: '+group+'\n  m_SerializeEntries:\n  - m_GUID: '+guid+'\n    m_Address: '+address+'\n','utf8')
 def test_valid_ui_source(self):self.asset('Assets/Art/atlasSource/default/mainView/button.png');self.assertFalse(self.codes())
 def test_nested_source_rejected(self):self.asset('Assets/Art/atlasSource/default/mainView/old/button.png');self.assertIn('SOURCE_CHILD_DIRECTORY',self.codes());self.assertIn('SOURCE_LAYOUT',self.codes())
 def test_prefab_cannot_enter_unpack(self):self.asset('Assets/Art/unpack/default/card/1/rig.prefab');self.assertIn('UNPACK_TYPE',self.codes())
 def test_native_texture_is_legal(self):self.asset('Assets/Art/skeletalAnimation/default/card/2101/idle/portrait-skin.png');self.assertFalse(self.codes())
 def test_resource_data_is_legal(self):self.asset('Assets/ResourceData/default/item/item-resources.json',b'{}');self.assertFalse(self.codes())
 def test_invalid_resource_data_json(self):self.asset('Assets/ResourceData/default/item/item-resources.json',b'{bad');self.assertIn('DATA_JSON',self.codes())
 def test_builtin_virtual_not_missing(self):self.entry('EditorSceneList','EditorSceneList','Built In Data');self.assertNotIn('AA_MISSING_GUID',self.codes())
 def test_source_runtime_caught(self):p='Assets/Art/atlasSource/default/mainView/a.png';g=self.asset(p);self.entry(g,p);self.assertIn('SOURCE_RUNTIME',self.codes())
 def test_hotupdate_address_alias_preserved(self):p='Assets/HotUpdateBytes/code/Main.dll.bytes';g=self.asset(p);self.entry(g,'hotupdate/code/Main.dll','Main');self.assertNotIn('AA_ADDRESS',self.codes())
 def test_wrong_address_not_hidden_by_generated_rule(self):p='Assets/HotUpdateBytes/code/Main.dll.bytes';g=self.asset(p);self.entry(g,'hotupdate/code/Other.dll','Main');self.assertIn('AA_ADDRESS',self.codes())
 def test_legacy_exact_identity_only(self):p='Assets/Art/unpack/default/card/rig.prefab';g=self.asset(p);self.policy['legacyIssues']=[{'id':'L1','code':'UNPACK_TYPE','path':p,'guid':g,'owner':'owner','basis':'approved existing record','reviewBatch':'B2','exitCondition':'move to Prefab'}];self.assertNotIn('UNPACK_TYPE',self.codes());self.asset(p,guid=uuid.uuid4().hex);self.assertIn('UNPACK_TYPE',self.codes())
 def test_wildcard_legacy_rejected(self):self.policy['legacyIssues']=[{'id':'L1','code':'X','path':'Assets/*','guid':'1','owner':'owner','basis':'x','reviewBatch':'B2','exitCondition':'x'}];self.assertRaises(ValueError,rg.validate_policy,self.policy)
 def test_png_duplicate_requires_review(self):self.asset('Assets/Art/unpack/default/card/a.png',b'same');self.asset('Assets/Art/unpack/default/card/b.png',b'same');self.assertIn('DUPLICATE_CONTENT_REVIEW',self.codes())
 def test_known_duplicate_is_not_deletion_proof(self):a='Assets/Art/unpack/default/card/a.png';b='Assets/Art/unpack/default/card/b.png';self.asset(a,b'same');self.asset(b,b'same');self.policy['duplicateReviewGroups']=[{'paths':[a,b]}];r=rg.audit(self.root,self.policy);self.assertFalse(r['errors']);self.assertTrue(r['warnings']);self.assertTrue((self.root/a).exists())
 def test_frame_pair_required(self):self.asset('Assets/Art/frameAnimation/default/card/2101/flash.png');self.assertIn('FRAME_PAIR',self.codes())
 def test_frame_contract_required(self):p='Assets/Art/frameAnimation/default/card/2101/flash';self.asset(p+'.png');self.asset(p+'.json',b'{}');self.assertIn('FRAME_SCHEMA',self.codes())
 def test_complete_frame_contract(self):p='Assets/Art/frameAnimation/default/card/2101/flash';self.asset(p+'.png');self.asset(p+'.json',json.dumps({'frames':{'frame':{'rotated':False}},'meta':{},'pivot':{'x':.25,'y':.5}}).encode());self.assertFalse(self.codes())
 def test_business_resources_bypass_rejected(self):self.asset('Assets/Resources/Card.png');self.assertIn('BUSINESS_RESOURCES',self.codes())
 def test_vendor_resources_preserved(self):self.asset('Assets/TextMesh Pro/Resources/TMP Settings.asset');self.assertFalse(self.codes())
 def test_unresolved_config_address(self):self.asset('Assets/ConfigBin/example.json',b'[{"icon":"Assets/Art/unpack/default/card/missing.png"}]');self.assertIn('CONFIG_ADDRESS',self.codes())
 def test_missing_atlas_sprite(self):self.asset('Assets/ConfigBin/example.json',b'[{"icon":"Assets/Art/atlas/default/mainView/missing"}]');self.assertIn('CONFIG_SPRITE',self.codes())
 def test_new_version_path_rejected(self):self.asset('Assets/Art/unpack/default/card/rosterV99/a.png');self.assertIn('PRODUCTION_VERSION_NAME',self.codes())
 def test_legitimate_slider_name(self):self.asset('Assets/Art/atlasSource/default/common/slider1_dot.png');self.assertNotIn('PRODUCTION_VERSION_NAME',self.codes())
 def test_renamed_json_is_not_png(self):p='Assets/Art/unpack/default/card/wrong.png';self.asset(p);(self.root/p).write_bytes(b'{}');self.assertIn('PNG_ENCODING',self.codes())
 def test_missing_tool_lock_is_not_success(self):self.assertRaises(OSError,rg.verify_lock,self.root)
 def test_policy_array_rejected_as_schema_error(self):self.assertRaises(ValueError,rg.validate_policy,[])
 def test_scoped_check_retains_cross_module_duplicate(self):
  self.asset('Assets/Art/unpack/default/card/a.png',b'same');self.asset('Assets/Art/unpack/default/item/b.png',b'same');r=rg.audit(self.root,self.policy,scopes=['Assets/Art/unpack/default/item']);self.assertIn('DUPLICATE_CONTENT_REVIEW',{e['code'] for e in r['errors']})
 def test_duplicate_guid(self):g=self.asset('Assets/Art/unpack/default/card/a.png',b'a');self.asset('Assets/Art/unpack/default/card/b.png',b'b',g);self.assertIn('DUPLICATE_GUID',self.codes())
 def moveplan(self):
  p='Assets/Art/unpack/default/card/a.png';g=self.asset(p);return {'schemaVersion':1,'batchId':'fixture','approvalEvidence':'test-only','moves':[{'source':p,'target':'Assets/Art/unpack/default/card/b.png','guid':g,'sha256':rg.digest(self.root/p)}]}
 def test_preflight_does_not_move(self):p=self.moveplan();r=rg.preflight_plan(self.root,p);self.assertFalse(r['executed']);self.assertTrue((self.root/p['moves'][0]['source']).exists());self.assertFalse((self.root/p['moves'][0]['target']).exists())
 def test_preflight_hash_guard(self):p=self.moveplan();p['moves'][0]['sha256']='bad';self.assertRaises(ValueError,rg.preflight_plan,self.root,p)
 def test_preflight_traversal_guard(self):p=self.moveplan();p['moves'][0]['target']='Assets/../outside.png';self.assertRaises(ValueError,rg.preflight_plan,self.root,p)
 def test_preflight_case_collision(self):p=self.moveplan();second=copy.deepcopy(p['moves'][0]);second['target']=second['target'].upper();p['moves'].append(second);self.assertRaises(ValueError,rg.preflight_plan,self.root,p)
 def test_deletion_not_implied_by_general_approval(self):p=self.moveplan();p['deletes']=[{'path':p['moves'][0]['source']}];self.assertRaises(ValueError,rg.preflight_plan,self.root,p);self.assertTrue((self.root/p['moves'][0]['source']).exists())
 def test_durable_pending_journal_blocks_audit(self):d=self.root.parent/(self.root.name+'-journal');(d/'B1').mkdir(parents=True);f=d/'B1/journal.json';f.write_text(json.dumps({'project':str(self.root.resolve()),'state':'moving'}),'utf8');self.assertTrue(rg.audit(self.root,self.policy,journal_root=d)['errors']);f.unlink();(d/'B1').rmdir();d.rmdir()
 def test_verification_only_exempts_own_journal(self):
  with tempfile.TemporaryDirectory() as directory:
   d=Path(directory);(d/'one').mkdir();(d/'two').mkdir();a=d/'one/journal.json';b=d/'two/journal.json';a.write_text(json.dumps({'project':str(self.root.resolve()),'state':'verifying'}),'utf8');b.write_text(json.dumps({'project':str(self.root.resolve()),'state':'recovery-required'}),'utf8');r=rg.audit(self.root,self.policy,journal_root=d,active_journal=a);self.assertEqual(len(r['errors']),1);self.assertIn(str(b),r['errors'][0]['message'])

if __name__=='__main__':unittest.main(verbosity=2)
