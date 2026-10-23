param(
    [ValidateSet('Create','FocusedTests','EditMode','PlayMode','Capture','Build','Smoke','All')][string]$Stage='All',
    [string]$Editor='E:\UNITY\EDITOR\6000.6.2f1\Editor\Unity.exe'
)
$ErrorActionPreference='Stop'
$taskProject=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$taskLogs=Join-Path $taskProject 'Builds\BellCrossingValidation'
$taskEvidence=Join-Path $taskProject 'Assets\CombatDemo\Validation\BellCrossing'
$taskScratch=Join-Path $taskProject '.utmp\BellCrossing'
New-Item -ItemType Directory -Force -Path $taskLogs,$taskEvidence,$taskScratch | Out-Null
$taskOldTemp=$env:TEMP; $taskOldTmp=$env:TMP
function Invoke-BellUnity([string[]]$Extra,[string]$Name){
    $taskArguments=@('-batchmode','-projectPath',$taskProject,'-logFile',(Join-Path $taskLogs ($Name+'.log')))+$Extra
    $taskQuoted=($taskArguments|ForEach-Object {'"'+$_.Replace('"','\"')+'"'})-join ' '
    $taskProcess=Start-Process -FilePath $Editor -ArgumentList $taskQuoted -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    if($taskProcess.ExitCode -ne 0){throw "Unity $Name exited $($taskProcess.ExitCode). See $taskLogs."}
}
try {
    # The process inherits a project-local temporary directory, including when launched outside a sandbox.
    $env:TEMP=$taskScratch; $env:TMP=$taskScratch
    if($Stage -in @('All','Create')){Invoke-BellUnity @('-nographics','-executeMethod','Milkfrog.CombatDemo.Editor.BellCrossingAuthoring.Create','-quit') 'Create'}
    foreach($taskPlatform in @('FocusedTests','EditMode','PlayMode')){
        if($Stage -in @('All',$taskPlatform) -and !($Stage -eq 'All' -and $taskPlatform -eq 'FocusedTests')){
            $taskXml=Join-Path $taskEvidence ($taskPlatform+'.xml')
            $taskMode=if($taskPlatform -eq 'FocusedTests'){'PlayMode'}else{$taskPlatform}
            $taskExtra=@('-runTests','-testPlatform',$taskMode,'-testResults',$taskXml)
            if($taskPlatform -eq 'FocusedTests'){$taskExtra+=@('-testFilter','Milkfrog.CombatDemo.Tests.BellCrossingTests')}
            if($taskMode -eq 'EditMode'){$taskExtra=@('-nographics')+$taskExtra}
            Invoke-BellUnity $taskExtra $taskPlatform
            [xml]$taskResult=Get-Content -LiteralPath $taskXml
            if($taskResult.'test-run'.result -ne 'Passed' -or [int]$taskResult.'test-run'.failed -ne 0 -or [int]$taskResult.'test-run'.total -le 0){throw "$taskPlatform tests failed: $taskXml"}
            Write-Output "$taskPlatform $($taskResult.'test-run'.passed)/$($taskResult.'test-run'.total) passed"
        }
    }
    if($Stage -in @('All','Capture')){Invoke-BellUnity @('-executeMethod','Milkfrog.CombatDemo.Editor.BellCrossingCapture.Run') 'Capture'}
    if($Stage -in @('All','Build')){Invoke-BellUnity @('-executeMethod','Milkfrog.CombatDemo.Editor.BellCrossingAuthoring.Build','-quit') 'Build'}
    if($Stage -in @('All','Smoke')){
        $taskExe=Join-Path $taskProject 'Builds\Milkfrog-BellCrossing\Milkfrog-BellCrossing.exe'
        $taskSmokeLog=Join-Path $taskLogs 'WindowsSmoke.log'
        $taskSmoke=Start-Process -FilePath $taskExe -ArgumentList ('-bell-smoke -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "'+$taskSmokeLog+'"') -WindowStyle Hidden -PassThru
        $taskSmoke.WaitForExit()
        if($taskSmoke.ExitCode -ne 0){throw "Windows smoke exited $($taskSmoke.ExitCode)."}
        $taskSmokeFolder=Join-Path (Split-Path $taskExe -Parent) 'SmokeEvidence'
        $taskSmokeResult=Get-Content (Join-Path $taskSmokeFolder 'smoke.json')|ConvertFrom-Json
        if(!$taskSmokeResult.passed){throw 'Windows smoke did not pass.'}
        New-Item -ItemType Directory -Force -Path (Join-Path $taskEvidence 'Windows')|Out-Null
        Copy-Item -LiteralPath (Join-Path $taskSmokeFolder 'smoke.json') -Destination (Join-Path $taskEvidence 'windows-smoke.json')
        foreach($taskImage in @('player.png','overview.png')){Copy-Item -LiteralPath (Join-Path $taskSmokeFolder $taskImage) -Destination (Join-Path $taskEvidence ('Windows\'+$taskImage))}
        Write-Output 'Windows player initialization, movement, two camera renders and scripted fixture progression passed.'
    }
} finally {$env:TEMP=$taskOldTemp;$env:TMP=$taskOldTmp}
