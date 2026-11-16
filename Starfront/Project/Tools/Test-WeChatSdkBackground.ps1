#Requires -Version 5.1
param([string]$PackagePath = (Join-Path $PSScriptRoot '../Packages/com.qq.weixin.minigame'))
$ErrorActionPreference='Stop'
$package=Join-Path $PackagePath 'package.json'
$core=Join-Path $PackagePath 'Editor/WXConvertCore.cs'
if(!(Test-Path -LiteralPath $package -PathType Leaf) -or !(Test-Path -LiteralPath $core -PathType Leaf)){throw 'Missing embedded WeChat SDK background patch'}
if((Get-Content -LiteralPath $package -Raw|ConvertFrom-Json).name -ne 'com.qq.weixin.minigame'){throw 'Wrong WeChat SDK package identity'}
if(!(Test-Path -LiteralPath (Join-Path $PackagePath 'LICENSE') -PathType Leaf)){throw 'Missing upstream SDK license'}
$expected='92402ca47a35a8d5bd0a3ba9a0daeb65c0ed8c32b5ca6bbeb86cc6550dc2f257'
if((Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash -ne $expected){throw 'WeChat SDK minimal background patch changed or regressed; re-verify the pinned three-line deletion before upgrading'}
Write-Host '[wechat-sdk] pinned native background patch verified (three assignments removed)'
