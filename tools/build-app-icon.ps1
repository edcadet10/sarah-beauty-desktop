$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskAssets = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/SarahBeauty.Desktop/Assets'
[xml]$taskMark = Get-Content -LiteralPath (Join-Path $taskAssets 'AppMark.svg') -Raw

# This renderer supports the move, cubic curve and close commands in AppMark.svg.
function New-MarkPath([string]$Data) {
    $taskTokens = [regex]::Matches($Data, '[MCZ]|-?\d+(?:\.\d+)?') | ForEach-Object Value
    $taskPath = [Drawing.Drawing2D.GraphicsPath]::new()
    $taskIndex = 0
    $taskCurrent = [Drawing.PointF]::new(0, 0)
    while ($taskIndex -lt $taskTokens.Count) {
        switch ($taskTokens[$taskIndex++]) {
            'M' {
                $taskCurrent = [Drawing.PointF]::new([single]$taskTokens[$taskIndex++], [single]$taskTokens[$taskIndex++])
                $taskPath.StartFigure()
            }
            'C' {
                $taskA = [Drawing.PointF]::new([single]$taskTokens[$taskIndex++], [single]$taskTokens[$taskIndex++])
                $taskB = [Drawing.PointF]::new([single]$taskTokens[$taskIndex++], [single]$taskTokens[$taskIndex++])
                $taskEnd = [Drawing.PointF]::new([single]$taskTokens[$taskIndex++], [single]$taskTokens[$taskIndex++])
                $taskPath.AddBezier($taskCurrent, $taskA, $taskB, $taskEnd)
                $taskCurrent = $taskEnd
            }
            'Z' { $taskPath.CloseFigure() }
        }
    }
    return ,$taskPath
}

function New-TilePath($Rect) {
    $taskPath = [Drawing.Drawing2D.GraphicsPath]::new()
    $taskDiameter = 2 * [single]$Rect.rx
    $taskX = [single]$Rect.x
    $taskY = [single]$Rect.y
    $taskRight = $taskX + [single]$Rect.width - $taskDiameter
    $taskBottom = $taskY + [single]$Rect.height - $taskDiameter
    $taskPath.AddArc($taskX, $taskY, $taskDiameter, $taskDiameter, 180, 90)
    $taskPath.AddArc($taskRight, $taskY, $taskDiameter, $taskDiameter, 270, 90)
    $taskPath.AddArc($taskRight, $taskBottom, $taskDiameter, $taskDiameter, 0, 90)
    $taskPath.AddArc($taskX, $taskBottom, $taskDiameter, $taskDiameter, 90, 90)
    $taskPath.CloseFigure()
    return ,$taskPath
}

function Paint-Mark([Drawing.Graphics]$Graphics) {
    $taskShapes = @(@{ Path = (New-TilePath $taskMark.svg.rect); Fill = $taskMark.svg.rect.fill })
    $taskShapes += @($taskMark.svg.path | ForEach-Object { @{ Path = (New-MarkPath $_.d); Fill = $_.fill } })
    foreach ($taskShape in $taskShapes) {
        $taskBrush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($taskShape.Fill))
        try { $Graphics.FillPath($taskBrush, $taskShape.Path) }
        finally { $taskBrush.Dispose(); $taskShape.Path.Dispose() }
    }
}

function Get-IconFrame([int]$Size) {
    $taskBitmap = [Drawing.Bitmap]::new($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $taskGraphics = [Drawing.Graphics]::FromImage($taskBitmap)
    $taskPng = [IO.MemoryStream]::new()
    try {
        $taskGraphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $taskGraphics.ScaleTransform($Size / 256.0, $Size / 256.0)
        Paint-Mark $taskGraphics
        $taskBitmap.Save($taskPng, [Drawing.Imaging.ImageFormat]::Png)
        return ,$taskPng.ToArray()
    }
    finally { $taskPng.Dispose(); $taskGraphics.Dispose(); $taskBitmap.Dispose() }
}

function Write-Icon([int[]]$Sizes, [object[]]$Frames, [string]$Path) {
    $taskOutput = [IO.MemoryStream]::new()
    $taskWriter = [IO.BinaryWriter]::new($taskOutput)
    try {
        $taskWriter.Write([uint16]0)
        $taskWriter.Write([uint16]1)
        $taskWriter.Write([uint16]$Sizes.Count)
        $taskOffset = 6 + 16 * $Sizes.Count
        for ($taskIndex = 0; $taskIndex -lt $Sizes.Count; $taskIndex++) {
            $taskDimension = if ($Sizes[$taskIndex] -eq 256) { 0 } else { $Sizes[$taskIndex] }
            $taskWriter.Write([byte]$taskDimension)
            $taskWriter.Write([byte]$taskDimension)
            $taskWriter.Write([uint16]0)
            $taskWriter.Write([uint16]1)
            $taskWriter.Write([uint16]32)
            $taskWriter.Write([uint32]$Frames[$taskIndex].Length)
            $taskWriter.Write([uint32]$taskOffset)
            $taskOffset += $Frames[$taskIndex].Length
        }
        foreach ($taskFrame in $Frames) { $taskWriter.Write([byte[]]$taskFrame) }
        $taskWriter.Flush()
        [IO.File]::WriteAllBytes($Path, $taskOutput.ToArray())
    }
    finally { $taskWriter.Dispose(); $taskOutput.Dispose() }
}

$taskSizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$taskFrames = @($taskSizes | ForEach-Object { Get-IconFrame $_ })
Write-Icon $taskSizes $taskFrames (Join-Path $taskAssets 'AppIcon.ico')
Write-Output 'Generated the app icon from its vector source at ten sizes, 16 through 256 pixels.'
