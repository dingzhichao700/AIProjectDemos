#Requires -Version 5.1
<#
.SYNOPSIS
  Read-only verification of persistent Game Framework project settings.
.DESCRIPTION
  Checks a template, generated staging project, or existing Unity project.
  Does not launch Unity, change settings, or force runtime frame updates.
#>
param([string]$ProjectPath = (Join-Path $PSScriptRoot '..'))
$ErrorActionPreference = 'Stop'
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
$settingsPath = Join-Path $ProjectPath 'ProjectSettings\ProjectSettings.asset'
if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
    throw "Missing PlayerSettings file: $settingsPath"
}
$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8
$entries = [regex]::Matches($settings, '(?m)^  runInBackground:[^\r\n]*')
if ($entries.Count -ne 1 -or $entries[0].Value.TrimEnd() -cne '  runInBackground: 1') {
    throw "Run In Background must have exactly one 'runInBackground: 1' entry in $settingsPath. Restore persistent Player Settings; a temporary Application.runInBackground override does not fix the project. File was not changed."
}
Write-Host "[project-settings] Run In Background enabled: $settingsPath"