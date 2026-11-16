#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

[InitializeOnLoad]
internal static class ResourceGovernanceB1Review {
    const string Pending="Library/AIUI/pending-resource-governance-b1.json";
    [Serializable] class Request {public string requestId,planPath,outputDirectory,expectedPolicyHash,expectedLockHash; public int processId;}
    [Serializable] class Result {public string requestId,status,error,journal; public int checks,moved,addresses,codeFiles;}
    static ResourceGovernanceB1Review(){EditorApplication.update+=Poll;}
    static string Hash(string p){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static void Check(bool ok,string message,Result r){if(!ok)throw new Exception(message);r.checks++;}
    static void Poll(){
        if(!File.Exists(Pending)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        Request q=null;var r=new Result();byte[] policyBefore=null,lockBefore=null;
        const string policyPath="ProjectSettings/ResourceGovernance.json",lockPath="Tools/ResourceGovernance/ResourceGovernance.lock.json";
        try{
            q=JsonUtility.FromJson<Request>(File.ReadAllText(Pending));r.requestId=q.requestId;
            if(q.processId!=System.Diagnostics.Process.GetCurrentProcess().Id||q.requestId!="RESOURCE-GOVERNANCE-01-B1-01-R1")throw new Exception("Wrong request/PID");
            File.Move(Pending,Pending+".processing-"+DateTime.UtcNow.Ticks);
            var plan=JsonUtility.FromJson<ResourceMoveTransaction.Plan>(File.ReadAllText(q.planPath));
            Check(plan.batchId==q.requestId&&plan.moves.Length==4,"Exact approved four-image batch",r);
            Check(Hash(policyPath)==q.expectedPolicyHash&&Hash(lockPath)==q.expectedLockHash,"Reviewed policy and lock unchanged",r);
            policyBefore=File.ReadAllBytes(policyPath);lockBefore=File.ReadAllBytes(lockPath);
            var metas=plan.moves.ToDictionary(m=>m.guid,m=>Hash(m.source+".meta"));
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            r.journal=ResourceMoveTransaction.Execute(q.planPath,ResourceGovernanceGate.JournalRoot,()=>{
                foreach(var m in plan.moves){
                    Check(AssetDatabase.AssetPathToGUID(m.target)==m.guid&&Hash(m.target)==m.sha256,"Moved identity/bytes",r);
                    Check(Hash(m.target+".meta")==metas[m.guid],"Exact importer/meta preserved",r);
                    var entry=settings.FindAssetEntry(m.guid);Check(entry!=null&&entry.address==m.source,"Expected prior AA identity",r);
                    entry.SetAddress(m.target);r.addresses++;
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(m.target);Check(sprite!=null&&sprite.rect.width>0&&sprite.rect.height>0,"Imported Sprite",r);
                    r.moved++;
                }
                const string oldPrefix="Assets/Art/atlasSource/default/cardSkillDetail/skillVisual-";
                const string newPrefix="Assets/Art/unpack/default/cardSkillDetail/skillVisual-";
                foreach(var path in plan.affectedFiles.Where(p=>p.EndsWith(".cs",StringComparison.Ordinal))){
                    string text=File.ReadAllText(path);Check(text.Contains(oldPrefix),"Expected consumer prefix",r);
                    File.WriteAllText(path,text.Replace(oldPrefix,newPrefix));r.codeFiles++;
                }
                EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
                File.Copy(Path.Combine(q.outputDirectory,"policy-candidate.json"),policyPath,true);
                File.Copy(Path.Combine(q.outputDirectory,"lock-candidate.json"),lockPath,true);
                foreach(var m in plan.moves)Check(settings.FindAssetEntry(m.guid).address==m.target&&!File.Exists(m.source),"No old asset/address",r);
            });
            r.status="passed";
        }catch(Exception e){if(policyBefore!=null){File.WriteAllBytes(policyPath,policyBefore);File.WriteAllBytes(lockPath,lockBefore);}r.status="failed";r.error=e.ToString();}
        finally{if(q!=null&&Directory.Exists(q.outputDirectory))File.WriteAllText(Path.Combine(q.outputDirectory,"editor-migration-result.json"),JsonUtility.ToJson(r,true));}
    }
}
#endif
