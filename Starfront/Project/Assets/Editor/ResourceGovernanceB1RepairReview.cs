#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

[InitializeOnLoad]
internal static class ResourceGovernanceB1RepairReview {
    const string Pending="Library/AIUI/pending-resource-governance-b1-repair.json";
    const string Policy="ProjectSettings/ResourceGovernance.json",Lock="Tools/ResourceGovernance/ResourceGovernance.lock.json";
    [Serializable] class Request {public string requestId,planPath,outputDirectory,workbook,dotnetDirectory,expectedWorkbookHash;public int processId;}
    [Serializable] class OldSource {public string path,guid,replacementGuid;}
    [Serializable] class RepairPlan {public OldSource[] oldSourcesRetained;}
    [Serializable] class Result {public string requestId,status,error,journal;public int checks,moved,addresses,removedUnusedRegistrations;}
    static ResourceGovernanceB1RepairReview(){EditorApplication.update+=Poll;}
    static string Hash(string p){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static void Check(bool ok,string message,Result r){if(!ok)throw new Exception(message);r.checks++;}
    static void Poll(){
        if(!File.Exists(Pending)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        Request q=null;var r=new Result();byte[] sourceBefore=null,policyBefore=null,lockBefore=null;
        try{
            q=JsonUtility.FromJson<Request>(File.ReadAllText(Pending));r.requestId=q.requestId;
            if(q.processId!=System.Diagnostics.Process.GetCurrentProcess().Id||q.requestId!="RESOURCE-GOVERNANCE-01-B1-01-R2")throw new Exception("Wrong request identity");
            File.Move(Pending,Pending+".processing-"+DateTime.UtcNow.Ticks);
            var plan=JsonUtility.FromJson<ResourceMoveTransaction.Plan>(File.ReadAllText(q.planPath));var repair=JsonUtility.FromJson<RepairPlan>(File.ReadAllText(q.planPath));
            Check(plan.moves.Length==4&&Hash(q.workbook)==q.expectedWorkbookHash,"Reviewed four-image batch and original source table",r);
            sourceBefore=File.ReadAllBytes(q.workbook);policyBefore=File.ReadAllBytes(Policy);lockBefore=File.ReadAllBytes(Lock);
            foreach(var group in AddressableAssetSettingsDefaultObject.Settings.groups.Where(g=>g!=null)){
                EditorUtility.ClearDirty(group);AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(group),ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);group.OnAfterDeserialize();
            }
            AddressableAssetSettingsDefaultObject.Settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.BatchModification,null,false,false);
            string[] metaHashes=plan.moves.Select(x=>Hash(x.source+".meta")).ToArray();
            r.journal=ResourceMoveTransaction.Execute(q.planPath,ResourceGovernanceGate.JournalRoot,()=>{
                var settings=AddressableAssetSettingsDefaultObject.Settings;
                for(int i=0;i<plan.moves.Length;i++){
                    var move=plan.moves[i];Check(AssetDatabase.AssetPathToGUID(move.target)==move.guid&&Hash(move.target)==move.sha256&&Hash(move.target+".meta")==metaHashes[i],"GUID/PNG/meta preserved",r);
                    r.addresses++;r.moved++;
                }
                const string prefab="Assets/Prefab/default/mainView/CardCultivationPanel.prefab";
                string text=File.ReadAllText(prefab);
                foreach(var old in repair.oldSourcesRetained){Check(text.Contains(old.guid),"Expected old prefab default image",r);text=text.Replace(old.guid,old.replacementGuid);}
                File.WriteAllText(prefab,text);AssetDatabase.ImportAsset(prefab,ImportAssetOptions.ForceSynchronousImport);
                foreach(var old in repair.oldSourcesRetained){Check(File.Exists(old.path)&&AssetDatabase.AssetPathToGUID(old.path)==old.guid,"Old source retained, not deleted",r);r.removedUnusedRegistrations++;}
                File.Copy(Path.Combine(q.outputDirectory,"CultivationSkillResource.xlsx"),q.workbook,true);
                var info=new System.Diagnostics.ProcessStartInfo{FileName="cmd.exe",Arguments="/d /c gen_json.bat",WorkingDirectory=Path.Combine(ResourceGovernanceGate.ProjectRoot,"ConfigResource"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                Check(File.Exists(Path.Combine(q.dotnetDirectory,"dotnet.exe")),"Existing approved .NET runtime",r);
                string processPath=q.dotnetDirectory+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH");
                info.EnvironmentVariables.Remove("PATH");info.EnvironmentVariables.Remove("Path");info.EnvironmentVariables["Path"]=processPath;
                info.EnvironmentVariables["DOTNET_ROOT"]=q.dotnetDirectory;
                using(var process=System.Diagnostics.Process.Start(info)){
                    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();process.StandardInput.WriteLine();process.StandardInput.Close();
                    if(!process.WaitForExit(120000)){process.Kill();throw new TimeoutException("Original Luban script timed out");}
                    System.Threading.Tasks.Task.WaitAll(stdout,stderr);string log=stdout.Result+stderr.Result;File.WriteAllText(Path.Combine(q.outputDirectory,"formal-luban.log"),log);
                    Check(process.ExitCode==0&&!log.Contains("ERROR")&&!log.Contains("not recognized"),"Original Luban generation successful",r);
                }
                string expected=Path.Combine(q.outputDirectory,"expected-config.json");Check(Hash("Assets/ConfigBin/cfgobj_cultivationskillobj.json")==Hash(expected),"Exact isolated-generated four-address config",r);
                File.Copy(Path.Combine(q.outputDirectory,"policy-candidate.json"),Policy,true);File.Copy(Path.Combine(q.outputDirectory,"lock-candidate.json"),Lock,true);
                const string groupPath="Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset";
                var group=AssetDatabase.LoadAssetAtPath<UnityEditor.AddressableAssets.Settings.AddressableAssetGroup>(groupPath);EditorUtility.ClearDirty(group);
                File.Copy(Path.Combine(q.outputDirectory,"addressables-candidate.asset"),groupPath,true);AssetDatabase.ImportAsset(groupPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                string registered=File.ReadAllText(groupPath);
                foreach(var move in plan.moves)Check(!File.Exists(move.source)&&registered.Contains("m_Address: "+move.target)&&!registered.Contains("m_Address: "+move.source),"Final disk registration",r);
                foreach(var old in repair.oldSourcesRetained)Check(!registered.Contains("m_GUID: "+old.guid),"Old registration absent; physical file retained",r);
            });r.status="passed";
        }catch(Exception e){if(sourceBefore!=null){File.WriteAllBytes(q.workbook,sourceBefore);File.WriteAllBytes(Policy,policyBefore);File.WriteAllBytes(Lock,lockBefore);}r.status="failed";r.error=e.ToString();}
        finally{if(q!=null)File.WriteAllText(Path.Combine(q.outputDirectory,"editor-migration-result.json"),JsonUtility.ToJson(r,true));}
    }
}
#endif
