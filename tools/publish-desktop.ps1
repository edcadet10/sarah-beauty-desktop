param([string]$GoCommand='go')
$ErrorActionPreference='Stop'
$taskRepository=Split-Path $PSScriptRoot -Parent
$taskPublish=Join-Path $taskRepository '.artifacts/publish/win-x64'

function Build-DesktopRecovery {
    & (Join-Path $PSScriptRoot 'build-recovery.ps1') -GoCommand $GoCommand
    if($LASTEXITCODE -ne 0){throw 'Build the recovery helper before publishing the desktop app.'}
}

function Publish-Desktop {
    $taskProject=Join-Path $taskRepository 'src/SarahBeauty.Desktop/SarahBeauty.Desktop.csproj'
    & dotnet publish $taskProject -c Release -r win-x64 -p:PublishProfile=win-x64 -o $taskPublish --nologo
    if($LASTEXITCODE -ne 0){throw 'The self-contained Windows publish failed.'}
}

function Confirm-PublishedRuntime {
    $taskRequired=@('SarahBeauty.Desktop.exe','SarahBeauty.Business.dll','SarahBeauty.Desktop.runtimeconfig.json',
        'SarahBeauty.Recovery.exe','coreclr.dll','Microsoft.UI.Xaml.dll',
        'ThirdParty/Recovery/age-1.3.2-license.txt','ThirdParty/DotNet/LICENSE.txt',
        'ThirdParty/WindowsAppSDK/LICENSE.txt','ThirdParty/WindowsAppSDK/NOTICE.txt')
    foreach($taskFile in $taskRequired){
        if(-not (Test-Path -LiteralPath (Join-Path $taskPublish $taskFile) -PathType Leaf)){
            throw ('The published app is missing a required file: '+$taskFile)
        }
    }
    $taskConfig=Get-Content -LiteralPath (Join-Path $taskPublish 'SarahBeauty.Desktop.runtimeconfig.json') -Raw | ConvertFrom-Json
    if(-not $taskConfig.runtimeOptions.includedFrameworks){throw 'The publish did not bundle the .NET runtime.'}
    if($taskConfig.runtimeOptions.configProperties.'System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault' -eq $false){
        throw 'The published app disables the JSON reflection required by its saved records.'
    }
}

Push-Location $taskRepository
try {
    Build-DesktopRecovery
    Publish-Desktop
    Confirm-PublishedRuntime
    Write-Output ('Published Windows x64 app with bundled runtimes and recovery helper: '+$taskPublish)
}finally{Pop-Location}
