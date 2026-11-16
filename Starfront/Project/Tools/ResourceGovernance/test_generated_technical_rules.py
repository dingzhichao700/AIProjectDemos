import json,tempfile,unittest,hashlib
from pathlib import Path
import generated_technical_rules as g
class GeneratedTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.p=Path(self.temp.name)
  def put(path,data):
   f=self.p/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(data);return f
  self.producer=put('Assets/Editor/FrameworkHotUpdateBuild.cs',b'approved-producer')
  self.m={'stageContract':{'producer':'Assets/Editor/FrameworkHotUpdateBuild.cs','producerSha256':g.sha(self.producer)}}
  self.state={'baseVersion':'20261116T194447373Z','release':'20261116T194447373Z','resourceRoot':'releases/20261116T194447373Z/','bundleRoot':'bundles/','assemblies':['HotUpdateCore.dll','Login.dll','Main.dll'],'aot':['System.dll']}
  self.statefile=put('Library/AIUI/hotupdate-build-state.json',json.dumps(self.state).encode())
  self.settings=put('ProjectSettings/HybridCLRSettings.asset',b'  outputAOTGenericReferenceFile: Editor/AOT.cs\n  hotUpdateDllCompileOutputRootDir: HybridCLRData/HotUpdateDlls\n  strippedAOTDllOutputRootDir: HybridCLRData/AssembliesPostIl2CppStrip\n')
  self.refs=put('Assets/Editor/AOT.cs',b'// {{ AOT assemblies\n"System.dll"\n// }}')
  payload=bytearray(160);payload[:2]=b'MZ';payload[60:64]=(128).to_bytes(4,'little');payload[128:132]=b'PE\0\0'
  for kind,names,folder in [('code',self.state['assemblies'],'HotUpdateDlls'),('aot',self.state['aot'],'AssembliesPostIl2CppStrip')]:
   for name in names:put('HybridCLRData/'+folder+'/WebGL/'+name,payload);put('Assets/HotUpdateBytes/'+kind+'/'+name+'.bytes',payload)
 def reject(self):self.assertRaises((ValueError,OSError),g.verified_outputs,self.p,self.m)
 def test_current_generated_extra_system_is_accepted(self):self.assertEqual(g.verified_outputs(self.p,self.m)['Assets/HotUpdateBytes/aot/System.dll.bytes']['address'],'hotupdate/aot/System.dll')
 def test_wrong_producer_rejected(self):self.producer.write_bytes(b'changed');self.reject()
 def test_state_and_aot_reference_mismatch_rejected(self):self.refs.write_text('// {{ AOT assemblies\n"Other.dll"\n// }}');self.reject()
 def test_duplicate_aot_rejected(self):self.refs.write_text('// {{ AOT assemblies\n"System.dll","System.dll"\n// }}');self.reject()
 def test_unknown_code_rejected(self):self.state['assemblies'].append('Extra.dll');self.statefile.write_text(json.dumps(self.state));self.reject()
 def test_changed_staged_bytes_rejected(self):(self.p/'Assets/HotUpdateBytes/code/Main.dll.bytes').write_bytes(b'drift');self.reject()
 def test_forged_non_pe_both_sides_rejected(self):
  for f in ('HybridCLRData/HotUpdateDlls/WebGL/Main.dll','Assets/HotUpdateBytes/code/Main.dll.bytes'):(self.p/f).write_bytes(b'junk'*100)
  self.reject()
 def test_missing_generated_output_rejected(self):(self.p/'HybridCLRData/AssembliesPostIl2CppStrip/WebGL/System.dll').unlink();self.reject()
 def test_escaped_source_root_rejected(self):self.settings.write_text(self.settings.read_text().replace('HybridCLRData/HotUpdateDlls','../Outside'));self.reject()
 def test_bad_base_version_rejected(self):self.state['baseVersion']='old';self.statefile.write_text(json.dumps(self.state));self.reject()
class ConsolidationGeneratedTests(unittest.TestCase):
 def setUp(self):
  from test_consolidation_rules import ConsolidationTests
  self.f=ConsolidationTests();self.f.setUp();self.addCleanup(self.f.doCleanups);self.f.after()
 def outputs(self,item):
  from unittest.mock import patch
  return patch('consolidation_rules.generated.available_outputs',return_value={item['target']:item['afterEntry']})
 def test_technical_regeneration_keeps_migration_identity(self):
  import consolidation_rules as c
  f=self.f;i=f.item('Main');(f.root/i['target']).write_bytes(b'regenerated')
  with self.outputs(i):self.assertEqual(c.audit_identities(f.root,f.manifest,f.records),[])
 def test_technical_meta_drift_still_fails(self):
  import consolidation_rules as c
  f=self.f;i=f.item('Main');Path(str(f.root/i['target'])+'.meta').write_text('changed')
  with self.outputs(i):self.assertTrue(c.audit_identities(f.root,f.manifest,f.records))
 def test_non_technical_drift_still_fails(self):
  import consolidation_rules as c
  f=self.f;i=f.item('icons');(f.root/i['target']).write_bytes(b'changed')
  with self.outputs(i):self.assertTrue(c.audit_identities(f.root,f.manifest,f.records))
 def test_generated_extra_checks_exact_alias_and_labels(self):
  import consolidation_rules as c
  from unittest.mock import patch
  f=self.f;path='Assets/HotUpdateBytes/aot/System.dll.bytes';guid='f'*32;entry=dict(group='hotUpdate',address='hotupdate/aot/System.dll',labels=[]);f.records[path]={'guid':guid};e=dict(guid=guid,**entry)
  with patch('consolidation_rules.generated.available_outputs',return_value={path:entry}):
   self.assertEqual(c.audit_closed_groups(f.root,f.manifest,f.records,f.entries+[e]),[])
   for field,value in [('address','wrong'),('labels',['unexpected'])]:
    self.assertTrue(c.audit_closed_groups(f.root,f.manifest,f.records,f.entries+[dict(e,**{field:value})]))
 def test_unknown_extra_is_still_rejected(self):
  import consolidation_rules as c
  f=self.f;self.assertTrue(c.audit_closed_groups(f.root,f.manifest,f.records,f.entries+[dict(guid='f'*32,group='hotUpdate',address='hotupdate/aot/Extra.dll',labels=[])]))
if __name__=='__main__':unittest.main()

class ProducerDebugTests(unittest.TestCase):
 def setUp(self):
  self.f=GeneratedTests();self.f.setUp();self.addCleanup(self.f.doCleanups)
 def pair(self,kind,name,offset):
  from test_managed_pe_identity import fixture
  data=fixture();changed=bytearray(data);changed[offset]^=1
  directory='HotUpdateDlls' if kind=='code' else 'AssembliesPostIl2CppStrip'
  (self.f.p/('HybridCLRData/'+directory+'/WebGL/'+name)).write_bytes(changed)
  (self.f.p/('Assets/HotUpdateBytes/'+kind+'/'+name+'.bytes')).write_bytes(data)
 def test_aot_codeview_guid_only_accepted(self):self.pair('aot','System.dll',0x505);self.assertEqual(len(g.verified_outputs(self.f.p,self.f.m)),4)
 def test_code_even_codeview_guid_change_rejected(self):self.pair('code','Main.dll',0x505);self.f.reject()
 def test_aot_module_mvid_change_rejected(self):self.pair('aot','System.dll',0x340);self.f.reject()
