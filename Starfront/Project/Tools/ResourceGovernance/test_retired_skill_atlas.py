import unittest
import test_skill_icon_rules as base
from skill_icon_rules import audit_skill_icons
class RetiredSkillAtlasTests(unittest.TestCase):
 def test_retired_atlas_cannot_be_registered(self):
  fixture=base.SkillIconRulesTests();fixture.setUp()
  try:
   fixture.codes();p=fixture.root/'Assets/AddressableAssetsData/AssetGroups/test.asset';p.parent.mkdir(parents=True);p.write_text('    m_Address: Assets/Art/atlas/default/skillIcons.png\n',encoding='utf-8')
   findings=audit_skill_icons(fixture.root,{'skillIconGovernance':fixture.rule},fixture.records);self.assertIn('SKILL_ICON_ATLAS_RUNTIME',{r['code'] for r in findings})
  finally:fixture.tearDown()
 def test_retired_path_with_another_guid_must_not_be_skipped(self):
  fixture=base.SkillIconRulesTests();fixture.setUp()
  try:
   fixture.codes();path='Assets/Art/atlas/default/skillIcons.png';fixture.records[path]={'guid':'a'*32};policy={'skillIconGovernance':fixture.rule,'retiredRuntimeAssets':[dict(path=path,guid='b'*32)]};findings=audit_skill_icons(fixture.root,policy,fixture.records);self.assertIn('RETIRED_ASSET_IDENTITY',{r['code'] for r in findings})
  finally:fixture.tearDown()
if __name__=='__main__':unittest.main(verbosity=2)
