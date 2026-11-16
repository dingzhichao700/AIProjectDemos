import unittest,copy
import domain_rules as dr
import pack_rules as pr

class DomainRulesTests(unittest.TestCase):
 def selection(self):
  row={'guid':'a','path':'Assets/a.png','sha256':'abc','before':{'group':'uiMain','address':'Assets/a.png','labels':['ui_main']},'after':{'group':'common','address':'Assets/a.png','labels':['ui_main']}}
  return row,[copy.deepcopy(row)],{'a':copy.deepcopy(row)}
 def test_exact_regroup_selection_allowed(self):
  row,approved,current=self.selection();self.assertEqual(dr.validate_regroup_selection([row],approved,current),1)
 def test_unmigrated_root_uses_actual_current_contract(self):
  source={'group':'title','bundleKey':None,'domainRole':'shared-ui','domainApplied':False}
  target={'group':'title','bundleKey':'title','domainRole':'early'}
  self.assertFalse(pr.dependency_violation(source,target))
 def test_migrated_root_keeps_domain_barrier(self):
  source={'group':'common','bundleKey':'ui_common','domainRole':'shared-ui','domainApplied':True}
  target={'group':'Default Local Group','bundleKey':'ui_main','domainRole':'ui-page','domainApplied':False}
  self.assertTrue(pr.dependency_violation(source,target))
 def test_changed_asset_blocks_regroup(self):
  row,approved,current=self.selection();current['a']['sha256']='changed';self.assertRaises(ValueError,dr.validate_regroup_selection,[row],approved,current)
 def test_changed_registration_blocks_regroup(self):
  row,approved,current=self.selection();current['a']['before']['labels']=['changed'];self.assertRaises(ValueError,dr.validate_regroup_selection,[row],approved,current)
 def test_unapproved_destination_blocks_regroup(self):
  row,approved,current=self.selection();row['after']['group']='scene';self.assertRaises(ValueError,dr.validate_regroup_selection,[row],approved,current)
 def test_duplicate_selection_blocks_regroup(self):
  row,approved,current=self.selection();self.assertRaises(ValueError,dr.validate_regroup_selection,[row,row],approved,current)
 def setUp(self):self.policy={'revision':dr.REVISION,'approvalId':dr.REVISION,'bundleRoles':{},'sceneModules':['hud','effects','projectile']}
 def test_page_group_becomes_common_not_one_big_bundle(self):self.assertEqual(dr.owner('uiMain','ui_mainView',policy=self.policy),('common','ui_mainView'))
 def test_display_keeps_action_identity(self):self.assertEqual(dr.owner('cardDisplayAnimation','display_2102_display',policy=self.policy),('common','display_2102_display'))
 def test_map_identity_moves_to_scene(self):self.assertEqual(dr.owner('map','map_1002',policy=self.policy),('scene','scene_1002'))
 def test_shared_scene_purpose_preserved(self):self.assertEqual(dr.owner('battleCommon','projectile_1101',policy=self.policy),('scene','scene_common_projectile_1101'))
 def test_matching_is_frontend_not_battle_hud(self):self.assertEqual(dr.owner('uiBattle','ui_battle','Assets/Prefab/uiBattle/battle/playerMatching/Panel.prefab',self.policy),('common','ui_playerMatching'))
 def test_battle_hud_goes_to_scene(self):self.assertEqual(dr.owner('uiBattle','ui_battleInfo',policy=self.policy),('scene','scene_common_ui_battleInfo'))
 def test_skills_coalesce_without_changing_source_type(self):self.assertEqual(dr.owner('icons','skill_210101',policy=self.policy),('common','icons_skill'))
 def test_unknown_icon_category_blocked(self):self.assertRaises(ValueError,dr.owner,'icons','misc',policy=self.policy)
 def obj(self,group,key,former=None):return dr.annotated(group,key,self.policy,former_group=former)
 def test_shared_ui_cannot_pull_whole_page(self):self.assertTrue(dr.violation(self.obj('common','ui_common', 'uiCommon'),self.obj('common','ui_mainView')))
 def test_page_may_use_shared_ui(self):self.assertFalse(dr.violation(self.obj('common','ui_mainView'),self.obj('common','ui_common','uiCommon')))
 def test_different_page_dependency_blocked(self):self.assertTrue(dr.violation(self.obj('common','ui_mainView'),self.obj('common','ui_cultivation')))
 def test_map_cannot_pull_other_map(self):self.assertTrue(dr.violation(self.obj('scene','scene_1002'),self.obj('scene','scene_1001')))
 def test_map_can_use_scene_shared(self):self.assertFalse(dr.violation(self.obj('scene','scene_1002'),self.obj('scene','scene_common_hud')))
 def test_shared_scene_cannot_pull_a_map(self):self.assertTrue(dr.violation(self.obj('scene','scene_common_hud'),self.obj('scene','scene_1002')))
 def test_card_can_use_scene_shared(self):self.assertFalse(dr.violation(self.obj('cardBattle','card_2104'),self.obj('scene','scene_common_effects')))
 def test_card_cannot_pull_map(self):self.assertTrue(dr.violation(self.obj('cardBattle','card_2104'),self.obj('scene','scene_1002')))
 def test_card_cannot_pull_other_card(self):self.assertTrue(dr.violation(self.obj('cardBattle','card_2104'),self.obj('cardBattle','card_2101')))
 def test_new_scene_route_uses_real_map_id(self):self.assertEqual(pr.route('Assets/Art/frameAnimation/scene/1002/water.png',{'domainPolicy':self.policy,'maps':{1002}})['bundleKey'],'scene_1002')
 def test_unknown_scene_id_rejected(self):self.assertRaises(ValueError,pr.route,'Assets/Art/frameAnimation/scene/2104/water.png',{'domainPolicy':self.policy,'maps':{1002}})
 def test_shared_scene_route_requires_known_purpose(self):self.assertRaises(ValueError,pr.route,'Assets/Art/frameAnimation/scene/common/misc/a.png',{'domainPolicy':self.policy,'sceneModules':['hud']})
 def test_current_project_policy_requires_approval(self):bad=copy.deepcopy(self.policy);bad['approvalId']='';self.assertRaises(ValueError,dr.validate_policy,bad)
 def test_nonconsumer_is_not_a_generic_bypass(self):
  policy=copy.deepcopy(self.policy);policy['nonConsumerRoots']={'bogus':{'path':'Assets/a','assetSha256':'x','evidence':'x'}}
  self.assertRaises(ValueError,dr.validate_nonconsumer_roots,{'resourceDomainPolicy':policy,'records':[],'packingOverrides':[]})
 def test_regrouping_cannot_activate_preview(self):
  row={'guid':'x','state':'hold','current':{'entries':[]},'target':{'group':'cardDisplayAnimation','bundleKey':'display_2102_display'}}
  self.assertRaises(ValueError,dr.validate_override,row,{'path':'Assets/x','entry':{}},self.policy)

if __name__=='__main__':unittest.main()
