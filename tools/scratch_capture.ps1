Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$rootDir = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $rootDir "src\QpdfGui.App\bin\Debug\net10.0\QpdfGui.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Executable not found. Building project..."
    dotnet build (Join-Path $rootDir "src\QpdfGui.App\QpdfGui.App.csproj")
}

$proc = Start-Process -FilePath $exePath -PassThru
Start-Sleep -Seconds 4

try {
    $proc.Refresh()
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen(0, 0, 0, 0, $bitmap.Size)
    $scratchDir = Join-Path $rootDir "scratch"
    if (-not (Test-Path $scratchDir)) { New-Item -ItemType Directory -Path $scratchDir -Force | Out-Null }
    $outPath = Join-Path $scratchDir "preview.png"
    $bitmap.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Output "Screenshot saved to $outPath"
}
finally {
    if ($proc -and !$proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }
}
