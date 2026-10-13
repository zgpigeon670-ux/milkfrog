param([ValidateSet('EditMode','PlayMode','Capture','GameplayCapture','Build')][string]$Stage)
$ErrorActionPreference='Stop'
$project=Get-Content (Join-Path $PSScriptRoot '../Builds/AnimationRepair/validation-project.txt')
$evidence=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Builds/AnimationRepair'))
$unity='E:\UNITY\EDITOR\6000.6.2f1\Editor\Unity.exe'
$arguments=@('-batchmode','-projectPath',$project)
switch($Stage){
    'EditMode' {$arguments+=@('-nographics','-runTests','-testPlatform','EditMode','-testResults',(Join-Path $evidence 'EditMode.xml'))}
    'PlayMode' {$arguments+=@('-nographics','-runTests','-testPlatform','PlayMode','-testResults',(Join-Path $evidence 'PlayMode.xml'))}
    'Capture' {$env:MILKFROG_ANIMATION_EVIDENCE=Join-Path $evidence 'Unity';$arguments+=@('-executeMethod','Milkfrog.CombatDemo.Editor.MilkfrogAnimationCapture.Run','-quit')}
    'GameplayCapture' {$env:MILKFROG_ANIMATION_EVIDENCE=Join-Path $evidence 'Gameplay';$arguments+=@('-executeMethod','Milkfrog.CombatDemo.Editor.MilkfrogGameplayCapture.Run')}
    'Build' {$env:MILKFROG_BUILD_OUTPUT=Join-Path $evidence 'Windows/MilkfrogMVP.exe';$arguments+=@('-nographics','-executeMethod','Milkfrog.CombatDemo.Editor.MvpInventoryUpgrade.BuildPlayer','-quit')}
}
$arguments+=@('-logFile',(Join-Path $evidence ($Stage+'.log')))
$process=Start-Process $unity -ArgumentList (($arguments|ForEach-Object{'"'+$_+'"'})-join ' ') -WindowStyle Hidden -PassThru
$process.Id | Set-Content (Join-Path $evidence ($Stage+'-process.txt'))
'Started '+$Stage+' PID '+$process.Id
