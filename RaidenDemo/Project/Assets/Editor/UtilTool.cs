using System.Diagnostics;
using UnityEditor;
using UnityEngine;

public class UtilTool
{
    public static bool CmdExe(string exePath, string cmdArg, bool wait = true)
    {
        bool runSuc = true;

        ProcessStartInfo start = new ProcessStartInfo(exePath);
        start.Arguments = cmdArg;
        start.CreateNoWindow = false;
        start.ErrorDialog = true;
        start.UseShellExecute = false;

        if (start.UseShellExecute)
        {
            start.RedirectStandardOutput = false;
            start.RedirectStandardError = false;
            start.RedirectStandardInput = false;
        }
        else
        {
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.RedirectStandardInput = true;
            start.StandardOutputEncoding = System.Text.UTF8Encoding.UTF8;
            start.StandardErrorEncoding = System.Text.UTF8Encoding.UTF8;
        }

        Process p = Process.Start(start);
        if (!start.UseShellExecute)
        {
            UnityEngine.Debug.Log(p.StandardOutput.ReadToEnd());
            UnityEngine.Debug.Log(p.StandardError.ReadToEnd());
        }

        if (wait)
        {
            p.WaitForExit();
            int code = p.ExitCode;
            p.Close();

            UnityEngine.Debug.Log(exePath + " exe return code: " + code);
            return code == 0;
        }
        return runSuc;
    }

    public static bool CmdExe2(string cmdExe, string workingDir, string args, bool wait = true)
    {
        bool runSuc = true;
        Process p = new Process();
        p.StartInfo.FileName = cmdExe;
        p.StartInfo.Arguments = args;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        if (!string.IsNullOrEmpty(workingDir))
            p.StartInfo.WorkingDirectory = workingDir;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            UnityEngine.Debug.Log(e.Data);
        };
        p.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            UnityEngine.Debug.LogError(e.Data);
        };
        p.Start();
        p.BeginErrorReadLine();
        p.BeginErrorReadLine();
        if (wait)
        {
            p.WaitForExit();
            int code = p.ExitCode;
            p.Close();

            UnityEngine.Debug.Log(cmdExe + " exe return code: " + code);
            return code == 0;
        }
        return runSuc;
    }
}
