param([string]$GoCommand='go')
$ErrorActionPreference='Stop'
$taskRepository=Split-Path $PSScriptRoot -Parent
$taskGo=Get-Command $GoCommand -ErrorAction SilentlyContinue
if($null -eq $taskGo){
    $taskFallback=Join-Path $taskRepository '.artifacts/toolchains/go1.27.4/go/bin/go.exe'
    if(-not (Test-Path -LiteralPath $taskFallback)){throw 'Install Go 1.27.4 or pass its executable path with -GoCommand.'}
    $taskGoPath=$taskFallback
}else{$taskGoPath=$taskGo.Source}
$taskOutput=Join-Path $taskRepository '.artifacts/recovery-helper'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$env:GOTOOLCHAIN='local'
$env:CGO_ENABLED='0'
Push-Location (Join-Path $PSScriptRoot 'SarahBeauty.Recovery')
try {
    & $taskGoPath build -trimpath -ldflags '-s -w' -o (Join-Path $taskOutput 'SarahBeauty.Recovery.exe') .
    if($LASTEXITCODE -ne 0){throw 'The recovery helper build failed.'}
}finally{Pop-Location}
$taskNotices=Join-Path $taskOutput 'ThirdParty'
New-Item -ItemType Directory -Path $taskNotices -Force | Out-Null
foreach($taskNotice in @('age-1.3.2-license.txt','hpke-0.4.0-license.txt','golang-crypto-0.55.0-license.txt','golang-sys-0.47.0-license.txt','go-1.27.4-license.txt')){
    Copy-Item -LiteralPath (Join-Path $taskRepository ('docs/third-party/'+$taskNotice)) -Destination (Join-Path $taskNotices $taskNotice) -Force
}
Write-Output 'Built the recovery helper with its third-party notices in .artifacts/recovery-helper.'
