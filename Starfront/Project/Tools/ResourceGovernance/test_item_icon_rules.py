import unittest,tempfile,json
from pathlib import Path
from item_icon_rules import audit_item_icons
class ItemIconTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.p=Path(self.tmp.name);self.rule=dict(itemsFile='Assets/ConfigBin/items.json',mapFile='Assets/ResourceData/default/item/icon-map.json',root='Assets/Art/unpack/default/itemIcons');self.items=[dict(id=2001,iconId=2001)];self.entries=[dict(itemId=2001,content=self.rule['root']+'/2001.png')];self.records={self.entries[0]['content']:{'guid':'a'*32}}
 def tearDown(self):self.tmp.cleanup()
 def codes(self):
  for path,data in [(self.rule['itemsFile'],self.items),(self.rule['mapFile'],dict(entries=self.entries))]:
   p=self.p/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data),encoding='utf-8')
  return {x['code'] for x in audit_item_icons(self.p,{'itemIconGovernance':self.rule},self.records)}
 def test_valid_flat_item(self):self.assertFalse(self.codes())
 def test_wrong_icon_id(self):self.items[0]['iconId']=0;self.assertIn('ITEM_ICON_IDENTITY',self.codes())
 def test_wrong_content_id(self):self.entries[0]['content']=self.rule['root']+'/2002.png';self.assertIn('ITEM_ICON_ADDRESS',self.codes())
 def test_missing_image(self):self.records={};self.assertIn('ITEM_ICON_MISSING',self.codes())
 def test_nested_directory(self):self.records[self.rule['root']+'/2001/content.png']={'guid':'b'*32};self.assertIn('ITEM_ICON_LAYOUT',self.codes())
 def test_unknown_item(self):self.entries.append(dict(itemId=999,content='x'));self.assertIn('ITEM_ICON_OWNER',self.codes())
 def test_duplicate_mapping(self):self.entries.append(self.entries[0]);self.assertIn('ITEM_ICON_SCHEMA',self.codes())
 def test_quality_frame_not_an_item_icon(self):self.records[self.rule['root']+'/green-background.png']={'guid':'c'*32};self.assertIn('ITEM_ICON_LAYOUT',self.codes())
if __name__=='__main__':unittest.main(verbosity=2)
