#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [ValidateSet('Standalone', 'WeChat')][string]$Platform = 'WeChat',
    [string]$BootstrapUrl,
    [string]$CdnRoot,
    [string]$AppId
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Write-Json([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 30), $utf8)
}
function Set-Reference([string]$RelativePath, [string[]]$Names) {
    $path = Join-Path $project $RelativePath
    $assembly = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $assembly.references = @($assembly.references | Where-Object { $_ -notin @('HybridCLR.Runtime', 'HybridCLR.Editor', 'Wx', 'WxEditor') })
    if ($Platform -eq 'WeChat') { $assembly.references += $Names }
    Write-Json $path $assembly
}

$manifestPath = Join-Path $project 'Packages/manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.dependencies.PSObject.Properties.Remove('com.code-philosophy.hybridclr')
$manifest.dependencies.PSObject.Properties.Remove('com.qq.weixin.minigame')
if ($Platform -eq 'WeChat') {
    # Lock the versions already validated by the source project.
    $manifest.dependencies | Add-Member -NotePropertyName 'com.code-philosophy.hybridclr' -NotePropertyValue 'https://gitee.com/focus-creative-games/hybridclr_unity.git#fd32708e7f5106f0ffa375bcfe43342ee594c1e5'
    $manifest.dependencies | Add-Member -NotePropertyName 'com.qq.weixin.minigame' -NotePropertyValue 'https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git#d288776c50578926c732496882bd6ab6684c778c'
}
Write-Json $manifestPath $manifest
Set-Reference 'Assets/Scripts/csharp/foundation/Foundation.asmdef' @('HybridCLR.Runtime')
Set-Reference 'Assets/Editor/Framework.Editor.asmdef' @('HybridCLR.Runtime', 'HybridCLR.Editor', 'Wx', 'WxEditor')

$responsePath = Join-Path $project 'Assets/csc.rsp'
$response = if (Test-Path -LiteralPath $responsePath) { [IO.File]::ReadAllText($responsePath) } else { '' }
$response = $response -replace '(?m)^-define:GAMEFRAMEWORK_WECHAT\s*$', ''
if ($Platform -eq 'WeChat') { $response = $response.TrimEnd() + "`n-define:GAMEFRAMEWORK_WECHAT`n" }
[IO.File]::WriteAllText($responsePath, $response, $utf8)

$configPath = Join-Path $project 'ProjectSettings/FrameworkPlatform.json'
$config = if (Test-Path -LiteralPath $configPath) { Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json } else { [PSCustomObject]@{ platform = ''; bootstrapUrl = ''; cdnRoot = ''; appId = '' } }
$config.platform = $Platform
if ($PSBoundParameters.ContainsKey('BootstrapUrl')) { $config.bootstrapUrl = $BootstrapUrl }
if ($PSBoundParameters.ContainsKey('CdnRoot')) { $config.cdnRoot = $CdnRoot }
if ($PSBoundParameters.ContainsKey('AppId')) { $config.appId = $AppId }
Write-Json $configPath $config
Write-Host "Configured platform: $Platform. Reimport the project; use WebGL for WeChat exports."

# Explicit Standalone export disables the template hot-update settings.
$hybridPath = Join-Path $project 'ProjectSettings/HybridCLRSettings.asset'
if (Test-Path -LiteralPath $hybridPath) {
    $hybrid = [IO.File]::ReadAllText($hybridPath)
    $enabled = if ($Platform -eq 'WeChat') { '1' } else { '0' }
    $hybrid = $hybrid -replace '(?m)^  enable: [01]', ('  enable: ' + $enabled)
    [IO.File]::WriteAllText($hybridPath, $hybrid, $utf8)
}
