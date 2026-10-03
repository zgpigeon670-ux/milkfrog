param(
    [string]$Editor = 'E:\UNITY\EDITOR\6000.6.2f1\Editor\Unity.exe',
    [string]$ValidationProject,
    [switch]$Capture,
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
$sourceProject = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (!$ValidationProject) { $ValidationProject = Join-Path (Split-Path $sourceProject -Parent) 'milkfrog-gameplay-validation' }
$ValidationProject = [IO.Path]::GetFullPath($ValidationProject).TrimEnd('\')
if ($ValidationProject.Equals($sourceProject, [StringComparison]::OrdinalIgnoreCase) -or
    $ValidationProject.StartsWith($sourceProject + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation must run in a separate project outside the source checkout.'
}
if (!(Test-Path -LiteralPath $Editor -PathType Leaf)) { throw "Unity editor missing: $Editor" }
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($ValidationProject)
}) { throw 'The validation project already has a Unity process. Close it before running.' }
$output = Join-Path $sourceProject 'Builds\GameplayValidation'
New-Item -ItemType Directory -Force -Path $output | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    & robocopy (Join-Path $sourceProject $folder) (Join-Path $ValidationProject $folder) /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Could not copy $folder" }
}
# Stale files from a previous run must never silently enter a test or build.
foreach ($file in Get-ChildItem (Join-Path $ValidationProject 'Assets') -File -Recurse) {
    $relative = $file.FullName.Substring($ValidationProject.Length + 1)
    if (!(Test-Path -LiteralPath (Join-Path $sourceProject $relative))) {
        throw "Extra validation asset: $relative. Use a fresh validation directory."
    }
}
function Invoke-Unity([string[]]$Arguments, [string]$Log) {
    $allArguments = @('-batchmode', '-projectPath', $ValidationProject) + $Arguments + @('-logFile', $Log)
    $quoted = ($allArguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $process = Start-Process $Editor -ArgumentList $quoted -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity exit $($process.ExitCode). See $Log" }
}
foreach ($platform in @('EditMode', 'PlayMode')) {
    $xmlPath = Join-Path $output "$platform.xml"
    Invoke-Unity @('-nographics', '-runTests', '-testPlatform', $platform, '-testResults', $xmlPath) (Join-Path $output "$platform.log")
    [xml]$results = Get-Content -LiteralPath $xmlPath
    if ([int]$results.'test-run'.total -eq 0 -or [int]$results.'test-run'.failed -gt 0 -or $results.'test-run'.result -ne 'Passed') {
        throw "$platform did not pass. See $xmlPath"
    }
    Write-Host "$platform $($results.'test-run'.passed)/$($results.'test-run'.total) passed"
}
if ($Capture) {
    $previousEvidence = $env:MILKFROG_INVENTORY_EVIDENCE
    try {
        $env:MILKFROG_INVENTORY_EVIDENCE = Join-Path $output 'InventoryScreenshots'
        Invoke-Unity @('-executeMethod', 'Milkfrog.CombatDemo.Editor.InventoryCapture.Run') (Join-Path $output 'InventoryCapture.log')
        Invoke-Unity @('-executeMethod', 'Milkfrog.CombatDemo.Editor.PhaseThreeCapture.Actions') (Join-Path $output 'ActionsCapture.log')
    }
    finally { $env:MILKFROG_INVENTORY_EVIDENCE = $previousEvidence }
}
if ($Build) {
    $previousBuild = $env:MILKFROG_BUILD_OUTPUT
    try {
        $env:MILKFROG_BUILD_OUTPUT = Join-Path $sourceProject 'Builds\MilkfrogMVP-Gameplay\MilkfrogMVP.exe'
        Invoke-Unity @('-quit', '-executeMethod', 'Milkfrog.CombatDemo.Editor.MvpInventoryUpgrade.BuildPlayer') (Join-Path $output 'Build.log')
        if (!(Test-Path -LiteralPath $env:MILKFROG_BUILD_OUTPUT -PathType Leaf)) { throw 'Build executable is missing.' }
        Write-Host "Build: $env:MILKFROG_BUILD_OUTPUT"
    }
    finally { $env:MILKFROG_BUILD_OUTPUT = $previousBuild }
}
