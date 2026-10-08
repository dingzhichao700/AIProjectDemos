param(
    [ValidateSet('prepare', 'generate', 'export', 'content', 'publish')]
    [string]$Action = 'content'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../Project'
$queue = Join-Path $project 'Library/AIUI'
$ready = Get-Content -LiteralPath (Join-Path $queue 'wechat-build-ready.json') -Raw | ConvertFrom-Json
$status = Get-Content -LiteralPath (Join-Path $queue 'editor-status.json') -Raw | ConvertFrom-Json
if ($ready.processId -ne $status.processId -or $ready.target -ne 'WebGL') { throw 'Unity PID mismatch or target is not WebGL.' }
$null = Get-Process -Id $ready.processId -ErrorAction Stop
if ($status.isPlaying -or $status.isCompiling -or $status.isUpdating -or $status.isRunning) { throw 'Unity is busy. Stop Play mode and wait for compilation before retrying.' }
$resultPath = Join-Path $queue 'wechat-build-result.json'
if (Test-Path -LiteralPath $resultPath) {
    $previous = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($previous.status -in @('preparing-hybridclr', 'generating-hybridclr', 'building-content', 'building-addressables', 'building-wechat')) { throw 'A build is already running. Inspect wechat-build-result.json and the Unity console.' }
}
$request = @{ requestId = [guid]::NewGuid().ToString(); processId = $ready.processId; action = $Action }
if ($Action -eq 'export') {
    $request.outputPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('../build/build_wechat_' + (Get-Date -Format 'yyyyMMddHHmmss'))))
}
$pending = Join-Path $queue 'pending-wechat-build.json'
if (Test-Path -LiteralPath $pending) { throw 'Another build request is pending.' }
$temporary = Join-Path $queue ($request.requestId + '.tmp')
$request | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
Move-Item -LiteralPath $temporary -Destination $pending
Write-Host ('Requested ' + $Action + ': ' + $request.requestId)
Write-Host ('Progress: ' + $resultPath)
