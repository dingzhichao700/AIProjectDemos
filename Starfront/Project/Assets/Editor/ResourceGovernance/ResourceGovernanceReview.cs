#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ResourceGovernanceReview {
    const string Pending="Library/AIUI/pending-resource-governance.json";
    [Serializable] class Request{public string requestId,python,journalRoot,output;public int processId;}
    [Serializable] class Result{public string requestId,status,error,auditReport;public int checks;}
    static ResourceGovernanceReview(){EditorApplication.update+=Poll;}
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static void Check(bool ok,string label,Result result){if(!ok)throw new Exception(label);result.checks++;}
    static void Poll(){
        if(!File.Exists(Pending)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var result=new Result();var made=new System.Collections.Generic.List<string>();string fixture=null,invalidFolder=null;
        try{
            var request=JsonUtility.FromJson<Request>(File.ReadAllText(Pending));result.requestId=request.requestId;
            if(request.processId!=System.Diagnostics.Process.GetCurrentProcess().Id||string.IsNullOrWhiteSpace(request.requestId))throw new Exception("Request identity mismatch");File.Move(Pending,Pending+".processing-"+DateTime.UtcNow.Ticks);
            ResourceGovernanceGate.ConfigureMachine(request.python,request.journalRoot);
            var audit=ResourceGovernanceGate.Audit("B0-self-check");result.auditReport=audit.reportPath;Check(audit.exitCode==0,"Real project audit failed",result);Check(audit.report.errors.Length==0,"Unexpected new errors",result);
            string atlas="Assets/Art/atlas/default/mainView.png";string atlasHash=Hash(atlas);string group="Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset";string groupHash=Hash(group);
            invalidFolder="Assets/Art/atlasSource/default/mainView/rg-fixture-"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets/Art/atlasSource/default/mainView",Path.GetFileName(invalidFolder));File.WriteAllText(invalidFolder+"/invalid.txt","owned B0 producer-gate negative fixture");AssetDatabase.ImportAsset(invalidFolder+"/invalid.txt",ImportAssetOptions.ForceSynchronousImport);
            bool packBlocked=false;try{TexturePackerImporter.TexturePackerTool.PackAtlasSourceAssetPath("Assets/Art/atlasSource/default/mainView",false);}catch(InvalidOperationException e){packBlocked=e.Message.Contains("governance blocked");}
            Check(packBlocked,"Actual TexturePacker entry did not block invalid input",result);Check(Hash(atlas)==atlasHash,"Packing gate wrote output before rejecting",result);
            bool assignBlocked=false;try{AddressablesAutoBuilder.AssignGameAddressables();}catch(InvalidOperationException e){assignBlocked=e.Message.Contains("governance blocked");}
            Check(assignBlocked,"Actual Addressables assignment entry did not block",result);Check(Hash(group)==groupHash,"AA gate mutated group before rejecting",result);
            AssetDatabase.DeleteAsset(invalidFolder);invalidFolder=null;
            var deletePlan=new ResourceMoveTransaction.Plan{schemaVersion=1,batchId="delete-rejected-"+Guid.NewGuid().ToString("N"),approvalEvidence="B0-test-only",deletes=new[]{"Assets/Art/unpack/default/card/should-not-delete.png"},moves=new[]{new ResourceMoveTransaction.Move{source="Assets/nope",target="Assets/nope2"}}};
            string deleteFile="Library/ResourceGovernance/delete-rejection.json";File.WriteAllText(deleteFile,JsonUtility.ToJson(deletePlan));bool rejected=false;try{ResourceMoveTransaction.Execute(deleteFile,request.journalRoot,()=>{});}catch(InvalidOperationException e){rejected=e.Message.Contains("Deletion");}Check(rejected,"Deletion was not rejected",result);
            string token="fixture-"+Guid.NewGuid().ToString("N");string parent="Assets/ResourceData/default/common/"+token;
            string current="Assets";foreach(string part in parent.Substring("Assets/".Length).Split('/')){string next=current+"/"+part;if(!AssetDatabase.IsValidFolder(next)){AssetDatabase.CreateFolder(current,part);made.Add(next);}current=next;}
            fixture=parent+"/source.json";File.WriteAllText(fixture,"{\"test\":\"owned temporary B0 fixture\"}");AssetDatabase.ImportAsset(fixture,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            string guid=AssetDatabase.AssetPathToGUID(fixture),hash=Hash(fixture),metaHash=Hash(fixture+".meta"),target=parent+"/moved.json";
            var plan=new ResourceMoveTransaction.Plan{schemaVersion=1,batchId=token+"-rollback",approvalEvidence="B0 isolated fixture only",affectedFiles=new[]{group},moves=new[]{new ResourceMoveTransaction.Move{source=fixture,target=target,guid=guid,sha256=hash}}};string file="Library/ResourceGovernance/transaction-test.json";File.WriteAllText(file,JsonUtility.ToJson(plan));
            bool failed=false;try{ResourceMoveTransaction.Execute(file,request.journalRoot,()=>{throw new Exception("expected fixture interruption");});}catch(Exception e){failed=e.Message=="expected fixture interruption";}
            Check(failed,"Deliberate failure did not propagate",result);Check(File.Exists(fixture)&&!File.Exists(target),"Move rollback paths",result);Check(AssetDatabase.AssetPathToGUID(fixture)==guid&&Hash(fixture)==hash&&Hash(fixture+".meta")==metaHash,"Rollback GUID/bytes/meta",result);
            var restored=ResourceMoveTransaction.InspectRecovery(Path.Combine(request.journalRoot,plan.batchId,"journal.json"));Check(restored.state=="rolled-back","Durable rollback journal",result);Check(restored.backups.Any(b=>b.path=="Assets/AddressableAssetsData/AddressableAssetSettings.asset"),"AA group transaction auto-backs-up root settings",result);
            plan.batchId=token+"-commit";File.WriteAllText(file,JsonUtility.ToJson(plan));string committed=ResourceMoveTransaction.Execute(file,request.journalRoot,()=>{Check(AssetDatabase.AssetPathToGUID(target)==guid,"Moved fixture GUID",result);});fixture=target;
            Check(ResourceMoveTransaction.InspectRecovery(committed).state=="committed","Durable commit journal",result);Check(Hash(target)==hash,"Committed fixture bytes",result);
            var final=ResourceGovernanceGate.Audit("B0-after-transaction");Check(final.exitCode==0,"Committed/rolled-back journals blocked final audit",result);result.status="passed";
        }catch(Exception e){result.status="failed";result.error=e.ToString();}
        finally{
            if(fixture!=null&&File.Exists(fixture))AssetDatabase.DeleteAsset(fixture);
            if(invalidFolder!=null&&Directory.Exists(invalidFolder))AssetDatabase.DeleteAsset(invalidFolder);
            foreach(string directory in made.AsEnumerable().Reverse())if(Directory.Exists(directory)&&Directory.GetFileSystemEntries(directory).Length==0)AssetDatabase.DeleteAsset(directory);
            File.WriteAllText("Library/AIUI/resource-governance-result.json",JsonUtility.ToJson(result,true));
        }
    }
}
#endif
