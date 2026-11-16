#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>Approved move batches only. Dependency updates and verification belong in the callback.</summary>
public static class ResourceMoveTransaction {
    [Serializable] public class Move {public string source,target,guid,sha256;}
    [Serializable] public class Plan {public int schemaVersion;public string batchId,approvalEvidence,kind;public Move[] moves;public string[] affectedFiles;public string[] deletes;public string[] packTransitionGuids;}
    [Serializable] public class Step {public string source,target,guid,sha256;public bool complete;}
    [Serializable] public class Backup {public string path,file,sha256;public bool existed;}
    [Serializable] public class Journal {public int schemaVersion=1;public string batchId,project,planSha256,state,error;public Step[] steps;public Backup[] backups;}
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static void Save(string path,Journal value){string temp=path+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(value,true));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
    static string AssetPath(string value){
        if(string.IsNullOrWhiteSpace(value)||!value.StartsWith("Assets/",StringComparison.Ordinal)||value.Contains('\\')||value.Split('/').Any(p=>p==".."||p=="."||p==""||p.Contains(':')||p.EndsWith(".")||p.EndsWith(" ")))throw new InvalidOperationException("Unsafe asset path");
        string full=Path.GetFullPath(value);if(!ResourceGovernanceGate.IsWithin(full,Application.dataPath))throw new InvalidOperationException("Asset path escaped project");
        for(var current=new DirectoryInfo(Path.GetDirectoryName(full));current!=null&&ResourceGovernanceGate.IsWithin(current.FullName,Application.dataPath);current=current.Parent)if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Linked parent path");
        return value;
    }
    static void Folder(string path,List<string> created){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent,created);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));created.Add(path);}
    public static string Execute(string planPath,string journalRoot,Action updateDependenciesAndVerify){
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Move transaction needs owned Edit/idle slot");
        if(updateDependenciesAndVerify==null)throw new ArgumentNullException(nameof(updateDependenciesAndVerify));
        string root=Path.GetFullPath(journalRoot);if(ResourceGovernanceGate.IsWithin(root,ResourceGovernanceGate.ProjectRoot))throw new InvalidOperationException("Durable journals must be outside project");
        if(!string.Equals(root.TrimEnd('\\','/'),Path.GetFullPath(ResourceGovernanceGate.JournalRoot).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Transaction must use the configured shared journal root");
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(planPath));
        if(plan.schemaVersion!=1||string.IsNullOrWhiteSpace(plan.batchId)||plan.batchId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||plan.batchId==".."||string.IsNullOrWhiteSpace(plan.approvalEvidence)||(plan.moves==null||plan.moves.Length==0)&&(plan.packTransitionGuids==null||plan.packTransitionGuids.Length==0))throw new InvalidOperationException("Missing plan identity/approval/moves");
        if(plan.deletes!=null&&plan.deletes.Length!=0)throw new InvalidOperationException("Deletion execution disabled; submit exact deletion list for separate approval");
        plan.moves=plan.moves??new Move[0];
        string preflightOutput=ResourceGovernanceGate.RunCli(new[]{"--project",ResourceGovernanceGate.ProjectRoot,"--preflight-plan",Path.GetFullPath(planPath)},out int preflightExit);
        if(preflightExit!=0)throw new InvalidOperationException("Canonical move/pack preflight rejected plan: "+preflightOutput);
        var sourceSet=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var targetSet=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var move in plan.moves){
            AssetPath(move.source);AssetPath(move.target);
            if(!new[]{"Assets/Art/","Assets/Prefab/","Assets/ResourceData/"}.Any(p=>move.source.StartsWith(p,StringComparison.Ordinal))||!new[]{"Assets/Art/","Assets/Prefab/","Assets/ResourceData/"}.Any(p=>move.target.StartsWith(p,StringComparison.Ordinal)))throw new InvalidOperationException("Only approved resource roots may be moved");
            if(!sourceSet.Add(move.source)||!targetSet.Add(move.target)||!File.Exists(move.source)||File.Exists(move.target)||File.Exists(move.target+".meta")||Path.GetExtension(move.source)!=Path.GetExtension(move.target)||AssetDatabase.AssetPathToGUID(move.source)!=move.guid||Hash(move.source)!=move.sha256)throw new InvalidOperationException("Move preflight changed source/target: "+move.source);
        }
        ResourceGovernanceGate.ValidateOrThrow("before-move");
        string dir=Path.Combine(root,plan.batchId);if(Directory.Exists(dir))throw new InvalidOperationException("Batch journal already exists; inspect rather than overwrite");Directory.CreateDirectory(dir);
        var backups=new List<Backup>();var snapshot=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var move in plan.moves){snapshot.Add(move.source);snapshot.Add(move.source+".meta");}
        foreach(var path in plan.affectedFiles??new string[0]){AssetPath(path);snapshot.Add(path);snapshot.Add(path+".meta");}
        foreach(var implicitPath in ResourcePackRouting.BackupPaths(plan))snapshot.Add(implicitPath);
        if(snapshot.Any(path=>path.StartsWith("Assets/AddressableAssetsData/AssetGroups/",StringComparison.Ordinal))&&File.Exists("Assets/AddressableAssetsData/AddressableAssetSettings.asset")){snapshot.Add("Assets/AddressableAssetsData/AddressableAssetSettings.asset");snapshot.Add("Assets/AddressableAssetsData/AddressableAssetSettings.asset.meta");}
        foreach(var path in snapshot){var b=new Backup{path=path,existed=File.Exists(path),file=backups.Count.ToString("D5")+".backup"};if(b.existed){File.Copy(path,Path.Combine(dir,b.file));b.sha256=Hash(path);if(Hash(Path.Combine(dir,b.file))!=b.sha256)throw new IOException("Backup verification failed");}backups.Add(b);}
        var journal=new Journal{batchId=plan.batchId,project=ResourceGovernanceGate.ProjectRoot,planSha256=Hash(planPath),state="prepared",steps=plan.moves.Select(m=>new Step{source=m.source,target=m.target,guid=m.guid,sha256=m.sha256}).ToArray(),backups=backups.ToArray()};string journalPath=Path.Combine(dir,"journal.json");Save(journalPath,journal);var created=new List<string>();
        try{
            journal.state="moving";Save(journalPath,journal);
            foreach(var step in journal.steps){Folder(Path.GetDirectoryName(step.target).Replace('\\','/'),created);string error=AssetDatabase.MoveAsset(step.source,step.target);if(error!="")throw new IOException(error);step.complete=true;Save(journalPath,journal);if(AssetDatabase.AssetPathToGUID(step.target)!=step.guid||Hash(step.target)!=step.sha256)throw new IOException("Moved identity/bytes changed");}
            journal.state="verifying";Save(journalPath,journal);
            using(ResourceGovernanceGate.EnterTransactionVerification(journalPath)){
                if(plan.kind!="domain-regroup"&&plan.kind!="matching-admission"&&plan.kind!="resource-relocation"&&plan.kind!="group-consolidation")ResourcePackRouting.Advance(plan,journal.planSha256);updateDependenciesAndVerify();AssetDatabase.SaveAssets();
                var checkedResult=ResourceGovernanceGate.AuditInternal("verify-move",journalPath);if(checkedResult.exitCode!=0)throw new InvalidOperationException("Post-move governance validation failed: "+checkedResult.reportPath);
            }
            journal.state="committed";Save(journalPath,journal);return journalPath;
        }catch(Exception failure){
            journal.state="rolling-back";journal.error=failure.ToString();Save(journalPath,journal);var restoreErrors=new List<string>();
            foreach(var step in journal.steps.Reverse())if(step.complete){try{Folder(Path.GetDirectoryName(step.source).Replace('\\','/'),created);if(File.Exists(step.source))throw new IOException("Rollback source occupied");if(AssetDatabase.AssetPathToGUID(step.target)!=step.guid||Hash(step.target)!=step.sha256)throw new IOException("Rollback target changed; preserve for inspection");string error=AssetDatabase.MoveAsset(step.target,step.source);if(error!="")throw new IOException(error);}catch(Exception e){restoreErrors.Add(e.ToString());}}
            if(restoreErrors.Count==0){
                foreach(var b in backups.Where(b=>!b.existed&&!b.path.EndsWith(".meta",StringComparison.Ordinal)))if(File.Exists(b.path)&&!AssetDatabase.DeleteAsset(b.path))restoreErrors.Add("Cannot remove owned new output: "+b.path);
                foreach(var b in backups.Where(b=>b.existed&&!b.path.EndsWith(".meta",StringComparison.Ordinal))){var restored=AssetDatabase.LoadMainAssetAtPath(b.path);if(restored!=null)EditorUtility.ClearDirty(restored);}
                AssetDatabase.StartAssetEditing();try{foreach(var b in backups){try{if(b.existed){Directory.CreateDirectory(Path.GetDirectoryName(b.path));File.Copy(Path.Combine(dir,b.file),b.path,true);}}catch(Exception e){restoreErrors.Add(e.ToString());}}}finally{AssetDatabase.StopAssetEditing();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);}
            }
            foreach(var b in backups.Where(b=>b.existed))if(!File.Exists(b.path)||Hash(b.path)!=b.sha256)restoreErrors.Add("Restored byte mismatch: "+b.path);
            foreach(var folder in created.AsEnumerable().Reverse())if(Directory.Exists(folder)&&Directory.GetFileSystemEntries(folder).Length==0)AssetDatabase.DeleteAsset(folder);
            journal.state=restoreErrors.Count==0?"rolled-back":"recovery-required";journal.error=failure+"\n"+string.Join("\n",restoreErrors);Save(journalPath,journal);
            if(restoreErrors.Count!=0)throw new AggregateException("Transaction failed; explicit journal recovery required",failure,new IOException(journal.error));throw;
        }
    }
    public static Journal InspectRecovery(string journalPath){var journal=JsonUtility.FromJson<Journal>(File.ReadAllText(journalPath));if(journal.project!=ResourceGovernanceGate.ProjectRoot)throw new InvalidOperationException("Wrong project journal");return journal;}
}
#endif
