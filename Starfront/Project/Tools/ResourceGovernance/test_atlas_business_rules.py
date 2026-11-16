import unittest,copy
import pack_rules as pr
import test_pack_rules as baseline

class AtlasBusinessTests(unittest.TestCase):
 write=baseline.PackTests.write
 asset=baseline.PackTests.asset
 codes=baseline.PackTests.codes
 entries=baseline.PackTests.entries
 tearDown=baseline.PackTests.tearDown
 def setUp(self):
  baseline.PackTests.setUp(self)
  self.layout={'default/common':{'group':'uiCommon','bundleKey':'ui_common'},'default/mainView':{'group':'uiMain','bundleKey':'ui_mainView'}}
  self.data['atlasBusinessLayout']={'revision':'ART-BUSINESS-LAYOUT/R1','approval':'explicit user approval fixture','businesses':self.layout}
 def domain(self):return {'atlasBusinesses':self.layout}
 def test_source_business_is_editor_only(self):
  row=pr.route('Assets/Art/atlasSource/default/common/button.png',self.domain());self.assertFalse(row['runtime']);self.assertIsNone(row['group'])
 def test_product_keeps_path_but_gets_business_group(self):
  row=pr.route('Assets/Art/atlas/default/mainView.png',self.domain());self.assertEqual((row['group'],row['bundleKey']),('uiMain','ui_mainView'))
 def test_nested_source_rejected(self):self.assertRaises(ValueError,pr.route,'Assets/Art/atlasSource/default/common/child/a.png',self.domain())
 def test_unknown_business_rejected(self):self.assertRaises(ValueError,pr.route,'Assets/Art/atlasSource/default/misc/a.png',self.domain())
 def test_unknown_product_rejected(self):self.assertRaises(ValueError,pr.route,'Assets/Art/atlas/default/misc.png',self.domain())
 def test_no_generic_scene_fallback(self):self.assertIsNone(pr.route('Assets/Art/unpack/scene/misc/a.png',self.domain()))
 def test_new_aa_group_source_directory_rejected(self):self.assertRaises(ValueError,pr.route,'Assets/Art/atlasSource/uiMain/mainView/a.png',self.domain())
 def test_new_source_unregistered_allowed(self):self.asset('Assets/Art/atlasSource/default/common/new.png');self.assertFalse(self.codes())
 def test_new_source_runtime_entry_rejected(self):
  path='Assets/Art/atlasSource/default/common/new.png';g=self.asset(path);self.assertIn('PACK_REGISTRATION',self.codes([{'guid':g,'group':'uiCommon','address':path,'labels':['ui_common']}]))
 def test_new_product_exact_business_registration(self):
  path='Assets/Art/atlas/default/mainView.png';g=self.asset(path);self.assertFalse(self.codes([{'guid':g,'group':'uiMain','address':path,'labels':['ui_mainView']}]))
 def test_new_product_default_group_rejected(self):
  path='Assets/Art/atlas/default/mainView.png';g=self.asset(path);self.assertIn('PACK_REGISTRATION',self.codes([{'guid':g,'group':'Default Local Group','address':path,'labels':[]}]))
 def test_invalid_owner_blocked(self):
  self.layout['default/common']['group']='unknown';self.assertRaises(ValueError,self.codes)

if __name__=='__main__':unittest.main()
