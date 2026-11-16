import struct,unittest
import managed_pe_identity as p
def fixture():
 d=bytearray(2048)
 def u16(o,v):struct.pack_into('<H',d,o,v)
 def u32(o,v):struct.pack_into('<I',d,o,v)
 d[:2]=b'MZ';u32(60,0x80);d[0x80:0x84]=b'PE\0\0';u16(0x86,1);u16(0x94,224);u16(0x98,0x10b)
 section=0x178;u32(section+12,0x2000);u32(section+16,0x600);u32(section+20,0x200)
 directory=0xf8;u32(directory+14*8,0x2000);u32(directory+14*8+4,72);u32(0x208,0x2080);u32(0x20c,0x180)
 md=0x280;d[md:md+4]=b'BSJB';u32(md+12,4);d[md+16:md+20]=b'v1\0\0';u16(md+22,2)
 u32(md+24,0x60);u32(md+28,0x40);d[md+32:md+36]=b'#~\0\0'
 u32(md+36,0xc0);u32(md+40,16);d[md+44:md+52]=b'#GUID\0\0\0'
 table=md+0x60;struct.pack_into('<Q',d,table+8,1);u32(table+24,1);u16(table+28+4,1);d[md+0xc0:md+0xd0]=bytes(range(16))
 u32(directory+6*8,0x2280);u32(directory+6*8+4,28);u32(0x480+12,2);u32(0x480+16,30);u32(0x480+20,0x2300);u32(0x480+24,0x500)
 d[0x500:0x504]=b'RSDS';d[0x504:0x514]=bytes(range(16));u32(0x514,1);d[0x518:0x51e]=b'a.pdb\0'
 return bytes(d)
class CodeViewTests(unittest.TestCase):
 def setUp(self):self.original=fixture()
 def change(self,offset):d=bytearray(self.original);d[offset]^=1;return bytes(d)
 def test_actual_parsed_codeview_guid_only_is_equivalent(self):self.assertEqual(p.aot_codeview_equivalence(self.original,self.change(0x505))['onlyField'],'IMAGE_DEBUG_TYPE_CODEVIEW.RSDS.GUID')
 def test_identical(self):self.assertTrue(p.aot_codeview_equivalence(self.original,self.original)['equivalent'])
 def test_module_mvid_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x340))
 def test_non_debug_code_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x600))
 def test_debug_age_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x514))
 def test_debug_path_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x518))
 def test_debug_signature_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x500))
 def test_invalid_debug_pointer_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x498))
 def test_invalid_clr_header_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x280))
 def test_metadata_table_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x2e0+28))
 def test_file_length_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.original+b'x')
 def test_pe_header_change_rejected(self):self.assertRaises(ValueError,p.aot_codeview_equivalence,self.original,self.change(0x90))
if __name__=='__main__':unittest.main()
