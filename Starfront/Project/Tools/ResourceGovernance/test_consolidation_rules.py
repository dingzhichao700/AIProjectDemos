"""Real transaction boundaries: AA-only entries, physical identity, history and retirement."""
import base64,collections,copy,hashlib,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import consolidation_rules as gc
import pack_rules as pr
import resource_governance as rg

class ConsolidationTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name)
  specs=[]
  def add(group,source,target,key=None,address=None):specs.append((group,source,target,key,address))
  for name in sorted(gc.AOT):
   p='Assets/HotUpdateBytes/aot/'+name+'.bytes';add('Foundation',p,p,None,'hotupdate/aot/'+name)
  for group in sorted(gc.CODE):
   p='Assets/HotUpdateBytes/code/'+group+'.dll.bytes';add(group,p,p,None,'hotupdate/code/'+group+'.dll')
  for ext in ['png','json']:
   p='Assets/Art/frameAnimation/cardDisplayAnimation/2101/burst/muzzle-flash.'+ext;add('cardDisplayAnimation',p,p.replace('/cardDisplayAnimation/','/default/cardDisplayAnimation/'),'display_2101_burst')
  for name in ['body.png','thruster-left.png','thruster-right.png','MedivacDisplayRig.prefab']:
   p='Assets/'+('Prefab' if name.endswith('.prefab') else 'Art/skeletalAnimation')+'/cardDisplayAnimation/2102/display/'+name;add('common',p,p.replace('/cardDisplayAnimation/','/default/cardDisplayAnimation/'),'display_2102_display')
  for category,count,key in [('skillIcons',64,'icons_skill'),('itemIcons',19,'icons_item')]:
   for i in range(count):
    p=f'Assets/Art/unpack/default/{category}/{2100+i}.png';add('icons',p,p,key)
  p='Assets/Art/shader/DeckLockedFrame.shader';add('renderCommon',p,p.replace('/shader/','/shader/default/'),'render_DeckLockedFrame')
  for name in ['power-missing','power-title']+['power-digit-'+str(i) for i in range(10)]:
   p='Assets/Art/atlasSource/default/sharedPower/'+name+'.png';add('uiCommon',p,p.replace('/atlasSource/','/unpack/'),'ui_power')
  p='Assets/Art/atlasSource/default/cardCultivation/ui-power-icon.png';add('uiCommon',p,'Assets/Art/unpack/default/sharedPower/ui-power-icon.png','ui_power')
  for name in ['skillFrameSquareRim','skillOpeningSquareMask']:
   p='Assets/Art/atlasSource/default/cardSkillDetail/'+name+'.png';add('uiCommon',p,'Assets/Art/unpack/default/skillControls/'+name+'.png','ui_skillControls')
  p='Assets/Art/material/DeckLockedFrame.mat';add('uiCommon',p,'Assets/Art/material/default/sharedRender/DeckLockedFrame.mat','ui_cardControls')
  rows=[];assets=[];self.records={};self.entries=[]
  for group,source,target,key,address in specs:
   guid=hashlib.md5(source.encode()).hexdigest();f=self.root/source;f.parent.mkdir(parents=True,exist_ok=True);payload=('asset:'+source).encode()
   if source.endswith('.png'):payload=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jk1kAAAAASUVORK5CYII=')+payload
   elif source.endswith('.json'):payload=json.dumps({'frames':{'frame':{'rotated':False}},'meta':{},'pivot':{'x':.5,'y':.5}}).encode()
   f.write_bytes(payload);meta=Path(str(f)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n','utf8')
   before=dict(group=group,address=address or source,labels=[] if group in gc.CODE|{'Foundation','icons'} else [key]);after=dict(group='hotUpdate' if group in gc.CODE|{'Foundation'} else 'default',address=address or target,labels=[] if address else [key]);old_key=key or ('aot_' if group=='Foundation' else 'code_')+Path(source).name[:-10]
   row=dict(guid=guid,state='target' if group in ['cardDisplayAnimation','common'] else 'current',batch='old',basis='approved-current-baseline',migratable=True,current=dict(path=source,entries=[copy.deepcopy(before)]),target=dict(path=source,group=group,bundleKey=old_key))
   if row['state']=='target':row.update(transitionEvidence='old/hash',targetHistory=[])
   item=dict(guid=guid,source=source,target=target,sha256=gc.digest(f),metaSha256=gc.digest(meta),beforeEntry=before,afterEntry=after,beforeRecord=dict(state=row['state'],currentPath=source,target=copy.deepcopy(row['target'])),beforeOverrides=[])
   rows.append(row);assets.append(item);self.entries.append(dict(guid=guid,**before));self.records[source]={'guid':guid}
  scope=dict(revision=gc.REVISION,scopeId='nine',batchId='NINE-01',approvalEvidence='explicit-user-scope',planSha256='a'*64,groups=list(gc.COUNTS),assets=assets)
  self.manifest=dict(schemaVersion=1,approvalId='RESOURCE-PACK/R1',groupModes=gc.expected_modes({'groupConsolidation':scope},pr.MODES),currentGroupModes={'Default Local Group':'PackSeparately'},groupModules={},records=rows,groupConsolidation=scope,testScopes={'nine':{'allowedGuids':[a['guid'] for a in assets]}})
  self.policy={'packGovernance':{'schemaVersion':1,'manifest':pr.MANIFEST}}
  self.plan=dict(schemaVersion=1,kind='group-consolidation',batchId='NINE-01',approvalEvidence='explicit-user-scope',testScope='nine',packTransitionGuids=[a['guid'] for a in assets],moves=[{k:a[k] for k in ['guid','source','target','sha256','metaSha256']} for a in assets if a['source']!=a['target']],deletes=[])
 def item(self,group='icons'):return next(a for a in self.manifest['groupConsolidation']['assets'] if a['beforeEntry']['group']==group)
 def preflight(self):return gc.preflight(self.root,self.manifest,self.plan,self.entries,'a'*64)
 def reject(self):self.assertRaises(ValueError,self.preflight)
 def save(self):
  f=self.root/pr.MANIFEST;f.parent.mkdir(parents=True,exist_ok=True);f.write_text(json.dumps(self.manifest),'utf8')
 def after(self,physical=True):
  for row,item in zip(self.manifest['records'],self.manifest['groupConsolidation']['assets']):
   row.setdefault('targetHistory',[]).append({'target':copy.deepcopy(row['target'])});row['target']=dict(path=item['target'],group=item['afterEntry']['group'],bundleKey=item['afterEntry']['labels'][0] if item['afterEntry']['labels'] else item['beforeRecord']['target']['bundleKey']);row.update(state='target',transitionEvidence='NINE-01/'+'a'*64)
   for old in item['beforeOverrides']:self.manifest.setdefault('packingOverrideHistory',[]).append({'supersededBy':'NINE-01','override':copy.deepcopy(old)})
   if physical and item['source']!=item['target']:
    src=self.root/item['source'];dst=self.root/item['target'];dst.parent.mkdir(parents=True,exist_ok=True);src.rename(dst);Path(str(src)+'.meta').rename(Path(str(dst)+'.meta'));self.records[item['target']]=self.records.pop(item['source'])
  self.manifest['packingOverrides']=[];self.entries=[dict(guid=a['guid'],**a['afterEntry']) for a in self.manifest['groupConsolidation']['assets']]
 def add_override(self):
  item=self.item('uiCommon');old=dict(guid=item['guid'],path=item['source'],assetSha256=item['sha256'],purpose='shared-dependency',entry=item['beforeEntry'],evidence='old-proof');item['beforeOverrides']=[copy.deepcopy(old)];self.manifest['packingOverrides']=[old]
 def test_valid_all118_preflight_and23_physical_moves(self):
  result=self.preflight();self.assertEqual((result['consolidationCount'],result['moveCount']),(118,23));self.assertFalse(result['executed'])
 def test_twelve_technical_bytes_are_explicitly_covered(self):
  self.after();self.save();report=pr.routing_report(self.root,self.policy,self.records);technical=[i for i in report['items'] if i['path'].startswith('Assets/HotUpdateBytes/')];self.assertEqual(len(technical),12);self.assertTrue(all(i['desiredEntries'][0]['address'].startswith('hotupdate/') for i in technical));self.assertEqual(pr.audit_packs(self.root,self.policy,self.records,self.entries),[])
 def test_before_metadata_is_not_mistaken_for_active_samepath_target(self):self.assertFalse(gc.active(self.manifest,self.item()['guid']))
 def test_frozen_guid_content_meta_including_aa_only_entries(self):
  for group in ['icons','Foundation','Main']:
   with self.subTest(group=group):
    item=self.item(group);f=self.root/item['source'];data=f.read_bytes();f.write_bytes(data+b'changed');self.reject();f.write_bytes(data)
 def test_after_identity_change_is_detected(self):
  self.after();f=self.root/self.item('Main')['target'];f.write_bytes(b'drift');self.assertEqual(gc.audit_identities(self.root,self.manifest,self.records)[0]['code'],'PACK_CONSOLIDATION_IDENTITY')
 def test_meta_guid_and_importer_change_rejected(self):
  meta=Path(str(self.root/self.item()['source'])+'.meta');meta.write_text('guid: '+'b'*32+'\n','utf8');self.reject()
 def test_aa_only_entry_cannot_be_omitted(self):self.plan['packTransitionGuids'].remove(self.item()['guid']);self.reject()
 def test_aa_only_entry_cannot_be_moved(self):self.plan['moves'].append({k:self.item()[k] for k in ['guid','source','target','sha256','metaSha256']});self.reject()
 def test_new_oldgroup_entry_blocks_empty_group_claim(self):self.entries.append(dict(guid='f'*32,group='icons',address='extra',labels=[]));self.reject()
 def test_new_technical_entry_not_admitted_by_out_of_art_exception(self):
  self.entries.append(dict(guid='f'*32,group='hotUpdate',address='hotupdate/code/Extra.dll',labels=[]));self.reject();self.assertEqual(gc.audit_closed_groups(self.root,self.manifest,self.records,self.entries)[0]['code'],'PACK_HOTUPDATE_SCOPE')
 def test_unrelated_existing_group_is_preserved(self):self.entries.append(dict(guid='f'*32,group='Default Local Group',address='main',labels=['main-content']));self.assertTrue(self.preflight()['planReady'])
 def test_actual_aa_drift_or_duplicates_rejected(self):
  self.entries.append(copy.deepcopy(self.entries[0]));self.reject()
 def test_logical_assembly_aliases_cannot_be_replaced_with_bytes_path(self):
  item=self.item('Main');item['afterEntry']['address']=item['target'];self.reject()
 def test_aot_names_cannot_be_renamed(self):self.item('Foundation')['target']='Assets/HotUpdateBytes/aot/Other.dll.bytes';self.reject()
 def test_technical_new_group_requires_contract(self):
  self.save();self.manifest.pop('groupConsolidation');self.save();self.assertRaises(ValueError,pr.load_manifest,self.root,self.policy)
 def test_hotupdate_mode_and_default_schema_exact(self):
  for group,mode in [('default','PackTogether'),('hotUpdate','PackTogetherByLabel')]:
   with self.subTest(group=group):
    original=self.manifest['groupModes'][group];self.manifest['groupModes'][group]=mode;self.reject();self.manifest['groupModes'][group]=original
 def test_legacy_default_local_group_mode_must_stay_separate(self):self.manifest['currentGroupModes']['Default Local Group']='PackTogether';self.reject()
 def test_wrong_label_scope_or_guid_rejected(self):self.item()['afterEntry']['labels']=['unapproved'];self.reject()
 def test_skill_and_item_quotas_cannot_shift(self):
  item=self.item();item['source']=item['source'].replace('/skillIcons/','/itemIcons/');item['target']=item['source'];item['beforeEntry']['address']=item['source'];item['afterEntry'].update(address=item['target'],labels=['icons_item']);self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_current_target_history_cannot_be_lost(self):
  self.after();self.manifest['records'][0]['targetHistory']=[];self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_before_current_path_is_preserved(self):
  self.after();self.manifest['records'][0]['current']['path']='Assets/Other.bytes';self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_after_bundlekey_must_match_exact_target(self):
  self.after();self.manifest['records'][0]['target']['bundleKey']='changed';self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_old_override_must_be_archived_exactly(self):
  self.add_override();self.after();self.assertTrue(gc.validate_contract(self.manifest));self.manifest['packingOverrideHistory'][0]['override']['assetSha256']='b'*64;self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_unarchived_override_cannot_disappear(self):self.add_override();self.after();self.manifest['packingOverrideHistory']=[];self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_after_cannot_keep_old_override_active(self):
  self.add_override();old=copy.deepcopy(self.manifest['packingOverrides']);self.after();self.manifest['packingOverrides']=old;self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_undeclared_override_cannot_be_discarded(self):self.manifest['packingOverrides']=[{'guid':self.item()['guid']}];self.reject()
 def test_no_hold_activation(self):self.manifest['records'][0]['state']='hold';self.reject()
 def test_mixed_commit_rejected(self):
  row=self.manifest['records'][0];item=self.manifest['groupConsolidation']['assets'][0];row.update(state='target',target=dict(path=item['target'],group='hotUpdate',bundleKey=row['target']['bundleKey']),transitionEvidence='NINE-01/hash');self.reject()
 def test_plan_hash_or_physical_hash_drift_rejected(self):self.assertRaises(ValueError,gc.preflight,self.root,self.manifest,self.plan,self.entries,'b'*64)
 def test_moves_cannot_exclude_physical_asset(self):self.plan['moves'].pop();self.reject()
 def test_target_occupation_rejected(self):
  target=self.root/self.item('cardDisplayAnimation')['target'];target.parent.mkdir(parents=True);target.write_bytes(b'occupied');self.reject()
 def test_delete_and_extra_regrouping_not_authorized_by_move_contract(self):self.plan['deletes']=['Assets/Something'];self.reject()
 def test_reapply_committed_plan_rejected(self):self.after();self.reject()
 def test_retirement_requires_completed_all118(self):self.manifest['groupConsolidation']['closedGroups']=list(gc.COUNTS);self.reject()
 def test_retirement_can_only_close_exact_nine(self):
  self.after();self.manifest['groupConsolidation']['closedGroups']=list(gc.COUNTS)+['fonts'];self.assertRaises(ValueError,gc.validate_contract,self.manifest)
 def test_closed_group_asset_and_entry_resurrection_fail(self):
  self.after();self.manifest['groupConsolidation']['closedGroups']=list(gc.COUNTS);self.assertTrue(gc.validate_contract(self.manifest));f=self.root/'Assets/AddressableAssetsData/AssetGroups/icons.asset';f.parent.mkdir(parents=True);f.write_text('obsolete');findings=gc.audit_closed_groups(self.root,self.manifest,self.records,[dict(guid='f'*32,group='common')]);self.assertEqual({f['code'] for f in findings},{'PACK_CLOSED_GROUP','PACK_CLOSED_GROUP_ENTRY'})
 def test_all54_closed_group_schema_and_meta_paths_are_blocked(self):
  self.after();self.manifest['groupConsolidation']['closedGroups']=list(gc.COUNTS);paths=gc.closed_group_paths(gc.COUNTS);self.assertEqual(len(set(paths)),54)
  for path in paths:
   with self.subTest(path=path):
    f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_text('recreated');findings=gc.audit_closed_groups(self.root,self.manifest,self.records,self.entries);self.assertEqual([(x['code'],x['path']) for x in findings],[('PACK_CLOSED_GROUP',path)]);f.unlink()
 def test_unclosed_group_and_unrelated_schemas_remain_allowed(self):
  paths=gc.closed_group_paths(['icons','fonts','default'])
  for path in paths:
   f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_text('existing allowed')
  self.assertEqual(gc.audit_closed_groups(self.root,self.manifest,self.records,self.entries),[])
  self.after();self.manifest['groupConsolidation']['closedGroups']=list(gc.COUNTS)
  for path in gc.closed_group_paths(['icons']):(self.root/path).unlink()
  self.assertEqual(gc.audit_closed_groups(self.root,self.manifest,self.records,self.entries),[])
 def hotupdate_schema(self,mode='1',build='remote-build',load='remote-load'):
  self.manifest['remoteProfileIds']={'build':'remote-build','load':'remote-load'};f=self.root/'Assets/AddressableAssetsData/AssetGroups/Schemas/hotUpdate_BundledAssetGroupSchema.asset';f.parent.mkdir(parents=True,exist_ok=True);f.write_text(f'm_BundleMode: {mode}\nm_BuildPath:\n  m_Id: {build}\nm_LoadPath:\n  m_Id: {load}\n','utf8');return f
 def test_gc_hotupdate_remote_profile_accepts_exact_build_and_load(self):
  self.after();self.hotupdate_schema();self.save();self.assertEqual(pr.audit_packs(self.root,self.policy,self.records,self.entries),[])
 def test_gc_hotupdate_rejects_build_and_load_profile_drift(self):
  self.after()
  for field in ['build','load']:
   with self.subTest(field=field):
    self.hotupdate_schema(**{field:'local-or-unapproved'});self.save();findings=pr.audit_packs(self.root,self.policy,self.records,self.entries);self.assertTrue(any(f['code']=='PACK_REMOTE' and field in f['message'] for f in findings))
 def test_gc_hotupdate_rejects_missing_remote_profile_id(self):
  self.after();f=self.hotupdate_schema();f.write_text('m_BundleMode: 1\nm_BuildPath: {}\nm_LoadPath: {}\n','utf8');self.save();self.assertEqual(sum(f['code']=='PACK_REMOTE' for f in pr.audit_packs(self.root,self.policy,self.records,self.entries)),2)
 def test_gc_hotupdate_schema_is_checked_before_first_target_transition(self):
  self.hotupdate_schema(mode='0',load='local-load');self.save();codes={f['code'] for f in pr.audit_packs(self.root,self.policy,self.records,self.entries)};self.assertTrue({'PACK_REMOTE','PACK_SCHEMA'}<=codes)
 def test_legacy_hotupdate_without_gc_contract_keeps_its_remote_policy(self):
  self.hotupdate_schema(build='local-build',load='local-load');self.manifest=dict(schemaVersion=1,approvalId='RESOURCE-PACK/R1',records=[],groupModules={},groupModes=dict(pr.MODES),currentGroupModes={'hotUpdate':'PackSeparately'});self.save();self.assertEqual(pr.audit_packs(self.root,self.policy,{},[]),[])
 def test_native_shared_ui_role_permits_cross_bundle_shader(self):
  shared=dict(group='default',bundleKey='ui_cardControls',domainRole=gc.role(self.item('uiCommon')),domainApplied=True);shader=dict(group='default',bundleKey='render_DeckLockedFrame',domainRole=gc.role(self.item('renderCommon')));self.assertFalse(pr.dependency_violation(shared,shader))
 def test_native_shared_ui_role_cannot_pull_battle_card(self):
  self.assertTrue(pr.dependency_violation(dict(group='default',bundleKey='icons_skill',domainRole='shared-ui',domainApplied=True),dict(group='cardBattle',bundleKey='card_2101',domainRole='card-exclusive')))
 def test_generic_default_labels_are_not_broadened(self):self.assertEqual(pr.labels_for('default','icons_skill'),[])
 def enable_domain(self):self.manifest['resourceDomainPolicy']={'revision':'RESOURCE-PACK-STRATEGY/R2','approvalId':'RESOURCE-PACK-STRATEGY/R2','bundleRoles':{}}
 def native(self,links):
  self.save();nodes={p:[] for p in self.records};nodes.update(links);snapshot=self.root/'snapshot.json';snapshot.write_text(json.dumps(dict(schemaVersion=1,project=str(self.root),producer='AssetDatabase.GetDependencies',nodes=nodes)),'utf8');return pr.audit_dependency_snapshot(self.root,self.policy,self.records,self.entries,snapshot)
 def test_native_graph_shared_control_can_pull_shared_shader(self):
  self.after();self.enable_domain();material=next(a for a in self.manifest['groupConsolidation']['assets'] if a['target'].endswith('.mat'))['target'];findings,stats=self.native({material:[self.item('renderCommon')['target']]});self.assertEqual(findings,[]);self.assertEqual(stats['strictRoots'],118)
 def test_native_graph_owned_icon_cannot_pull_card_battle(self):
  self.after();self.enable_domain();path='Assets/Art/frameAnimation/cardBattle/2101/idle.png';guid='f'*32;self.records[path]={'guid':guid};self.entries.append(dict(guid=guid,group='cardBattle',address=path,labels=['card_2101']));findings,stats=self.native({self.item()['target']:[path]});self.assertTrue(any(f['code']=='PACK_NATIVE_DEPENDENCY' for f in findings))
 def test_closed_group_new_routing_is_blocked_and_not_reinitialized(self):
  self.after();self.enable_domain();self.manifest['groupConsolidation']['closedGroups']=list(gc.COUNTS);path='Assets/Art/unpack/default/skillIcons/99999.png';self.records[path]={'guid':'f'*32};self.save();self.assertTrue(any(f['code']=='PACK_CLOSED_GROUP_ROUTE' for f in pr.audit_packs(self.root,self.policy,self.records,self.entries)));self.assertRaises(ValueError,pr.routing_report,self.root,self.policy,self.records)
 def test_passive_legacy_target_name_does_not_require_closed_group(self):
  self.after();self.manifest['groupConsolidation']['closedGroups']=list(gc.COUNTS);path='Assets/Prefab/default/Legacy.prefab';guid='f'*32;f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_text('legacy');self.records[path]={'guid':guid};entry=dict(group='Default Local Group',address=path,labels=['main-content']);self.entries.append(dict(guid=guid,**entry));self.manifest['records'].append(dict(guid=guid,state='current',batch='old',basis='legacy untouched',current=dict(path=path,entries=[entry]),target=dict(path=path,group='common',bundleKey='legacy_pending')));self.save();self.assertEqual(pr.audit_packs(self.root,self.policy,self.records,self.entries),[])
 def test_cli_preflight_routes_to_exact_contract(self):
  with patch.object(rg,'load_manifest',return_value=self.manifest),patch.object(rg,'load_json',return_value={}),patch.object(rg,'aa_entries',return_value=self.entries):self.assertEqual(rg.preflight_plan(self.root,self.plan,'a'*64)['consolidationCount'],118)
 def source_policy(self):
  return dict(schemaVersion=1,toolVersion=rg.VERSION,groups=sorted(rg.OLD_GROUPS),packGovernance=self.policy['packGovernance'],protectedVendorRoots=[],globalResourceRoots=[],modules=['mainView'],artTypes=['atlasSource','atlas','unpack','frameAnimation','skeletalAnimation','material','shader'],bootstrapResources=[],builtInVirtualGuids=[],skeletalExtensions=['.png','.asset','.anim','.controller'],audioExtensions=[],generatedAddressRules=[dict(assetPrefix='Assets/HotUpdateBytes/'+kind+'/',stripSuffix='.bytes',addressPrefix='hotupdate/'+kind+'/') for kind in ['code','aot']],duplicateReviewGroups=[])
 def source_audit(self,policy):
  self.save();groups=collections.defaultdict(list)
  for e in self.entries:groups[e['group']].append(e)
  for group,entries in groups.items():
   f=self.root/'Assets/AddressableAssetsData/AssetGroups'/(group+'.asset');f.parent.mkdir(parents=True,exist_ok=True);f.write_text('  m_GroupName: '+group+'\n  m_SerializeEntries:\n'+''.join('  - m_GUID: '+e['guid']+'\n    m_Address: '+e['address']+'\n    m_ReadOnly: 0\n    m_SerializedLabels:\n'+''.join('    - '+label+'\n' for label in e['labels']) for e in entries)+'  m_ReadOnly: 0\n','utf8')
  return rg.audit(self.root,policy)
 def duplicate_fixture(self):
  item=next(a for a in self.manifest['groupConsolidation']['assets'] if a['source'].endswith('/cardCultivation/ui-power-icon.png'));other='Assets/Art/atlasSource/default/mainView/formation-power-icon.png';f=self.root/other;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes((self.root/item['target']).read_bytes());Path(str(f)+'.meta').write_text('guid: '+'f'*32+'\n','utf8');self.records[other]={'guid':'f'*32};self.manifest['atlasBusinessLayout']=dict(revision='ART-BUSINESS-LAYOUT/R1',approval='existing-approved-layout',businesses={'default/mainView':dict(group='uiMain',bundleKey='ui_main')});group=dict(id='DUP-011',paths=[item['source'],other],owner='resource-owner',reviewBatch='B1/B5',basis='Existing byte identity review remains pending');return item,other,group
 def test_full_source_audit_accepts_exact_new_modules_and_preserves_named_duplicate(self):
  self.after();item,other,review=self.duplicate_fixture();policy=self.source_policy();policy['duplicateReviewGroups']=[review];result=self.source_audit(policy);self.assertEqual(result['errors'],[]);warning=next(f for f in result['warnings'] if f['code']=='DUPLICATE_CONTENT_REVIEW');self.assertEqual(warning['duplicateReviewId'],'DUP-011');self.assertEqual(set(warning['relatedPaths']),{item['target'],other})
 def test_full_source_audit_does_not_whitelist_new_module_siblings(self):
  self.after();path='Assets/Art/unpack/default/skillControls/unapproved.png';f=self.root/path;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes((self.root/self.item()['target']).read_bytes());Path(str(f)+'.meta').write_text('guid: '+'f'*32+'\n','utf8');result=self.source_audit(self.source_policy());self.assertTrue(any(e['code']=='UNKNOWN_MODULE' and e['path']==path for e in result['errors']))
 def test_exact_active_route_rejects_other_guid_path_and_inactive_identity(self):
  self.after();item=self.item('cardDisplayAnimation');self.assertIsNotNone(gc.approved_route(self.manifest,item['target'],item['guid']));self.assertIsNone(gc.approved_route(self.manifest,item['target'],'f'*32));self.assertIsNone(gc.approved_route(self.manifest,item['target']+'.png',item['guid']));next(r for r in self.manifest['records'] if r['guid']==item['guid'])['state']='current';self.assertIsNone(gc.approved_route(self.manifest,item['target'],item['guid']))
 def test_mapped_duplicate_review_rejects_importer_content_and_guid_drift(self):
  self.after();item,other,review=self.duplicate_fixture();self.assertTrue(gc.remapped_duplicate_reviews(self.root,self.manifest,[review],self.records));meta=Path(str(self.root/item['target'])+'.meta');meta.write_text(meta.read_text()+'importer-drift\n','utf8');self.assertFalse(gc.remapped_duplicate_reviews(self.root,self.manifest,[review],self.records))
 def test_mapped_duplicate_review_does_not_cover_a_third_or_unnamed_copy(self):
  self.after();item,other,review=self.duplicate_fixture();policy=self.source_policy();policy['duplicateReviewGroups']=[review];third='Assets/Art/atlasSource/default/mainView/extra.png';f=self.root/third;f.write_bytes((self.root/item['target']).read_bytes());Path(str(f)+'.meta').write_text('guid: '+'e'*32+'\n','utf8');result=self.source_audit(policy);self.assertTrue(any(e['code']=='DUPLICATE_CONTENT_REVIEW' for e in result['errors']));review.pop('id');self.assertFalse(gc.remapped_duplicate_reviews(self.root,self.manifest,[review],self.records))

if __name__=='__main__':unittest.main()
