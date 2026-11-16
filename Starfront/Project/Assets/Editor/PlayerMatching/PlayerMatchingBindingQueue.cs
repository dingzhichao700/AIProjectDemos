#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>匹配界面专用的常驻绑定/隔离验证入口，不接业务或资源登记。</summary>
[InitializeOnLoad]
public static class PlayerMatchingBindingQueue {
    const string Pending="Library/PlayerMatching/pending-binding.json";
    const string Result="Library/PlayerMatching/binding-result.json";
    [Serializable] public class Member { public string alias,path,type; }
    [Serializable] public class Plan { public string prefab,view,script,beforeSha256; public Member[] members; }
    [Serializable] public class Definition { public string revision; public Plan[] plans; }
    [Serializable] public class Request { public string requestId,action,planPath; }
    [Serializable] public class Response { public string requestId,status,error; public int prefabs,schemas,members; }
    static bool running;

    static PlayerMatchingBindingQueue() { EditorApplication.update+=Poll; }

    static void Poll() {
        if (running || !File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running=true; Request request=null;
        try {
            request=JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
            if (request.action=="bind") Bind(request);
            else if (request.action=="clean") {
                Type cleanup=FindType("PlayerMatchingCandidateCleanup");
                string report=(string)cleanup.GetMethod("Run",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[] {request.planPath});
                File.WriteAllText("Library/PlayerMatching/cleanup.json",report);
                SaveResult(new Response {requestId=request.requestId,status="passed"});
            }
            else if (request.action=="test") {
                Type test=FindType("PlayerMatchingBindingValidation");
                string report=(string)test.GetMethod("Run",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
                File.WriteAllText("Library/PlayerMatching/validation.json",report);
                SaveResult(new Response {requestId=request.requestId,status="passed"});
            } else throw new InvalidOperationException("Unsupported matching binding action");
            File.Delete(Pending);
        } catch (Exception exception) {
            SaveResult(new Response {requestId=request?.requestId,status="failed",error=exception.ToString()});
            if (File.Exists(Pending)) File.Move(Pending,Pending+".failed-"+DateTime.UtcNow.Ticks);
            Debug.LogException(exception);
        } finally { running=false; }
    }

    static void SaveResult(Response result) {
        Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result,JsonUtility.ToJson(result,true));
    }

    static Type FindType(string name) {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            Type type=assembly.GetType(name,false); if (type!=null) return type;
        }
        throw new InvalidOperationException("Missing binding type: "+name);
    }

    static string Hash(string path) {
        using (SHA256 sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
    }

    static GameObject Resolve(GameObject root, Member member) {
        Transform found=root.transform.Find(member.path);
        if (found==null || found.GetComponent(FindType(member.type))==null) throw new InvalidOperationException("Missing binding target/component: "+root.name+"/"+member.path+" "+member.type);
        return found.gameObject;
    }

    static UIBindComponentData Data(GameObject go, Member member, int id) {
        bool custom=member.type.StartsWith("PlayerMatching",StringComparison.Ordinal);
        return new UIBindComponentData {id=id,uiName=member.alias,uiTypeName=FindType(member.type).Name,isCustomClass=custom,customClassName=custom?member.type:string.Empty,go=go};
    }

    /**同一视图不同状态仅导出各自存在的字段；公共声明由实际字段并集经原Inspector生成。*/
    static void GenerateSchema(IEnumerable<Plan> plans) {
        var roots=new List<GameObject>(); GameObject schema=null; Editor inspector=null;
        try {
            Plan first=plans.First(); var union=new Dictionary<string,UIBindComponentData>();
            foreach (Plan plan in plans) {
                GameObject root=PrefabUtility.LoadPrefabContents(plan.prefab); roots.Add(root);
                foreach (Member member in plan.members) {
                    GameObject go=Resolve(root,member);
                    if (union.TryGetValue(member.alias,out UIBindComponentData previous)) {
                        if (previous.uiTypeName!=FindType(member.type).Name) throw new InvalidOperationException("Conflicting field type: "+member.alias);
                    } else union.Add(member.alias,Data(go,member,union.Count+1));
                }
            }
            schema=new GameObject("PlayerMatchingBindingSchema"); schema.hideFlags=HideFlags.HideAndDontSave;
            UIBinder binder=schema.AddComponent<UIBinder>(); binder.csharpAssetPath=first.script; binder.csharpAsset=first.script; binder.uiList=union.Values.ToList();
            inspector=Editor.CreateEditor(binder);
            MethodInfo generator=inspector.GetType().GetMethod("GenerateUiBind",BindingFlags.Instance|BindingFlags.NonPublic);
            if (generator==null) throw new MissingMethodException("UIBinderInspector.GenerateUiBind");
            generator.Invoke(inspector,new object[] {first.script});
        } finally {
            if (inspector!=null) UnityEngine.Object.DestroyImmediate(inspector);
            if (schema!=null) UnityEngine.Object.DestroyImmediate(schema);
            foreach (GameObject root in roots) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Bind(Request request) {
        Definition definition=JsonUtility.FromJson<Definition>(File.ReadAllText(request.planPath));
        foreach (Plan plan in definition.plans) {
            if (!plan.prefab.StartsWith("Assets/Prefab/uiBattle/battle/playerMatching/",StringComparison.Ordinal) || !plan.script.StartsWith("Assets/Scripts/csharp/main/playerMatching/view/",StringComparison.Ordinal)) throw new InvalidOperationException("Binding outside approved module");
            if (Hash(plan.prefab)!=plan.beforeSha256) throw new InvalidOperationException("Prefab changed since preflight: "+plan.prefab);
        }
        var generated=new HashSet<string>(); int members=0;
        foreach (Plan plan in definition.plans) {
            if (generated.Add(plan.view)) GenerateSchema(definition.plans.Where(item=>item.view==plan.view));
            GameObject root=PrefabUtility.LoadPrefabContents(plan.prefab);
            try {
                // Root Panel is attached by UIBinder at runtime; reusable children are explicit nested view components.
                if (plan.view!="PlayerMatchingPanel" && root.GetComponent(FindType(plan.view))==null) root.AddComponent(FindType(plan.view));
                UIBinder binder=root.GetComponent<UIBinder>() ?? root.AddComponent<UIBinder>();
                binder.csharpAssetPath=plan.script; binder.csharpAsset=plan.script; binder.uiList=new List<UIBindComponentData>();
                foreach (Member member in plan.members) binder.uiList.Add(Data(Resolve(root,member),member,binder.uiList.Count+1));
                binder.RefreshScriptBinding(); members+=binder.uiList.Count;
                EditorUtility.SetDirty(binder); PrefabUtility.SaveAsPrefabAsset(root,plan.prefab); AssetDatabase.SaveAssets();
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        SaveResult(new Response {requestId=request.requestId,status="passed",prefabs=definition.plans.Length,schemas=generated.Count,members=members});
        AssetDatabase.Refresh();
    }
}
#endif
