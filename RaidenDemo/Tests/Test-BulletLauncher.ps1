param([string]$ProjectPath = (Join-Path $PSScriptRoot '../Project'))

$ErrorActionPreference = 'Stop'
$taskProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$taskResponse = Get-ChildItem -LiteralPath (Join-Path $taskProject 'Library/Bee/artifacts') -Filter 'Assembly-CSharp.rsp' -Recurse |
    Where-Object { $_.Directory.Name -like '*EDbg*' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$taskResponse) { throw '请先让 Unity 完成一次编辑器编译，生成当前工程的编译响应文件。' }
$taskLines = Get-Content -LiteralPath $taskResponse.FullName
$taskReference = $taskLines | Where-Object { $_ -match '^-r:"(.+)/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"$' } | Select-Object -First 1
if (!$taskReference -or $taskReference -notmatch '^-r:"(.+)/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll"$') { throw '无法从当前工程的编译依赖确认 Unity 安装路径。' }
$taskEditor = $Matches[1]
$taskSdk = (& dotnet --list-sdks | Select-Object -Last 1)
if ($taskSdk -notmatch '^([^ ]+) \[(.+)\]$') { throw '需要可用的 .NET SDK 执行独立源码编译。' }
$taskCompiler = Join-Path $Matches[2] ($Matches[1] + '/Roslyn/bincore/csc.dll')
$taskOutput = Join-Path ([System.IO.Path]::GetTempPath()) ('RaidenLauncherCheck-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput | Out-Null

# 只替换编译输出与当前源码清单，不覆盖 Unity 缓存程序集，也不启动或操作编辑器。
$taskCompileLines = @($taskLines | Where-Object { $_ -notmatch '^-(out|refout|target):' -and $_ -notmatch '^"?Assets/Scripts/.*\.cs"?$' })
Push-Location $taskProject
try {
    $taskCompileLines += @(rg --files Assets/Scripts -g '*.cs' | ForEach-Object { '"' + $_.Replace('\', '/') + '"' })
    if ($LASTEXITCODE -ne 0) { throw '读取当前源码清单失败。' }
    $taskCompileLines += '"' + (Join-Path $PSScriptRoot 'BulletLauncherRegression.cs') + '"'
    $taskCompileLines += '-target:exe', '-main:BulletLauncherRegression', ('-out:"' + (Join-Path $taskOutput 'LauncherTests.exe') + '"')
    $taskRsp = Join-Path $taskOutput 'tests.rsp'
    [System.IO.File]::WriteAllLines($taskRsp, $taskCompileLines)
    & dotnet $taskCompiler ('@' + $taskRsp)
    if ($LASTEXITCODE -ne 0) { throw '源码编译失败。' }
    $taskPreviousMonoPath = $env:MONO_PATH
    try {
        $env:MONO_PATH = "$taskEditor/Data/Managed/UnityEngine;$taskEditor/Data/Managed;$taskProject/Library/ScriptAssemblies"
        & (Join-Path $taskEditor 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutput 'LauncherTests.exe')
        if ($LASTEXITCODE -ne 0) { throw '发射器回归断言失败。' }
    } finally {
        $env:MONO_PATH = $taskPreviousMonoPath
    }
} finally {
    Pop-Location
}
Write-Host "检查产物：$taskOutput"
Write-Host '独立 Mono 可能报告 Unity 原生转向函数不可用；本检查只验证目标查询接线，实际追踪转向仍需在 Unity 中验收。'
