param([string]$NsisCommand='makensis.exe')
$ErrorActionPreference='Stop'
$taskRepository=Split-Path $PSScriptRoot -Parent
$taskFixture=Join-Path $taskRepository ('.artifacts/installer-path-checks/'+[Guid]::NewGuid().ToString())
$taskContainer=Join-Path $taskFixture 'Container'
$taskRoot=Join-Path $taskContainer 'New app with spaces'
$taskProbe=Join-Path $taskFixture 'PathProbe.exe'
New-Item -ItemType Directory -Path $taskFixture | Out-Null

function Assert-ProbeExit([string[]]$Parameters,[int]$Expected) {
    $taskProcess=Start-Process -FilePath $taskProbe -ArgumentList $Parameters -WindowStyle Hidden -PassThru -Wait
    if($taskProcess.ExitCode -ne $Expected){throw ('Native path probe returned '+$taskProcess.ExitCode+'; expected '+$Expected)}
}

& $NsisCommand /NOCONFIG /WX /V2 ('/DPROBE_ROOT='+$taskRoot) ('/DPROBE_OUTPUT='+$taskProbe) (Join-Path $PSScriptRoot 'installer/PathProbe.nsi')
if($LASTEXITCODE -ne 0){throw 'Native path probe compilation failed.'}
Assert-ProbeExit @('/S') 0
if(Test-Path -LiteralPath $taskRoot){throw 'Path validation created its nonexistent destination.'}
Assert-ProbeExit @('/S',('/D='+$taskFixture+'\Unexpected destination')) 2
$taskTarget=Join-Path $taskFixture 'Junction target'
New-Item -ItemType Directory -Path $taskTarget | Out-Null
[IO.File]::WriteAllText((Join-Path $taskTarget 'keep.txt'),'Fictional path guard sentinel')
New-Item -ItemType Junction -Path $taskContainer -Target $taskTarget | Out-Null
Assert-ProbeExit @('/S') 2
if([IO.File]::ReadAllText((Join-Path $taskTarget 'keep.txt')) -ne 'Fictional path guard sentinel' -or (Test-Path -LiteralPath (Join-Path $taskTarget 'New app with spaces'))){throw 'Junction rejection changed its target.'}
Write-Output 'Native guards preserve a nonexistent destination, reject a different destination, and reject a junction ancestor without creating app files.'
