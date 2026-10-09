param([string]$ProjectPath = (Join-Path $PSScriptRoot '../Project'))
$ErrorActionPreference = 'Stop'
$taskProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$taskCore = Join-Path $taskProject 'Assets/Scripts/csharp/foundation/core'
$taskSources = @(
    (Join-Path $taskCore 'eventBus/Handler.cs'),
    (Join-Path $taskCore 'timer/TimeHandler.cs'),
    (Join-Path $taskCore 'timer/Timer.cs'),
    (Join-Path $PSScriptRoot 'Tests/TimerRegression.cs')
)
Add-Type -Path $taskSources
[TimerRegression]::Run()
