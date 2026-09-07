param([string]$UnityEditor = 'D:/Unity/Editor/2022.3.62f3/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Project'))
$editor = Split-Path $UnityEditor
$response = Get-ChildItem "$project/Library/Bee/artifacts" -Filter Assembly-CSharp.rsp -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $response) { throw 'Open this project in Unity once to generate compiler references.' }
$output = Join-Path $project 'Library/RaidenBattleChecks'
New-Item -ItemType Directory -Force $output | Out-Null
$lines = @(Get-Content $response.FullName | Where-Object { $_ -notmatch '^[-/](out:|refout:|target:)' -and $_ -notmatch '\.cs"?$' })
$lines += '-target:exe', '-main:RaidenBattleChecks', ('-out:"' + $output + '/RaidenBattleChecks.exe"')
$lines += Get-ChildItem "$project/Assets/Scripts" -Filter *.cs -Recurse | ForEach-Object { '"' + $_.FullName + '"' }
$lines += '"' + $PSScriptRoot + '/Tests/RaidenBattleChecks.cs"'
$rsp = Join-Path $output 'checks.rsp'
$lines | Set-Content -Encoding utf8 $rsp
$oldMonoPath = $env:MONO_PATH
Push-Location $project
try {
    & "$editor/Data/NetCoreRuntime/dotnet.exe" "$editor/Data/DotNetSdkRoslyn/csc.dll" "@$rsp"
    if ($LASTEXITCODE -ne 0) { throw 'Battle compilation failed.' }
    $env:MONO_PATH = "$project/Library/ScriptAssemblies;$editor/Data/Managed/UnityEngine;$editor/Data/Managed"
    & "$editor/Data/MonoBleedingEdge/bin/mono.exe" "$output/RaidenBattleChecks.exe" "$project/Assets/ConfigBin"
    if ($LASTEXITCODE -ne 0) { throw 'Battle regression checks failed.' }
} finally {
    Pop-Location
    $env:MONO_PATH = $oldMonoPath
}
