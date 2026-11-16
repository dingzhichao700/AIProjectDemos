#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>Consumes the sole Python semantic report; no second path-to-owner classifier.</summary>
public static class ResourcePackRouting {
    public static string LastDependencyReport {get;private set;}
    public const string ManifestPath="ProjectSettings/ResourcePackManifest.json";
    public const string LockPath="Tools/ResourceGovernance/ResourceGovernance.lock.json";
    public static bool Enabled {get{const string policy="ProjectSettings/ResourceGovernance.json";if(!File.Exists(policy))return false;var rule=JObject.Parse(File.ReadAllText(policy))["packGovernance"];return rule!=null&&rule.Type!=JTokenType.Null;}}
    static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static void Atomic(string path,string text){string temp=path+".p0-tmp";File.WriteAllText(temp,text);File.Replace(temp,path,null);}
    static JObject ValidRouting(string stage){
        var audit=ResourceGovernanceGate.Audit(stage);
        if(audit.exitCode!=0)throw new InvalidOperationException("Resource governance blocked "+stage+": "+audit.stdout+"\n"+audit.reportPath);
        var report=JObject.Parse(File.ReadAllText(audit.reportPath));var routing=report["packRouting"] as JObject;
        if(routing==null||(int?)routing["schemaVersion"]!=1)throw new InvalidOperationException("Missing canonical pack routing report");return routing;
    }
    static AddressableAssetGroup Group(AddressableAssetSettings settings,string name,string mode){
        var group=name=="Default Local Group"?settings.DefaultGroup:settings.FindGroup(name);
        if(group!=null)return group; // Existing schema changes require a declared transition, not blanket reconfiguration.
        group=settings.CreateGroup(name,false,false,false,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
        var schema=group.GetSchema<BundledAssetGroupSchema>();
        schema.BundleMode=(BundledAssetGroupSchema.BundlePackingMode)Enum.Parse(typeof(BundledAssetGroupSchema.BundlePackingMode),mode);
        schema.BuildPath.SetVariableByName(settings,AddressableAssetSettings.kRemoteBuildPath);
        schema.LoadPath.SetVariableByName(settings,AddressableAssetSettings.kRemoteLoadPath);
        schema.UseAssetBundleCache=true;schema.UseAssetBundleCrc=true;
        EditorUtility.SetDirty(schema);return group;
    }
    public static void Initialize(AddressableAssetSettings settings){
        var data=ValidRouting("addressables-assign");
        foreach(var item in (JArray)data["items"]){
            if((string)item["state"]!="target"&&(string)item["state"]!="new"&&(string)item["state"]!="packing-override")continue;
            foreach(var entry in (JArray)item["desiredEntries"]){string name=(string)entry["group"];Group(settings,name,(string)data["groupModes"][name]);}
        }
    }
    public static void AssignDeclared(AddressableAssetSettings settings){
        var data=ValidRouting("addressables-assign");
        foreach(var item in (JArray)data["items"]){
            string state=(string)item["state"],guid=(string)item["guid"];
            if(state!="target"&&state!="new"&&state!="packing-override")continue; // Preserve exact current/hold/retired/keep states.
            var desired=(JArray)item["desiredEntries"];
            if(desired.Count==0){if(state=="target")settings.RemoveAssetEntry(guid);continue;}
            if(desired.Count!=1)throw new InvalidOperationException("Multiple desired entries for "+guid);
            var record=desired[0];string name=(string)record["group"];
            var group=Group(settings,name,(string)data["groupModes"][name]);
            var entry=settings.FindAssetEntry(guid);
            if(state=="new"&&entry!=null&&entry.parentGroup!=group)throw new InvalidOperationException("Manual ownership conflict: "+guid);
            entry=settings.CreateOrMoveEntry(guid,group);entry.address=(string)record["address"];
            foreach(string label in entry.labels.ToArray())entry.SetLabel(label,false);
            foreach(var label in (JArray)record["labels"])entry.SetLabel((string)label,true,true);
        }
        AssetDatabase.SaveAssets();ResourceGovernanceGate.ValidateOrThrow("addressables-assign-post");
    }
    public static string ValidateDependencies(AddressableAssetSettings settings){
        ResourceGovernanceGate.ValidateOrThrow("native-dependency-input");
        var nodes=new JObject();var pending=new Queue<string>(settings.groups.Where(g=>g!=null).SelectMany(g=>g.entries).Select(e=>e.AssetPath).Where(p=>!string.IsNullOrEmpty(p)&&File.Exists(p)).Distinct());
        while(pending.Count>0){string path=pending.Dequeue();if(nodes[path]!=null)continue;
            string[] deps=path.StartsWith("Assets/",StringComparison.Ordinal)||path.StartsWith("Packages/",StringComparison.Ordinal)?AssetDatabase.GetDependencies(path,false).Where(p=>p!=path&&!p.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)).Distinct().ToArray():new string[0];
            nodes[path]=new JArray(deps);foreach(string p in deps)if(nodes[p]==null)pending.Enqueue(p);
        }
        string dir="Library/ResourceGovernance";Directory.CreateDirectory(dir);string file=Path.Combine(dir,"native-input-"+Guid.NewGuid().ToString("N")+".json");
        File.WriteAllText(file,new JObject{["schemaVersion"]=1,["project"]=ResourceGovernanceGate.ProjectRoot,["producer"]="AssetDatabase.GetDependencies",["nodes"]=nodes}.ToString());
        LastDependencyReport=ResourceGovernanceGate.ValidateNativeSnapshot(file);
        Debug.Log("[ResourcePack] native dependency policy passed; actual Bundle build report remains separate: "+LastDependencyReport);return LastDependencyReport;
    }
    // Called only inside ResourceMoveTransaction after durable backup and moves, before dependency callback.
    internal static IEnumerable<string> BackupPaths(ResourceMoveTransaction.Plan plan){
        if(!Enabled)yield break;
        yield return ManifestPath;yield return LockPath;
        var data=JObject.Parse(File.ReadAllText(ManifestPath));var selected=new HashSet<string>(plan.packTransitionGuids??new string[0]);
        foreach(var move in plan.moves??new ResourceMoveTransaction.Move[0])selected.Add(move.guid);
        foreach(var row in ((JArray)data["records"]).Cast<JObject>().Where(r=>selected.Contains((string)r["guid"]))){
            var names=((JArray)row["current"]["entries"]).Select(e=>(string)e["group"]).ToList();
            if(row["target"]["group"].Type!=JTokenType.Null)names.Add((string)row["target"]["group"]);
            foreach(string name in names.Distinct()){
                string group="Assets/AddressableAssetsData/AssetGroups/"+name+".asset";
                yield return group;yield return group+".meta";
                foreach(string type in new[]{"BundledAssetGroupSchema","ContentUpdateGroupSchema"}){string schema="Assets/AddressableAssetsData/AssetGroups/Schemas/"+name+"_"+type+".asset";yield return schema;yield return schema+".meta";}
            }
        }
    }
    internal static void Advance(ResourceMoveTransaction.Plan plan,string planHash){
        if(!Enabled)return;
        var data=JObject.Parse(File.ReadAllText(ManifestPath));
        var rows=((JArray)data["records"]).Cast<JObject>().ToDictionary(r=>(string)r["guid"]);
        var selected=new HashSet<string>(plan.packTransitionGuids??new string[0]);
        foreach(var move in plan.moves??new ResourceMoveTransaction.Move[0])if(rows.ContainsKey(move.guid))selected.Add(move.guid);
        if(selected.Count==0)return;
        foreach(string guid in selected){
            if(!rows.TryGetValue(guid,out var row)||(string)row["state"]!="current"||!(bool)row["migratable"]||!plan.batchId.StartsWith((string)row["batch"]+"-",StringComparison.Ordinal))throw new InvalidOperationException("Unapproved pack transition: "+guid);
            string target=(string)row["target"]["path"];
            if(AssetDatabase.AssetPathToGUID(target)!=guid)throw new InvalidOperationException("Target identity missing: "+target);
            row["state"]="target";row["transitionEvidence"]=plan.batchId+"/"+planHash;
        }
        data["phase"]="migrating";Atomic(ManifestPath,data.ToString());
        var locked=JObject.Parse(File.ReadAllText(LockPath));locked["files"][ManifestPath]=Hash(ManifestPath);Atomic(LockPath,locked.ToString());
    }
}
#endif
