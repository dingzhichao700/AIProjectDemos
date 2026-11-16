#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug=UnityEngine.Debug;

/// <summary>Single adapter to the same read-only CLI used by import, pack, AA and release checks.</summary>
public static class ResourceGovernanceGate {
    public const string ApiVersion="1.0";
    const string PythonKey="ResourceGovernance.PythonExecutable";
    const string JournalKey="ResourceGovernance.JournalRoot";
    public static string ProjectRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    public static string JournalRoot=>EditorPrefs.GetString(JournalKey,Environment.GetEnvironmentVariable("RESOURCE_GOVERNANCE_JOURNAL_ROOT")??"");
    [Serializable] public class Finding {public string code,path,message,severity,exceptionId,reviewBatch;}
    [Serializable] public class Report {public string toolVersion,error;public Finding[] errors,legacy,warnings;public int assetCount,addressableEntryCount;}
    [Serializable] class MachineSettings {public string schemaVersion="1",pythonExecutable,journalRoot,sourceProject;}
    public sealed class AuditResult {public int exitCode;public string reportPath,stdout;public Report report;}
    public static void ConfigureMachine(string python,string journalRoot){
        if(!File.Exists(python))throw new FileNotFoundException("Python interpreter missing",python);
        string root=Path.GetFullPath(journalRoot);if(IsWithin(root,ProjectRoot))throw new InvalidOperationException("Durable journals must live outside the Unity project");
        Directory.CreateDirectory(root);EditorPrefs.SetString(PythonKey,Path.GetFullPath(python));EditorPrefs.SetString(JournalKey,root);
        var machine=new MachineSettings{pythonExecutable=Path.GetFullPath(python),journalRoot=root,sourceProject=ProjectRoot};
        string local=Path.Combine(ProjectRoot,"Library/ResourceGovernance");Directory.CreateDirectory(local);File.WriteAllText(Path.Combine(local,"machine-settings.json"),JsonUtility.ToJson(machine,true));File.WriteAllText(Path.Combine(root,"machine-settings.json"),JsonUtility.ToJson(machine,true));
    }
    internal static bool IsWithin(string path,string root)=>string.Equals(Path.GetFullPath(path).TrimEnd('\\','/'),Path.GetFullPath(root).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)||Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    static string Quote(string value){if(value.Contains('"')||value.Contains('\n')||value.Contains('\r'))throw new InvalidOperationException("Invalid CLI argument");return "\""+value.TrimEnd('\\')+"\"";}
    public static string RunCli(string[] arguments,out int exitCode){
        string python=EditorPrefs.GetString(PythonKey,Environment.GetEnvironmentVariable("RESOURCE_GOVERNANCE_PYTHON")??"python");
        string script=Path.Combine(ProjectRoot,"Tools/ResourceGovernance/resource_governance.py");
        if(!File.Exists(script))throw new FileNotFoundException("Resource governance CLI missing",script);
        using(var process=new Process()){
            process.StartInfo=new ProcessStartInfo{FileName=python,Arguments=Quote(script)+" "+string.Join(" ",arguments.Select(Quote)),WorkingDirectory=ProjectRoot,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            process.Start();var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(90000)){process.Kill();throw new TimeoutException("Resource governance CLI exceeded 90 seconds");}
            Task.WaitAll(stdout,stderr);exitCode=process.ExitCode;return stdout.Result+stderr.Result;
        }
    }
    [ThreadStatic] static string verifyingJournal;
    sealed class VerificationLease:IDisposable {public void Dispose(){verifyingJournal=null;}}
    internal static IDisposable EnterTransactionVerification(string journalPath){
        if(verifyingJournal!=null)throw new InvalidOperationException("Nested resource transaction verification is not allowed");
        string full=Path.GetFullPath(journalPath);
        if(!IsWithin(full,JournalRoot)||Path.GetFileName(full)!="journal.json")throw new InvalidOperationException("Verification journal is outside shared root");
        var journal=JsonUtility.FromJson<ResourceMoveTransaction.Journal>(File.ReadAllText(full));
        if(journal.project!=ProjectRoot||journal.state!="verifying")throw new InvalidOperationException("Only this project's verifying transaction may enter its producer scope");
        verifyingJournal=full;return new VerificationLease();
    }
    public static AuditResult Audit(string stage,params string[] scopes){return AuditInternal(stage,verifyingJournal,scopes);}
    internal static AuditResult AuditInternal(string stage,string activeJournal,params string[] scopes){
        if(string.IsNullOrWhiteSpace(JournalRoot))throw new InvalidOperationException("Configure ResourceGovernance durable journal directory first");
        string dir=Path.Combine(ProjectRoot,"Library/ResourceGovernance");Directory.CreateDirectory(dir);
        string report=Path.Combine(dir,"audit-"+Guid.NewGuid().ToString("N")+".json");
        var args=new List<string>{"--project",ProjectRoot,"--output",report,"--journal-root",JournalRoot};
        if(stage=="addressables-assign")args.Add("--allow-pending-registration");
        if(!string.IsNullOrEmpty(activeJournal)){args.Add("--active-journal");args.Add(activeJournal);}
        foreach(string scope in scopes??new string[0]){args.Add("--scope");args.Add(scope);}
        string stdout=RunCli(args.ToArray(),out int exit);
        var parsed=File.Exists(report)?JsonUtility.FromJson<Report>(File.ReadAllText(report)):null;
        if(exit==0&&(parsed==null||parsed.toolVersion!="1.0.0")){exit=3;stdout+=" Missing/incompatible audit report";}
        return new AuditResult{exitCode=exit,reportPath=report,stdout=stdout,report=parsed};
    }
    public static void ValidateOrThrow(string stage,params string[] scopes){
        var result=Audit(stage,scopes);
        if(result.exitCode!=0)throw new InvalidOperationException("Resource governance blocked "+stage+": "+result.stdout+"\nReport: "+result.reportPath);
    }
    public static string ValidateNativeSnapshot(string snapshot){
        string report=Path.Combine(ProjectRoot,"Library/ResourceGovernance/native-"+Guid.NewGuid().ToString("N")+".json");
        var args=new List<string>{"--project",ProjectRoot,"--output",report,"--journal-root",JournalRoot,"--dependency-snapshot",Path.GetFullPath(snapshot)};
        if(verifyingJournal!=null){args.Add("--active-journal");args.Add(verifyingJournal);}
        string stdout=RunCli(args.ToArray(),out int exit);if(exit!=0)throw new InvalidOperationException("Resource governance blocked native dependency check: "+stdout+"\n"+report);return report;
    }
    [MenuItem("Tools/Resources/Check All Resources")]
    public static void CheckAll(){var result=Audit("manual");if(result.exitCode!=0)Debug.LogError(result.stdout+"\n"+result.reportPath);else Debug.Log("[ResourceGovernance] passed; existing issues="+(result.report?.legacy?.Length??0)+", duplicate reviews="+(result.report?.warnings?.Length??0)+"; "+result.reportPath);}
    [MenuItem("Tools/Resources/Configure Python")]
    static void ConfigurePython(){string path=EditorUtility.OpenFilePanel("Python interpreter","","exe");if(!string.IsNullOrEmpty(path))EditorPrefs.SetString(PythonKey,path);}
    [MenuItem("Tools/Resources/Configure Durable Journal Directory")]
    static void ConfigureJournal(){string path=EditorUtility.OpenFolderPanel("Journal directory outside Unity project","","");if(!string.IsNullOrEmpty(path)){if(IsWithin(path,ProjectRoot))throw new InvalidOperationException("Journals must be outside project");EditorPrefs.SetString(JournalKey,Path.GetFullPath(path));}}
    // Sole release owner may call this adapter; it performs no build, export or activation.
    public static void BatchValidate(){ValidateOrThrow("release-input");}
}

/// <summary>Import reporting only: no automatic moves, deletions, Pivot changes or build actions.</summary>
internal sealed class ResourceGovernanceImportWatch:AssetPostprocessor {
    static readonly HashSet<string> pending=new HashSet<string>();static double next;
    static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom){
        foreach(var path in imported.Concat(deleted).Concat(moved).Concat(movedFrom))if(path.StartsWith("Assets/Art/",StringComparison.Ordinal)||path.StartsWith("Assets/Prefab/",StringComparison.Ordinal)||path.StartsWith("Assets/ResourceData/",StringComparison.Ordinal)||path.StartsWith("Assets/ConfigBin/",StringComparison.Ordinal)||path.StartsWith("Assets/Resources/",StringComparison.Ordinal))pending.Add(path);
        if(pending.Count==0)return;next=EditorApplication.timeSinceStartup+1;EditorApplication.update-=Poll;EditorApplication.update+=Poll;
    }
    static void Poll(){
        if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Poll;if(pending.Count==0)return;var paths=pending.ToArray();pending.Clear();
        try{var result=ResourceGovernanceGate.Audit("import",paths);if(result.exitCode!=0)Debug.LogWarning("[ResourceGovernance] Imported resources need correction; no asset modified. "+result.reportPath);}
        catch(Exception error){Debug.LogWarning("[ResourceGovernance] Import audit unavailable; controlled gates remain fail-closed: "+error.Message);}
    }
}
#endif
