import unittest,copy,tempfile,hashlib,json
from pathlib import Path
import relocation_rules as rr
import pack_rules as pr

class RelocationTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.project=Path(self.temp.name)
  assets=[];rows=[];entries=[]
  for i,source in enumerate(sorted({rr.SOURCE_ART+x for x in rr.ART_NAMES}|{rr.SOURCE_DATA})):
   target,key=rr.expected_target(source);guid=hashlib.md5(source.encode()).hexdigest();f=self.project/source;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(('asset:'+source).encode());meta=Path(str(f)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n','utf8')
   before=dict(group='uiMain',address=source,labels=[key]);after=dict(group='default',address=target,labels=[key])
   assets.append(dict(guid=guid,source=source,target=target,sha256=rr.digest(f),metaSha256=rr.digest(meta),beforeEntry=before,afterEntry=after));rows.append(dict(guid=guid,state='target',target=dict(path=source,group='uiMain',bundleKey=key),transitionEvidence='old-batch/hash',targetHistory=[]));entries.append(dict(guid=guid,**before))
  self.manifest=dict(records=rows,resourceRelocation=dict(revision=rr.REVISION,scopeId='reloc',batchId='RELOC-01',approvalEvidence='explicit-user-approval',assets=assets,planSha256='a'*64),testScopes={'reloc':{'allowedGuids':[a['guid'] for a in assets]}},groupModes={'default':'PackTogetherByLabel'},currentGroupModes={'Default Local Group':'PackSeparately'})
  self.plan=dict(kind='resource-relocation',batchId='RELOC-01',approvalEvidence='explicit-user-approval',testScope='reloc',moves=[{k:a[k] for k in ['guid','source','target','sha256','metaSha256']} for a in assets],packTransitionGuids=[a['guid'] for a in assets],deletes=[]);self.entries=entries
 def preflight(self):return rr.preflight(self.project,self.manifest,self.plan,self.entries,'a'*64)
 def reject(self):self.assertRaises(ValueError,self.preflight)
 def item(self):return self.manifest['resourceRelocation']['assets'][0]
 def make_after(self,row,item):
  row['targetHistory'].append({'target':copy.deepcopy(row['target'])});row['target']={'path':item['target'],'group':'default','bundleKey':item['afterEntry']['labels'][0]};row['transitionEvidence']='RELOC-01/'+'a'*64
 def test_valid_before_preflight(self):self.assertEqual(self.preflight()['moveCount'],11)
 def test_all_after_metadata_keeps_original_provenance(self):
  for row,item in zip(self.manifest['records'],self.manifest['resourceRelocation']['assets']):self.make_after(row,item)
  self.assertIsNotNone(rr.validate_contract(self.manifest));self.assertTrue(all(rr.active(self.manifest,a['guid']) for a in self.manifest['resourceRelocation']['assets']))
 def test_before_rows_not_promoted_to_new_domain(self):self.assertFalse(rr.active(self.manifest,self.item()['guid']))
 def test_partial_after_is_rejected(self):self.make_after(self.manifest['records'][0],self.item());self.reject()
 def test_no_hold_activation(self):self.manifest['records'][0]['state']='hold';self.reject()
 def test_no_current_state_transition_through_relocation(self):self.manifest['records'][0]['state']='current';self.reject()
 def test_wrong_guid_duplicate_rejected(self):self.item()['guid']=self.manifest['resourceRelocation']['assets'][1]['guid'];self.reject()
 def test_missing_pair_member_rejected(self):self.manifest['resourceRelocation']['assets'].pop();self.reject()
 def test_extra_asset_rejected(self):self.manifest['resourceRelocation']['assets'].append(copy.deepcopy(self.item()));self.reject()
 def test_wrong_source_outside_exact_names(self):self.item()['source']=rr.SOURCE_ART+'formationBackground/new.png';self.reject()
 def test_wrong_target_outside_default_mainview(self):self.item()['target']='Assets/Art/frameAnimation/default/other.png';self.reject()
 def test_wrong_old_group_rejected(self):self.item()['beforeEntry']['group']='common';self.reject()
 def test_wrong_new_group_rejected(self):self.item()['afterEntry']['group']='common';self.reject()
 def test_changed_bundle_label_rejected(self):self.item()['afterEntry']['labels']=['ui_other'];self.reject()
 def test_empty_bundle_label_rejected(self):self.item()['afterEntry']['labels']=[];self.reject()
 def test_source_hash_change_rejected(self):(self.project/self.item()['source']).write_bytes(b'changed');self.reject()
 def test_source_meta_change_rejected(self):Path(str(self.project/self.item()['source'])+'.meta').write_text('guid: '+'b'*32+'\n','utf8');self.reject()
 def test_source_missing_rejected(self):(self.project/self.item()['source']).unlink();self.reject()
 def test_target_occupied_rejected(self):
  f=self.project/self.item()['target'];f.parent.mkdir(parents=True);f.write_bytes(b'occupied');self.reject()
 def test_meta_only_target_occupied_rejected(self):
  f=Path(str(self.project/self.item()['target'])+'.meta');f.parent.mkdir(parents=True);f.write_bytes(b'occupied');self.reject()
 def test_deleted_scope_forbidden(self):self.plan['deletes']=[self.item()['source']];self.reject()
 def test_unrelated_regrouping_forbidden(self):self.plan['regroupings']=[{'guid':'b'*32}];self.reject()
 def test_plan_hash_drift_rejected(self):self.assertRaises(ValueError,rr.preflight,self.project,self.manifest,self.plan,self.entries,'b'*64)
 def test_move_source_freeze_drift_rejected(self):self.plan['moves'][0]['sha256']='b'*64;self.reject()
 def test_aa_before_drift_rejected(self):self.entries[0]['labels']=['changed'];self.reject()
 def test_duplicate_aa_before_rejected(self):self.entries.append(copy.deepcopy(self.entries[0]));self.reject()
 def test_unrelated_aa_entry_not_changed(self):self.entries.append(dict(guid='b'*32,group='Default Local Group',address='Assets/Untouched.prefab',labels=['main-content']));self.assertTrue(self.preflight()['planReady'])
 def test_declared_scope_diff_rejected(self):self.manifest['testScopes']['reloc']['allowedGuids'][0]='b'*32;self.reject()
 def test_registrations_scope_diff_rejected(self):self.plan['packTransitionGuids'].pop();self.reject()
 def test_existing_override_not_silently_discarded(self):self.manifest['packingOverrides']=[dict(guid=self.item()['guid'])];self.reject()
 def test_legacy_default_schema_unchanged(self):self.manifest['currentGroupModes']['Default Local Group']='PackTogetherByLabel';self.reject()
 def test_final_default_schema_cannot_be_together(self):self.manifest['groupModes']['default']='PackTogether';self.reject()
 def test_after_missing_history_rejected(self):
  for row,item in zip(self.manifest['records'],self.manifest['resourceRelocation']['assets']):self.make_after(row,item)
  self.manifest['records'][0]['targetHistory']=[];self.assertRaises(ValueError,rr.validate_contract,self.manifest)
 def test_after_wrong_evidence_rejected(self):
  for row,item in zip(self.manifest['records'],self.manifest['resourceRelocation']['assets']):self.make_after(row,item)
  self.manifest['records'][0]['transitionEvidence']='other/hash';self.assertRaises(ValueError,rr.validate_contract,self.manifest)
 def test_old_plan_cannot_reapply_after_commit(self):
  for row,item in zip(self.manifest['records'],self.manifest['resourceRelocation']['assets']):self.make_after(row,item)
  self.reject()
 def test_two_exact_default_keys_use_labels(self):
  for key in rr.KEYS:self.assertEqual(pr.labels_for('default',key),[key])
 def test_other_default_keys_not_implicitly_enabled(self):self.assertEqual(pr.labels_for('default','unapproved-label'),[])
 def test_migrated_native_ui_page_cannot_pull_other_page(self):
  self.assertTrue(pr.dependency_violation(dict(group='default',bundleKey='ui_formationBackground',domainRole='ui-page',domainApplied=True),dict(group='default',bundleKey='ui_cultivation',domainRole='ui-page')))

if __name__=='__main__':unittest.main()
