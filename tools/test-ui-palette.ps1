$ErrorActionPreference='Stop'
[xml]$taskPalette=Get-Content (Join-Path $PSScriptRoot '../src/SarahBeauty.Desktop/BrandResources.xaml') -Raw

function Get-Luminance([string]$Color) {
    $taskChannels=foreach($taskOffset in @(1,3,5)){
        $taskValue=[Convert]::ToInt32($Color.Substring($taskOffset,2),16)/255.0
        if($taskValue -le 0.04045){$taskValue/12.92}else{[Math]::Pow(($taskValue+0.055)/1.055,2.4)}
    }
    return 0.2126*$taskChannels[0]+0.7152*$taskChannels[1]+0.0722*$taskChannels[2]
}

function Assert-Contrast($Brushes,[string]$Foreground,[string]$Background,[double]$Minimum=4.5) {
    $taskFirst=Get-Luminance $Brushes[$Foreground];$taskSecond=Get-Luminance $Brushes[$Background]
    $taskRatio=([Math]::Max($taskFirst,$taskSecond)+0.05)/([Math]::Min($taskFirst,$taskSecond)+0.05)
    if($taskRatio -lt $Minimum){throw "$Foreground on $Background fails: $taskRatio"}
    Write-Output ('{0} on {1}: {2:N2}:1' -f $Foreground,$Background,$taskRatio)
}

foreach($taskTheme in $taskPalette.DocumentElement.ChildNodes[0].ChildNodes){
    $taskName=$taskTheme.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml')
    if($taskName -eq 'HighContrast'){continue}
    $taskBrushes=@{};foreach($taskBrush in $taskTheme.ChildNodes){if($taskBrush.LocalName -eq 'SolidColorBrush'){$taskBrushes[$taskBrush.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml')]=$taskBrush.Color}}
    Write-Output $taskName
    foreach($taskSurface in @('SarahCanvasBrush','SarahSurfaceBrush','SarahSoftBrush')){
        foreach($taskText in @('SarahTextBrush','SarahMutedBrush','AccentTextFillColorPrimaryBrush','AccentTextFillColorSecondaryBrush','AccentTextFillColorTertiaryBrush')){
            Assert-Contrast $taskBrushes $taskText $taskSurface
        }
        foreach($taskGraphic in @('SarahAccentBrush','SarahGoldBrush','ChartBlueBrush','ChartPurpleBrush')){
            Assert-Contrast $taskBrushes $taskGraphic $taskSurface 3
        }
    }
    foreach($taskFill in @('AccentFillColorDefaultBrush','AccentFillColorSecondaryBrush','AccentFillColorTertiaryBrush')){
        Assert-Contrast $taskBrushes 'TextOnAccentFillColorPrimaryBrush' $taskFill
        Assert-Contrast $taskBrushes 'TextOnAccentFillColorSecondaryBrush' $taskFill
    }
}
