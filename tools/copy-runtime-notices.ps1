param([Parameter(Mandatory)][string]$PublishDirectory)
$ErrorActionPreference='Stop'
$taskNoticeRepository=Split-Path $PSScriptRoot -Parent
$taskNoticeSources=Join-Path $taskNoticeRepository 'docs/third-party'
$taskNoticeFallbacks=Get-Content (Join-Path $taskNoticeSources 'runtime-license-fallbacks.json') -Raw | ConvertFrom-Json -AsHashtable
$taskNoticeAssets=Get-Content (Join-Path $taskNoticeRepository 'src/SarahBeauty.Desktop/obj/project.assets.json') -Raw | ConvertFrom-Json

function Find-RuntimePackage([string]$Key){
    foreach($taskCache in $taskNoticeAssets.packageFolders.PSObject.Properties.Name){
        $taskCandidate=Join-Path $taskCache $Key.ToLowerInvariant()
        if(Test-Path -LiteralPath $taskCandidate -PathType Container){return $taskCandidate}
    }
    throw ('The restored runtime package is unavailable: '+$Key)
}

function Read-PackageLicense([string]$Folder){
    $taskSpecs=@(Get-ChildItem -LiteralPath $Folder -Filter '*.nuspec' -File)
    if($taskSpecs.Count -ne 1){throw 'Expected exactly one runtime package manifest.'}
    $taskSpec=[xml](Get-Content -LiteralPath $taskSpecs[0].FullName -Raw)
    $taskLicense=$taskSpec.package.metadata.license
    if(!$taskLicense -or $taskLicense.type -notin @('file','expression')){throw 'Review the runtime package license metadata before publishing.'}
    return $taskLicense
}

function Find-PackageNotices([string]$Key,[string]$Folder,$License){
    $taskFiles=@(Get-ChildItem -LiteralPath $Folder -File | Where-Object {$_.Name -match '(?i)license|notice|copyright|credits'})
    if($License.type -eq 'file'){
        $taskDeclared=[IO.Path]::GetFullPath((Join-Path $Folder $License.InnerText))
        if(!$taskDeclared.StartsWith($Folder+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'The declared license path leaves its package.'}
        $taskFiles+=Get-Item -LiteralPath $taskDeclared -ErrorAction Stop
    }elseif(!($taskFiles | Where-Object {$_.Name -match '(?i)license'})){
        $taskFallback=$taskNoticeFallbacks[$Key]
        if(!$taskFallback -or $taskFallback.license -ne $License.InnerText){throw ('Review and pin license sources for '+$Key)}
        foreach($taskName in $taskFallback.files){$taskFiles+=Get-Item -LiteralPath (Join-Path $taskNoticeSources $taskName) -ErrorAction Stop}
    }
    return @($taskFiles | Sort-Object FullName -Unique)
}

function Copy-PackageNotices([string]$Key,[IO.FileInfo[]]$Files){
    $taskRelative='ThirdParty/RuntimePackages/'+$Key
    $taskOutput=Join-Path $PublishDirectory $taskRelative
    $null=New-Item -ItemType Directory -Path $taskOutput -Force
    foreach($taskFile in $Files){
        $taskDestination=Join-Path $taskOutput $taskFile.Name
        Copy-Item -LiteralPath $taskFile.FullName -Destination $taskDestination -Force
        $taskHash=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
        if((Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash -ne $taskHash){throw 'The copied runtime notice differs from its source.'}
        [ordered]@{path=$taskRelative+'/'+$taskFile.Name;sha256=$taskHash}
    }
}

function Write-RuntimeNoticeInventory {
    $taskDependencies=Get-Content -LiteralPath (Join-Path $PublishDirectory 'SarahBeauty.Desktop.deps.json') -Raw | ConvertFrom-Json
    $taskInventory=@(foreach($taskPackage in $taskDependencies.libraries.PSObject.Properties | Sort-Object Name){
        if($taskPackage.Value.type -ne 'package'){continue}
        $taskFolder=Find-RuntimePackage $taskPackage.Name
        $taskLicense=Read-PackageLicense $taskFolder
        $taskFiles=Find-PackageNotices $taskPackage.Name $taskFolder $taskLicense
        $taskCopied=@(Copy-PackageNotices $taskPackage.Name $taskFiles)
        if(!$taskCopied.Count){throw 'The runtime package has no copied license documents.'}
        [ordered]@{package=$taskPackage.Name;licenseType=$taskLicense.type;license=$taskLicense.InnerText;files=$taskCopied}
    })
    if(!$taskInventory.Count){throw 'The published app declares no runtime packages.'}
    $taskInventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PublishDirectory 'ThirdParty/RuntimePackages/inventory.json') -Encoding utf8NoBOM
    Write-Output ('Copied and verified notices for '+$taskInventory.Count+' runtime NuGet packages.')
}

Write-RuntimeNoticeInventory
