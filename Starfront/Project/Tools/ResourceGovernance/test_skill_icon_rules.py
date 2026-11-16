import copy,json,tempfile,unittest
from pathlib import Path
from skill_icon_rules import audit_skill_icons

class SkillIconRulesTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
  self.rule=dict(skillsFile='Assets/ConfigBin/skills.json',entriesFile='Assets/ConfigBin/entries.json',cardsFile='Assets/ConfigBin/cards.json',independentRoot='Assets/Art/unpack/default/skillIcons',atlasSourceRoot='Assets/Art/atlasSource/default/skillIcons',atlasAddressRoot='Assets/Art/atlas/default/skillIcons',legacyIcons=[],sharedIcons=[],loadingMode="unpack-flat")
  self.path='Assets/Art/unpack/default/skillIcons/210101.png';self.records={self.path:{'guid':'a'*32}}
  self.skills=[dict(id=210101,icon=self.path)];self.entries=[dict(cardId=2101,skillId=210101)];self.cards=[dict(id=2101)]
 def tearDown(self):self.temp.cleanup()
 def codes(self):
  for key,data in [('skillsFile',self.skills),('entriesFile',self.entries),('cardsFile',self.cards)]:
   p=self.root/self.rule[key];p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data),encoding='utf-8')
  for path in self.records:
   p=self.root/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('fixture',encoding='utf-8')
  self.findings=audit_skill_icons(self.root,{'skillIconGovernance':self.rule},self.records)
  return {x['code'] for x in self.findings if x['severity']=='error'}
 def test_card_skill_path_valid(self):self.assertFalse(self.codes())
 def test_wrong_card_directory(self):self.skills[0]['icon']=self.path.replace('/210101.png','/2102/210101.png');self.assertIn('SKILL_ICON_IDENTITY',self.codes())
 def test_wrong_skill_filename(self):self.skills[0]['icon']=self.path.replace('210101.png','210102.png');self.assertIn('SKILL_ICON_IDENTITY',self.codes())
 def test_unknown_skill_relation(self):self.entries[0]['skillId']=999;self.assertIn('SKILL_ICON_OWNER',self.codes())
 def test_unknown_card_relation(self):self.entries[0]['cardId']=999;self.assertIn('SKILL_ICON_OWNER',self.codes())
 def test_orphan_skill(self):self.entries=[];self.assertIn('SKILL_ICON_OWNER',self.codes())
 def test_multiple_owners_rejected(self):self.cards.append(dict(id=2102));self.entries.append(dict(cardId=2102,skillId=210101));self.assertIn('SKILL_ICON_OWNER',self.codes())
 def test_duplicate_skill_id(self):self.skills.append(copy.deepcopy(self.skills[0]));self.assertIn('SKILL_ICON_SCHEMA',self.codes())
 def test_legacy_address_exact_only(self):
  self.skills[0]['icon']='Assets/Art/atlas/default/skillIcons/skill-210101';self.records={'Assets/Art/atlasSource/default/skillIcons/skill-210101.png':{'guid':'b'*32}}
  self.rule['legacyIcons']=[dict(cardId=2101,skillId=210101,address=self.skills[0]['icon'],source=next(iter(self.records)),sourceGuid='b'*32,owner='owner',basis='old evidence',reviewBatch='B1-next',exitCondition='migrate')]
  self.assertFalse(self.codes());self.assertEqual('legacy',self.findings[0]['severity']);self.rule['legacyIcons'][0]['sourceGuid']='c'*32;self.assertIn('SKILL_ICON_IDENTITY',self.codes())
 def test_legacy_cannot_hide_new_address(self):
  self.skills[0]['icon']='Assets/Art/atlas/default/skillIcons/wrong';self.assertIn('SKILL_ICON_IDENTITY',self.codes())
 def test_new_type_named_source_rejected(self):self.records['Assets/Art/atlasSource/default/skillIcons/basic.png']={'guid':'d'*32};self.assertIn('SKILL_ICON_ASSET_IDENTITY',self.codes())
 def test_atlas_cannot_replace_independent_mode(self):self.skills[0]['icon']='Assets/Art/atlas/default/skillIcons/210101';self.records={'Assets/Art/atlasSource/default/skillIcons/210101.png':{'guid':'e'*32}};self.assertIn('SKILL_ICON_IDENTITY',self.codes());self.assertIn('SKILL_ICON_ASSET_IDENTITY',self.codes())
 def test_missing_authority_table(self):
  p=self.root/self.rule['skillsFile'];p.parent.mkdir(parents=True);p.write_text('[]',encoding='utf-8');self.assertEqual('SKILL_ICON_TABLES',audit_skill_icons(self.root,{'skillIconGovernance':self.rule},self.records)[0]['code'])
 def test_view_type_mapping_rejected(self):
  self.codes();p=self.root/'Assets/Scripts/view.cs';p.parent.mkdir(parents=True);p.write_text('const string p="skill-v11-";',encoding='utf-8');self.records['Assets/Scripts/view.cs']={'guid':'f'*32};self.assertIn('SKILL_ICON_VIEW_REMAP',{x['code'] for x in audit_skill_icons(self.root,{'skillIconGovernance':self.rule},self.records)})
 def test_shared_reference_requires_explicit_owner(self):
  self.cards.append(dict(id=2102));self.skills.append(dict(id=210201,icon=self.path));self.entries.append(dict(cardId=2102,skillId=210201));self.assertIn('SKILL_ICON_IDENTITY',self.codes());self.rule['sharedIcons']=[dict(address=self.path,ownerCardId=2101,ownerSkillId=210101,consumers=[[2102,210201]],basis='Compatible importer and same approved resource')];self.assertFalse(self.codes())

if __name__=='__main__':unittest.main(verbosity=2)
