#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('inspect', 'prepare', 'generate', 'export', 'content', 'publish')][string]$Action = 'content',
    [string]$BuildReleaseSkillPath
)
$ErrorActionPreference = 'Stop'
if (!$BuildReleaseSkillPath) {
    $scanRoot = if ($env:CODEX_HOME) { Join-Path $env:CODEX_HOME 'skills' } else { Join-Path $env:USERPROFILE '.codex/skills' }
    $BuildReleaseSkillPath = Join-Path $scanRoot 'game-build-release'
}
$entry = Join-Path $BuildReleaseSkillPath 'scripts/Invoke-GameBuild.ps1'
if (!(Test-Path -LiteralPath $entry)) { throw 'Install/link game-build-release or provide -BuildReleaseSkillPath. No request was submitted.' }
$mapped = if ($Action -eq 'publish') { 'publish-local' } else { $Action }
& $entry -ProjectPath (Join-Path $PSScriptRoot '../Project') -Action $mapped -WhatIf:$WhatIfPreference
