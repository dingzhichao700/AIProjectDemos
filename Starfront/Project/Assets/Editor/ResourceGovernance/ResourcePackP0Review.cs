#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>Owned throwaway fixtures only; restores manifest, lock, AA and created folders in finally.</summary>
public static class ResourcePackP0Review {
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks++;}
    static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static void WriteManifest(JObject data){File.WriteAllText(ResourcePackRouting.ManifestPath,data.ToString());var locked=JObject.Parse(File.ReadAllText(ResourcePackRouting.LockPath));locked["files"][ResourcePackRouting.ManifestPath]=Hash(ResourcePackRouting.ManifestPath);File.WriteAllText(ResourcePackRouting.LockPath,locked.ToString());}
    static void Folder(string path,List<string> made){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent,made);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));made.Add(path);}
    public static void Run(){
        const string requestPath="Library/ResourceGovernance/p0-request.json";
        var request=JObject.Parse(File.ReadAllText(requestPath));string output=(string)request["output"],python=(string)request["python"],journal=(string)request["journalRoot"];
        Directory.CreateDirectory(output);var result=new JObject{["requestId"]=(string)request["requestId"],["status"]="running"};
        var originals=new Dictionary<string,byte[]>();var folders=new List<string>();var owned=new List<string>();string invalid=null;bool snapshotReady=false;checks=0;
        try{
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating,"Owned Edit/idle required");
            foreach(string f in Directory.GetFiles("Assets/AddressableAssetsData","*",SearchOption.AllDirectories))originals[f.Replace('\\','/')]=File.ReadAllBytes(f);
            originals[ResourcePackRouting.ManifestPath]=File.ReadAllBytes(ResourcePackRouting.ManifestPath);originals[ResourcePackRouting.LockPath]=File.ReadAllBytes(ResourcePackRouting.LockPath);
            snapshotReady=true;
            ResourceGovernanceGate.ConfigureMachine(python,journal);
            foreach(string root in new[]{"", "Assets", "Assets/"}){var a=root==""?ResourceGovernanceGate.Audit("p0-editor"):ResourceGovernanceGate.Audit("p0-editor",root);Check(a.exitCode==0,"Real Editor full-root gate: "+root);Check(a.report.errors.Length==0,"Real Editor hard errors");}
            string group="Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset";string before=Hash(group);
            AddressablesAutoBuilder.AssignGameAddressables();Check(Hash(group)==before,"P0 assignment changed existing default group");
            AddressablesPrefabDependencyChecker.ValidateOrThrow();Check(true,"Actual native dependency producer delegated to canonical pack rules");
            string nativeReport=ResourcePackRouting.LastDependencyReport;Check(!string.IsNullOrEmpty(nativeReport)&&File.Exists(nativeReport),"Actual AssetDatabase dependency report missing");File.Copy(nativeReport,Path.Combine(output,"native-dependency-report.json"),true);
            foreach(var f in originals.Where(x=>x.Key.StartsWith("Assets/AddressableAssetsData/AssetGroups/")))Check(File.ReadAllBytes(f.Key).SequenceEqual(f.Value),"P0 changed existing group/schema: "+f.Key);
            string token=Guid.NewGuid().ToString("N");invalid="Assets/Art/atlasSource/default/mainView/p0-unclassified-"+token+".png";
            File.Copy("Assets/Art/unpack/default/skillIcons/210101.png",invalid);AssetDatabase.ImportAsset(invalid,ImportAssetOptions.ForceSynchronousImport);
            var bad=ResourceGovernanceGate.Audit("p0-new-old-root");Check(bad.exitCode==2&&bad.report.errors.Any(e=>e.code=="PACK_UNCLASSIFIED"),"New default resource did not fail classification");
            string atlas="Assets/Art/atlas/default/mainView.png",atlasHash=Hash(atlas);bool blocked=false;
            try{TexturePackerImporter.TexturePackerTool.PackAtlasSourceAssetPath("Assets/Art/atlasSource/default/mainView",false);}catch(InvalidOperationException){blocked=true;}
            Check(blocked&&Hash(atlas)==atlasHash,"Actual TexturePacker producer wrote before gate");
            blocked=false;try{AddressablesAutoBuilder.AssignGameAddressables();}catch(InvalidOperationException){blocked=true;}
            Check(blocked&&Hash(group)==before,"Actual AA producer changed group before rejecting");
            AssetDatabase.DeleteAsset(invalid);invalid=null;
            string parent="Assets/ResourceData/uiDebug/gm/p0-fixture-"+token;Folder(parent,folders);
            string fixture=parent+"/source.json";File.WriteAllText(fixture,"{\"fixture\":true}");AssetDatabase.ImportAsset(fixture,ImportAssetOptions.ForceSynchronousImport);owned.Add(fixture);
            string guid=AssetDatabase.AssetPathToGUID(fixture),sourceHash=Hash(fixture),metaHash=Hash(fixture+".meta");
            var data=JObject.Parse(File.ReadAllText(ResourcePackRouting.ManifestPath));
            var row=new JObject{["guid"]=guid,["state"]="current",["batch"]="P0TEST",["basis"]="P0 owned diagnostic fixture only",["migratable"]=true,["current"]=new JObject{["path"]=fixture,["entries"]=new JArray()},["target"]=new JObject{["path"]=fixture,["group"]="uiDebug",["bundleKey"]="ui_gm"}};
            ((JArray)data["records"]).Add(row);WriteManifest(data);
            var plan=new ResourceMoveTransaction.Plan{schemaVersion=1,batchId="P0TEST-"+token+"-registration-rollback",approvalEvidence="P0 self-check fixture only; no production migration",moves=new ResourceMoveTransaction.Move[0],packTransitionGuids=new[]{guid}};
            string planPath="Library/ResourceGovernance/p0-fixture-plan.json";File.WriteAllText(planPath,JsonUtility.ToJson(plan,true));string manifestHash=Hash(ResourcePackRouting.ManifestPath),lockHash=Hash(ResourcePackRouting.LockPath);
            blocked=false;try{ResourceMoveTransaction.Execute(planPath,journal,()=>{
                AddressablesAutoBuilder.AssignGameAddressables();Check(AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid)?.parentGroup.Name=="uiDebug","Fixture target group not assigned");
                string otherDir=Path.Combine(journal,"P0TEST-"+token+"-other-fence");Directory.CreateDirectory(otherDir);string other=Path.Combine(otherDir,"journal.json");
                try{File.WriteAllText(other,JsonUtility.ToJson(new ResourceMoveTransaction.Journal{project=ResourceGovernanceGate.ProjectRoot,batchId="P0TEST-other-fence",state="recovery-required"}));var fence=ResourceGovernanceGate.Audit("p0-other-journal-fence");Check(fence.exitCode==2&&fence.report.errors.Any(e=>e.code=="UNFINISHED_TRANSACTION"),"Producer scope hid another unfinished transaction");}
                finally{File.WriteAllText(other,JsonUtility.ToJson(new ResourceMoveTransaction.Journal{project=ResourceGovernanceGate.ProjectRoot,batchId="P0TEST-other-fence",state="rolled-back",error="Owned diagnostic fence resolved; no resource operations"}));}
                throw new Exception("expected P0 rollback");
            });}catch(Exception e){if(e.Message!="expected P0 rollback")throw;blocked=true;}
            Check(blocked,"Registration-only deliberate failure missing");Check(Hash(ResourcePackRouting.ManifestPath)==manifestHash&&Hash(ResourcePackRouting.LockPath)==lockHash,"Rollback did not restore pack state and lock");
            Check(AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid)==null,"Rollback left fixture registration");
            var restored=ResourceMoveTransaction.InspectRecovery(Path.Combine(journal,plan.batchId,"journal.json"));Check(restored.state=="rolled-back","Registration rollback journal not terminal");Check(restored.backups.Any(b=>b.path==ResourcePackRouting.ManifestPath)&&restored.backups.Any(b=>b.path==ResourcePackRouting.LockPath),"State/lock not in durable backups");
            plan.batchId="P0TEST-"+token+"-registration-commit";File.WriteAllText(planPath,JsonUtility.ToJson(plan,true));string committed=ResourceMoveTransaction.Execute(planPath,journal,()=>AddressablesAutoBuilder.AssignGameAddressables());
            Check(ResourceMoveTransaction.InspectRecovery(committed).state=="committed","Registration commit failed");
            var current=JObject.Parse(File.ReadAllText(ResourcePackRouting.ManifestPath));Check(((JArray)current["records"]).Any(r=>(string)r["guid"]==guid&&(string)r["state"]=="target"),"Target state not committed");
            Check(Hash(fixture)==sourceHash&&Hash(fixture+".meta")==metaHash,"Registration-only modified resource bytes/meta");
            var valid=ResourceGovernanceGate.Audit("p0-target-commit");Check(valid.exitCode==0,"Committed target state not accepted");
            // A moved fixture exercises the original MoveAsset path and new exact-state guard.
            string moveSource=parent+"/move-source.json",moveTarget=parent+"/move-target.json";File.WriteAllText(moveSource,"{\"moveFixture\":true}");AssetDatabase.ImportAsset(moveSource,ImportAssetOptions.ForceSynchronousImport);owned.Add(moveSource);owned.Add(moveTarget);
            string moveGuid=AssetDatabase.AssetPathToGUID(moveSource),moveHash=Hash(moveSource);var moveRow=(JObject)row.DeepClone();moveRow["guid"]=moveGuid;moveRow["current"]["path"]=moveSource;moveRow["target"]["path"]=moveTarget;moveRow["target"]["group"]=null;moveRow["target"]["bundleKey"]=null;
            ((JArray)current["records"]).Add(moveRow);WriteManifest(current);
            var movePlan=new ResourceMoveTransaction.Plan{schemaVersion=1,batchId="P0TEST-"+token+"-move-rollback",approvalEvidence="P0 owned fixture only",moves=new[]{new ResourceMoveTransaction.Move{source=moveSource,target=moveTarget,guid=moveGuid,sha256=moveHash}}};File.WriteAllText(planPath,JsonUtility.ToJson(movePlan,true));
            blocked=false;try{ResourceMoveTransaction.Execute(planPath,journal,()=>{throw new Exception("expected P0 move rollback");});}catch(Exception e){if(e.Message!="expected P0 move rollback")throw;blocked=true;}
            Check(blocked&&File.Exists(moveSource)&&!File.Exists(moveTarget),"MoveAsset rollback paths");Check(AssetDatabase.AssetPathToGUID(moveSource)==moveGuid&&Hash(moveSource)==moveHash,"MoveAsset rollback identity");
            movePlan.batchId="P0TEST-"+token+"-move-commit";File.WriteAllText(planPath,JsonUtility.ToJson(movePlan,true));ResourceMoveTransaction.Execute(planPath,journal,()=>{});Check(!File.Exists(moveSource)&&File.Exists(moveTarget)&&AssetDatabase.AssetPathToGUID(moveTarget)==moveGuid,"MoveAsset commit identity");
            // Reusing committed state is rejected rather than treating approval as reusable permission.
            blocked=false;try{ResourceMoveTransaction.Execute(planPath,journal,()=>{});}catch(InvalidOperationException){blocked=true;}Check(blocked,"Committed transition was reusable");
            var deletePlan=new ResourceMoveTransaction.Plan{schemaVersion=1,batchId="P0TEST-delete",approvalEvidence="fixture",moves=new ResourceMoveTransaction.Move[0],deletes=new[]{fixture}};File.WriteAllText(planPath,JsonUtility.ToJson(deletePlan));blocked=false;try{ResourceMoveTransaction.Execute(planPath,journal,()=>{});}catch(InvalidOperationException){blocked=true;}Check(blocked&&File.Exists(fixture),"Deletion was not separately blocked");
            result["status"]="passed";
        }catch(Exception e){result["status"]="failed";result["error"]=e.ToString();}
        finally{
            if(invalid!=null&&File.Exists(invalid))AssetDatabase.DeleteAsset(invalid);
            foreach(string f in owned)if(File.Exists(f))AssetDatabase.DeleteAsset(f);
            if(snapshotReady){
            foreach(string f in Directory.GetFiles("Assets/AddressableAssetsData","*",SearchOption.AllDirectories).Select(x=>x.Replace('\\','/')).Where(f=>!originals.ContainsKey(f)&&!f.EndsWith(".meta")).ToArray())AssetDatabase.DeleteAsset(f);
            foreach(var f in originals.Where(x=>x.Key.StartsWith("Assets/")&&!x.Key.EndsWith(".meta"))){var obj=AssetDatabase.LoadMainAssetAtPath(f.Key);if(obj!=null)EditorUtility.ClearDirty(obj);}
            foreach(var pair in originals)File.WriteAllBytes(pair.Key,pair.Value);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string d in folders.AsEnumerable().Reverse())if(Directory.Exists(d)&&Directory.GetFileSystemEntries(d).Length==0)AssetDatabase.DeleteAsset(d);
            try{
                foreach(var pair in originals)Check(File.Exists(pair.Key)&&File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value),"Final config bytes changed: "+pair.Key);
                var final=ResourceGovernanceGate.Audit("p0-final-restored");Check(final.exitCode==0,"Restored production audit failed");result["finalAudit"]=final.reportPath;
                Check(!owned.Any(File.Exists),"Owned temporary asset remained");
            }catch(Exception e){result["status"]="failed";result["restoreError"]=e.ToString();}
            }
            result["checks"]=checks;File.WriteAllText(Path.Combine(output,"editor-result.json"),result.ToString());
        }
        if((string)result["status"]!="passed")throw new Exception("P0 Editor self-check failed; inspect "+output);
        Debug.Log("[RESOURCE-PACK-P0] passed "+checks+" checks; production resources unchanged");
    }
}
#endif
