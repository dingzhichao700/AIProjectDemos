"""Verify regenerated technical bytes against the current producer outputs, not migration hashes."""
import hashlib,json,re
from pathlib import Path
from managed_pe_identity import aot_codeview_equivalence

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def verified_outputs(project,manifest):
 root=Path(project);stage=manifest.get('stageContract',{});producer=stage.get('producer','')
 if producer!='Assets/Editor/FrameworkHotUpdateBuild.cs' or sha(root/producer)!=stage.get('producerSha256'):raise ValueError('Frozen technical producer identity differs')
 state=json.loads((root/'Library/AIUI/hotupdate-build-state.json').read_text('utf-8-sig'))
 if not re.fullmatch(r'\d{8}T\d{9}Z',state.get('baseVersion','')) or not re.fullmatch(r'\d{8}T\d{9}Z',state.get('release','')):raise ValueError('Invalid current build identity')
 if state.get('resourceRoot')!='releases/'+state['release']+'/' or state.get('bundleRoot')!='bundles/':raise ValueError('Current build routing differs')
 settings=(root/'ProjectSettings/HybridCLRSettings.asset').read_text('utf-8-sig')
 def setting(key):
  m=re.search(r'^  '+key+r': ([^\r\n]+)$',settings,re.M)
  if not m:raise ValueError('Missing HybridCLR setting '+key)
  value=m[1]
  if '\\' in value or ':' in value or any(p in ('','..','.') for p in value.split('/')):raise ValueError('Unsafe generated output root')
  return value
 ref=root/'Assets'/setting('outputAOTGenericReferenceFile')
 text=ref.read_text('utf-8-sig');section=re.search(r'// \{\{ AOT assemblies(.*?)// \}\}',text,re.S)
 if not section:raise ValueError('Missing generated AOT assembly list')
 aot=re.findall(r'"([^"\r\n]+\.dll)"',section[1]);code=['HotUpdateCore.dll','Login.dll','Main.dll']
 if not aot or len(aot)!=len(set(aot)) or state.get('aot')!=aot or state.get('assemblies')!=code:raise ValueError('Build state and generated assembly requirements differ')
 result={}
 for kind,names,directory in [('code',code,setting('hotUpdateDllCompileOutputRootDir')),('aot',aot,setting('strippedAOTDllOutputRootDir'))]:
  for name in names:
   if not re.fullmatch(r'[A-Za-z0-9_.-]+\.dll',name):raise ValueError('Unsafe assembly name')
   source=root/directory/'WebGL'/name;target=root/'Assets/HotUpdateBytes'/kind/(name+'.bytes')
   if any(x.is_symlink() or (hasattr(x,'is_junction') and x.is_junction()) for file in (source,target) for x in (file,*file.parents) if x!=root):raise ValueError('Linked generated output')
   data=source.read_bytes()
   if len(data)<128 or data[:2]!=b'MZ':raise ValueError('Invalid generated assembly')
   offset=int.from_bytes(data[60:64],'little')
   if data[offset:offset+4]!=b'PE\0\0':raise ValueError('Invalid generated PE output: '+name)
   if sha(source)!=sha(target):
    if kind!='aot':raise ValueError('Staged code differs from generated output: '+name)
    aot_codeview_equivalence(data,target.read_bytes())
   path=target.relative_to(root).as_posix();result[path]=dict(group='hotUpdate',address='hotupdate/'+kind+'/'+name,labels=[])
 return result

def available_outputs(project,manifest):
 try:return verified_outputs(project,manifest)
 except (ValueError,OSError,KeyError,TypeError):return {}
