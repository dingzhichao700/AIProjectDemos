import unittest,tempfile,json,uuid,copy
from pathlib import Path
import card_suite_rules as cs
import pack_rules as pr

class SuiteTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.records={};self.policy={'cardSuiteGovernance':{'schemaVersion':1,'manifest':cs.MANIFEST,'revision':'RESOURCE-PACK/R2'}}
  self.unit=dict(id=2101,skillIds=[1101],firePoints=[dict(action=3,direction=2),dict(action=3,direction=4)],appearances=[])
  for action in [1,2,3,4]:
   for direction,name in [(2,'UpRight'),(4,'DownRight')]:
    relative=f'cardBattle/2101/{cs.ACTION_NAMES[action]}_{name}';self.unit['appearances'].append(dict(action=action,direction=direction,resource=relative,loop=action in [1,4]));self.pair(relative)
  self.card=dict(id=2101,artId=2101,showInLibrary=True,content={'$type':'UnitCardContent','unitId':2101})
  for p in ['Assets/Art/unpack/cardDisplay/cardFaces/2101.png','Assets/Art/unpack/cardDisplay/portraits/2101.png']:self.asset(p)
  self.suite=dict(cardId=2101,artId=2101,unitId=2101,buildingId=2101,readiness='partial',usages={u:dict(status='missing',evidence='fixture missing scope') for u in cs.USAGES})
  for u in ['cardFace','portrait','unitActions','productionBuilding','skillIcons','fragmentIcon']:self.suite['usages'][u]['status']='configured'
  self.contract=dict(revision='RESOURCE-PACK/R2',sourceDirections=[2,4],cards=[self.suite],legacyUnits=[],candidateAssets=[],buildingAssets=[])
  self.write('Assets/ConfigBin/cfgobj_cardobj.json',[self.card]);self.write('Assets/ConfigBin/cfgobj_cardartobj.json',[dict(id=2101,cardImage='Assets/Art/unpack/cardDisplay/cardFaces/2101.png',portrait='Assets/Art/unpack/cardDisplay/portraits/2101.png')]);self.write('Assets/ConfigBin/cfgobj_skillobj.json',[dict(id=1101,action=3)]);self.write('Assets/ConfigBin/cfgobj_unitproductionobj.json',[dict(unitId=2101,buildingId=2101)]);self.write('Assets/ConfigBin/cfgobj_buildingobj.json',[dict(id=2101)])
 def tearDown(self):self.temp.cleanup()
 def write(self,path,obj):p=self.root/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(obj),encoding='utf-8')
 def asset(self,path):
  p=self.root/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('fixture',encoding='utf-8');g=uuid.uuid4().hex;Path(str(p)+'.meta').write_text('guid: '+g);self.records[path]={'guid':g};return g
 def pair(self,relative):
  for ext in ['.png','.json']:self.asset('Assets/Art/frameAnimation/'+relative+ext)
 def audit(self):
  self.write(cs.MANIFEST,dict(cardSuites=self.contract));self.write('Assets/ConfigBin/cfgobj_unitobj.json',[self.unit]);return cs.audit_card_suites(self.root,self.policy,self.records)
 def codes(self):return {x['code'] for x in self.audit() if x['severity']=='error'}
 def legacy(self):
  self.unit['appearances'].append(dict(action=1,direction=1,resource='cardBattle/2101/Move_Up',loop=True));self.pair('cardBattle/2101/Move_Up')
  assets=[dict(path=p,guid=v['guid'],sha256=cs.sha(self.root/p)) for p,v in self.records.items() if '/frameAnimation/' in p]
  self.contract['legacyUnits']=[dict(unitId=2101,fingerprint=cs.fingerprint(self.unit),assets=assets,evidence='old approved exact',remediationBatch='next explicit correction')]
 def candidate(self,state='candidate',direction=2):
  p='Assets/Art/frameAnimation/cardBattle/2101/2101-Idle_UpRight.png';g=self.asset(p);item=dict(path=p,guid=g,sha256=cs.sha(self.root/p),cardId=2101,artId=2101,direction=direction,action=4,state=state,sourceEvidence='approved original source');self.contract['candidateAssets'].append(item);return item
 def test_valid_two_source(self):self.assertFalse(self.codes())
 def test_policy_absent_compatibility(self):self.assertEqual(cs.audit_card_suites(self.root,{},{}),[])
 def test_missing_direction(self):self.unit['appearances'].pop();self.assertIn('SUITE_REQUIRED_ACTION',self.codes())
 def test_missing_death(self):self.unit['appearances']=[a for a in self.unit['appearances'] if a['action']!=2];self.assertIn('SUITE_REQUIRED_ACTION',self.codes())
 def test_missing_skill_action(self):self.unit['appearances']=[a for a in self.unit['appearances'] if a['action']!=3];self.assertIn('SUITE_REQUIRED_ACTION',self.codes())
 def test_extra_direction(self):self.unit['appearances'].append(dict(action=1,direction=1,resource='x',loop=True));self.assertIn('SUITE_SOURCE_DIRECTIONS',self.codes())
 def test_duplicate_direction(self):self.unit['appearances'].append(copy.deepcopy(self.unit['appearances'][0]));self.assertIn('SUITE_DUPLICATE_DIRECTION',self.codes())
 def test_wrong_loop(self):self.unit['appearances'][0]['loop']=False;self.assertIn('SUITE_ACTION_LOOP',self.codes())
 def test_wrong_direction_filename(self):self.unit['appearances'][0]['resource']='cardBattle/2101/Move_DownRight';self.assertIn('SUITE_DIRECTION_NAME',self.codes())
 def test_firepoint_wrong_direction(self):self.unit['firePoints'][0]['direction']=1;self.assertIn('SUITE_FIRE_DIRECTION',self.codes())
 def test_missing_configured_pair(self):p=next(p for p in self.records if '/frameAnimation/' in p);(self.root/p).unlink();self.assertIn('SUITE_ACTION_ASSET',self.codes())
 def test_unknown_skill(self):self.unit['skillIds']=[999];self.assertIn('SUITE_SKILL',self.codes())
 def test_wrong_card_mapping(self):self.suite['unitId']=999;self.assertIn('SUITE_IDENTITY',self.codes())
 def test_wrong_building_mapping(self):self.suite['buildingId']=999;self.assertIn('SUITE_BUILDING',self.codes())
 def test_missing_display(self):(self.root/'Assets/Art/unpack/cardDisplay/cardFaces/2101.png').unlink();self.assertIn('SUITE_DISPLAY',self.codes())
 def test_false_complete(self):self.suite['readiness']='complete';self.assertIn('SUITE_FALSE_COMPLETE',self.codes())
 def test_required_usage_cannot_na(self):self.suite['readiness']='complete';self.suite['usages']={u:dict(status='not-applicable',evidence='fixture') for u in cs.USAGES};self.assertIn('SUITE_FALSE_COMPLETE',self.codes())
 def test_missing_matrix_usage(self):del self.suite['usages']['ruin'];self.assertRaises(ValueError,self.audit)
 def test_unknown_matrix_card(self):self.contract['cards'].append(dict(self.suite,cardId=999));self.assertRaises(ValueError,self.audit)
 def test_exact_legacy_retained(self):self.legacy();self.assertFalse(self.codes());self.assertTrue(any(x['severity']=='legacy' for x in self.audit()))
 def test_legacy_config_change_rejected(self):self.legacy();self.unit['appearances'][0]['resource']='different';self.assertIn('SUITE_LEGACY_DRIFT',self.codes())
 def test_legacy_pixel_change_rejected(self):self.legacy();(self.root/self.contract['legacyUnits'][0]['assets'][0]['path']).write_text('changed');self.assertIn('SUITE_LEGACY_ASSETS',self.codes())
 def test_legacy_guid_change_rejected(self):self.legacy();self.records[self.contract['legacyUnits'][0]['assets'][0]['path']]['guid']='0'*32;self.assertIn('SUITE_LEGACY_ASSETS',self.codes())
 def test_legacy_cannot_broaden(self):self.legacy();self.contract['legacyUnits'][0]['assets'].pop();self.assertIn('SUITE_LEGACY_ASSETS',self.codes())
 def test_candidate_separate_valid(self):self.candidate();self.assertFalse(self.codes())
 def test_candidate_no_provenance(self):self.candidate()['sourceEvidence']='';self.assertRaises(ValueError,self.audit)
 def test_candidate_bad_direction(self):self.candidate(direction=1);self.assertRaises(ValueError,self.audit)
 def test_candidate_owner_invalid(self):self.candidate()['cardId']=999;self.assertRaises(ValueError,self.audit)
 def test_candidate_collision_legacy(self):self.legacy();c=self.candidate();c['path']=self.contract['legacyUnits'][0]['assets'][0]['path'];self.assertRaises(ValueError,self.audit)
 def test_candidate_changed(self):c=self.candidate();(self.root/c['path']).write_text('changed');self.assertIn('SUITE_CANDIDATE_ASSET',self.codes())
 def test_inactive_claim_active(self):self.candidate(state='active');self.assertIn('SUITE_ACTIVE_UNUSED',self.codes())
 def test_undeclared_new_source(self):self.pair('cardBattle/2101/Unexpected_UpRight');self.assertIn('SUITE_UNBOUND_ANIMATION',self.codes())
 def test_flat_building_binding(self):
  p='Assets/Art/unpack/cardBattle/buildings/2101-normal-body.png';d={'cards':{2101},'buildings':{2101},'buildingAssets':[dict(path=p,buildingId=2101,ownerCardIds=[2101])]};self.assertEqual(pr.route(p,d)['bundleKey'],'card_2101')
 def test_flat_building_missing_binding(self):self.assertRaises(ValueError,pr.route,'Assets/Art/unpack/cardBattle/buildings/2101-normal-body.png',{})
 def test_flat_building_shared_not_exclusive(self):
  p='Assets/Art/unpack/cardBattle/buildings/2101-normal-body.png';d={'cards':{2101,2104},'buildings':{2101},'buildingAssets':[dict(path=p,buildingId=2101,ownerCardIds=[2101,2104])]};self.assertRaises(ValueError,pr.route,p,d)
if __name__=='__main__':unittest.main()
