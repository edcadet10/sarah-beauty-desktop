Set-StrictMode -Version Latest

function ConvertTo-NsisLiteral([string]$Value) {
    if($Value -match '[\r\n"]' -or $Value.Contains('${')){throw 'A package path contains unsupported installer syntax.'}
    return $Value.Replace('$','$$')
}

function Assert-PackageDirectory([string]$Path) {
    $taskCurrent=[IO.Path]::GetFullPath($Path)
    while($taskCurrent){
        $taskItem=Get-Item -LiteralPath $taskCurrent -Force -ErrorAction Stop
        if(($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Package directories cannot use links or junctions.'}
        $taskCurrent=[IO.Path]::GetDirectoryName($taskCurrent)
    }
}

function Get-InstallerPackageFiles([string]$PublishDirectory) {
    $taskRoot=[IO.Path]::GetFullPath($PublishDirectory)
    Assert-PackageDirectory $taskRoot
    if(-not (Get-Item -LiteralPath $taskRoot -Force).PSIsContainer){throw 'Choose a published app directory.'}
    $taskPending=[Collections.Generic.Stack[string]]::new();$taskPending.Push($taskRoot)
    while($taskPending.Count -gt 0){
        foreach($taskItem in Get-ChildItem -LiteralPath $taskPending.Pop() -Force -ErrorAction Stop){
            if(($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Package entries cannot use links or junctions.'}
            if($taskItem.PSIsContainer){$taskPending.Push($taskItem.FullName);continue}
            Assert-NoBusinessArtifact $taskItem
            $taskRelative=[IO.Path]::GetRelativePath($taskRoot,$taskItem.FullName)
            $null=ConvertTo-NsisLiteral $taskRelative
            [pscustomobject]@{Source=$taskItem.FullName;Relative=$taskRelative;Directory=[IO.Path]::GetDirectoryName($taskRelative)}
        }
    }
}

function Assert-NoBusinessArtifact([IO.FileInfo]$File) {
    $taskStream=$File.OpenRead()
    try{
        $taskHeader=[byte[]]::new(64);$taskRead=$taskStream.Read($taskHeader,0,$taskHeader.Length)
        $taskPrefix=[Text.Encoding]::ASCII.GetString($taskHeader,0,$taskRead)
    }finally{$taskStream.Dispose()}
    $taskBusinessHeader=$taskPrefix.StartsWith('SQLite format 3') -or $taskPrefix.StartsWith('age-encryption.org/') -or
        $taskPrefix.StartsWith('-----BEGIN AGE ENCRYPTED FILE-----')
    if($File.Name -match '\.(db|sqlite|sqlite3)(-(wal|shm))?$|\.age$' -or $taskBusinessHeader){
        throw 'Business databases and recovery archives cannot be installed as app files.'
    }
}

function Get-InstallerCopyLines([object[]]$Files) {
    foreach($taskFile in $Files){
        $taskDirectory=ConvertTo-NsisLiteral $taskFile.Directory
        $taskSource=ConvertTo-NsisLiteral $taskFile.Source
        $taskName=ConvertTo-NsisLiteral ([IO.Path]::GetFileName($taskFile.Relative))
        'SetOutPath "$INSTDIR'+$(if($taskDirectory){'\'+$taskDirectory})+'"'
        'File "/oname='+$taskName+'" "'+$taskSource+'"'
    }
}

function Get-InstallerRemovalLines([object[]]$Files) {
    foreach($taskFile in $Files){'Delete "$INSTDIR\'+(ConvertTo-NsisLiteral $taskFile.Relative)+'"'}
    $taskDirectories=@(Get-InstallerDirectories $Files | Sort-Object -Unique | Sort-Object Length -Descending)
    foreach($taskDirectory in $taskDirectories){'RMDir "$INSTDIR\'+(ConvertTo-NsisLiteral $taskDirectory)+'"'}
}

function Get-InstallerDirectories([object[]]$Files) {
    foreach($taskFile in $Files){
        $taskDirectory=$taskFile.Directory
        while($taskDirectory){$taskDirectory;$taskDirectory=[IO.Path]::GetDirectoryName($taskDirectory)}
    }
}

function Get-InstallerPathChecks([object[]]$Files) {
    'Push "$INSTDIR"'
    'Call ${PATH_GUARD_PREFIX}RequirePlainPath'
    foreach($taskDirectory in @(Get-InstallerDirectories $Files | Sort-Object -Unique)){
        'Push "$INSTDIR\'+(ConvertTo-NsisLiteral $taskDirectory)+'"'
        'Call ${PATH_GUARD_PREFIX}RequirePlainPath'
    }
}

function Write-InstallerPackageIncludes([string]$PublishDirectory,[string]$Destination) {
    $taskFiles=@(Get-InstallerPackageFiles $PublishDirectory | Sort-Object Relative)
    if($taskFiles.Count -eq 0){throw 'Publish the app before generating installer file includes.'}
    $taskOutput=[IO.Path]::GetFullPath($Destination)
    $taskExisting=$taskOutput
    while(-not (Test-Path -LiteralPath $taskExisting)){$taskExisting=[IO.Path]::GetDirectoryName($taskExisting)}
    Assert-PackageDirectory $taskExisting
    New-Item -ItemType Directory -Path $taskOutput -Force -ErrorAction Stop | Out-Null
    Assert-PackageDirectory $taskOutput
    [IO.File]::WriteAllLines((Join-Path $taskOutput 'InstallFiles.nsh'),[string[]]@(Get-InstallerCopyLines $taskFiles),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllLines((Join-Path $taskOutput 'RemoveFiles.nsh'),[string[]]@(Get-InstallerRemovalLines $taskFiles),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllLines((Join-Path $taskOutput 'CheckPaths.nsh'),[string[]]@(Get-InstallerPathChecks $taskFiles),[Text.UTF8Encoding]::new($false))
    return $taskFiles.Count
}

Export-ModuleMember -Function Get-InstallerPackageFiles,Write-InstallerPackageIncludes
