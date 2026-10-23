param(
    [string]$Editor = 'E:\UNITY\EDITOR\6000.6.2f1\Editor\Unity.exe',
    [ValidateSet('All','EditMode','PlayMode','Benchmark','Capture','Build')][string]$Stage = 'All'
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$logs = Join-Path $project 'Builds\PCGValidation'
$evidence = Join-Path $project 'Assets\CombatDemo\Validation\PCG'
New-Item -ItemType Directory -Force -Path $logs,$evidence | Out-Null
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($project) }) { throw 'Close the project editor before batch validation.' }
function Invoke-PcgUnity([string[]]$Extra,[string]$Name) {
    $arguments = @('-batchmode','-projectPath',$project,'-logFile',(Join-Path $logs ($Name + '.log'))) + $Extra
    $quoted = ($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }) -join ' '
    $process = Start-Process -FilePath $Editor -ArgumentList $quoted -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity stage $Name exited $($process.ExitCode). Check $logs." }
}
if ($Stage -in @('All','Benchmark')) { Invoke-PcgUnity @('-nographics','-executeMethod','Milkfrog.CombatDemo.Editor.PcgAuthoring.Benchmark','-quit') 'Benchmark' }
foreach ($platform in @('EditMode','PlayMode')) {
    if ($Stage -in @('All',$platform)) {
        $xml = Join-Path $evidence ($platform + '.xml')
        $testArguments = @('-runTests','-testPlatform',$platform,'-testResults',$xml)
        if ($platform -eq 'EditMode') { $testArguments = @('-nographics') + $testArguments }
        Invoke-PcgUnity $testArguments $platform
        [xml]$result = Get-Content -LiteralPath $xml
        if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.failed -ne 0 -or [int]$result.'test-run'.total -le 0) { throw "$platform tests did not pass." }
        Write-Output "$platform $($result.'test-run'.passed)/$($result.'test-run'.total) passed"
    }
}
if ($Stage -in @('All','Capture')) { Invoke-PcgUnity @('-executeMethod','Milkfrog.CombatDemo.Editor.PcgCapture.Run') 'Capture' }
if ($Stage -in @('All','Build')) { Invoke-PcgUnity @('-executeMethod','Milkfrog.CombatDemo.Editor.PcgAuthoring.Build','-quit') 'Build' }
