$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'InstallerPackage.psm1') -Force
$taskRepository=Split-Path $PSScriptRoot -Parent
$taskFixture=Join-Path $taskRepository ('.artifacts/installer-package-checks/'+[Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $taskFixture -Force | Out-Null

function Assert-Rejected([scriptblock]$Action,[string]$Expected) {
    try{& $Action}catch{if($_.Exception.Message.Contains($Expected)){return};throw}
    throw ('Expected package rejection: '+$Expected)
}

function New-PackageFile([string]$Case,[string]$Relative) {
    $taskPath=Join-Path (Join-Path $taskFixture $Case) $Relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskPath)) -Force | Out-Null
    [IO.File]::WriteAllText($taskPath,'Fictional installer file-map probe')
    return Join-Path $taskFixture $Case
}

$taskValid=New-PackageFile 'valid' 'ThirdParty/Nested/price$check.txt'
$taskOutput=Join-Path $taskFixture 'includes'
if((Write-InstallerPackageIncludes $taskValid $taskOutput) -ne 1){throw 'The valid package file was not retained.'}
$taskRemoval=Get-Content -LiteralPath (Join-Path $taskOutput 'RemoveFiles.nsh') -Raw
if(-not $taskRemoval.Contains('price$$check.txt') -or -not $taskRemoval.Contains('RMDir "$INSTDIR\ThirdParty"')){throw 'Literal filenames or parent directories were lost.'}
if($taskRemoval -match 'RMDir\s+/r|Delete[^\r\n]*[?*]'){throw 'Removal includes contain a recursive or wildcard deletion.'}

foreach($taskName in @('business.db','business.db-wal','business.db-shm','business.sqlite','business.sqlite3','backup.age')){
    $taskCase=New-PackageFile ('blocked-'+$taskName) $taskName
    Assert-Rejected {Get-InstallerPackageFiles $taskCase} 'cannot be installed as app files'
}
$taskSyntax=New-PackageFile 'syntax' '${UnexpectedMacro}.txt'
Assert-Rejected {Get-InstallerPackageFiles $taskSyntax} 'unsupported installer syntax'
foreach($taskHeader in @('SQLite format 3','age-encryption.org/v1')){
    $taskHidden=New-PackageFile ([Guid]::NewGuid().ToString()) 'renamed.dat'
    [IO.File]::WriteAllText((Join-Path $taskHidden 'renamed.dat'),$taskHeader)
    Assert-Rejected {Get-InstallerPackageFiles $taskHidden} 'cannot be installed as app files'
}
$taskOutside=New-PackageFile 'outside' 'untouched.txt'
$taskLinked=Join-Path $taskFixture 'linked'
New-Item -ItemType Directory -Path $taskLinked | Out-Null
New-Item -ItemType Junction -Path (Join-Path $taskLinked 'redirect') -Target $taskOutside | Out-Null
Assert-Rejected {Get-InstallerPackageFiles $taskLinked} 'links or junctions'
Assert-Rejected {Write-InstallerPackageIncludes $taskValid (Join-Path $taskLinked 'redirect/new-output')} 'links or junctions'
if(Test-Path -LiteralPath (Join-Path $taskOutside 'new-output')){throw 'A rejected output link created a directory outside the destination.'}
Write-Output 'Installer file-map checks pass: literal filenames, parent folders, six data extensions, renamed SQLite/age headers, macro rejection, linked-source rejection and linked-output rejection.'
