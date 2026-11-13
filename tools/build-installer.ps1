param(
    [string]$PublishDirectory,
    [string]$NsisCommand='makensis.exe',
    [ValidatePattern('^$|^-QA[a-zA-Z0-9-]{1,24}$')][string]$ReviewSuffix='',
    [string]$ReviewInstallDirectory
)
$ErrorActionPreference='Stop'
$taskRepository=Split-Path $PSScriptRoot -Parent
Import-Module (Join-Path $PSScriptRoot 'InstallerPackage.psm1') -Force
if(-not $PublishDirectory){$PublishDirectory=Join-Path $taskRepository '.artifacts/publish/win-x64'}
$taskOutput=Join-Path $taskRepository '.artifacts/installer'

function Get-ReviewRoot {
    if(-not $ReviewInstallDirectory){return $null}
    $taskAllowed=[IO.Path]::GetFullPath((Join-Path $taskRepository '.artifacts/installer-review'))+[IO.Path]::DirectorySeparatorChar
    $taskReview=[IO.Path]::GetFullPath($ReviewInstallDirectory)
    if(-not $ReviewSuffix -or $taskReview -match '[\r\n"$]' -or -not $taskReview.StartsWith($taskAllowed,[StringComparison]::OrdinalIgnoreCase)){
        throw 'Use a QA suffix and an isolated installer-review folder for a custom test destination.'
    }
    return $taskReview
}

function Confirm-InstallerInputs {
    if((& $NsisCommand /VERSION) -ne 'v3.13'){throw 'Build the preview installer with NSIS 3.13.'}
    foreach($taskFile in @('SarahBeauty.Desktop.exe','SarahBeauty.Recovery.exe','coreclr.dll','Microsoft.UI.Xaml.dll')){
        if(-not (Test-Path -LiteralPath (Join-Path $PublishDirectory $taskFile) -PathType Leaf)){throw ('Publish the complete app first: '+$taskFile)}
    }
}

Confirm-InstallerInputs
$taskReviewRoot=Get-ReviewRoot
$taskCount=Write-InstallerPackageIncludes $PublishDirectory (Join-Path $taskOutput 'includes')
$taskVersion=[version]([Diagnostics.FileVersionInfo]::GetVersionInfo((Get-Item -LiteralPath (Join-Path $PublishDirectory 'SarahBeauty.Desktop.exe')).FullName).FileVersion)
$taskInstaller=Join-Path $taskOutput ('SarahBeautyDesktopPreview-'+$taskVersion+$ReviewSuffix+'.exe')
$taskArguments=@('/NOCONFIG','/WX','/V2',('/DPACKAGE_INCLUDES='+(Join-Path $taskOutput 'includes')),
    ('/DINSTALLER_OUTPUT='+$taskInstaller),('/DREVIEW_SUFFIX='+$ReviewSuffix),('/DAPP_VERSION='+$taskVersion))
if($taskReviewRoot){$taskArguments+=('/DINSTALL_ROOT='+$taskReviewRoot)}
& $NsisCommand @taskArguments (Join-Path $PSScriptRoot 'installer/Desktop.nsi')
if($LASTEXITCODE -ne 0){throw 'Preview installer compilation failed.'}
Write-Output ('Built an unsigned preview installer for '+$taskCount+' app files: '+$taskInstaller)
