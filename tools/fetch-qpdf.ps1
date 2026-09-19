[CmdletBinding()]
param(
    [string]$Version = "12.4.1",
    [string]$Rid = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$targetDir = Join-Path $rootDir "runtimes\$Rid\native"

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
}

Write-Host "Fetching QPDF v$Version for $Rid..."

if ($Rid -eq "win-x64") {
    $url = "https://github.com/qpdf/qpdf/releases/download/v$Version/qpdf-$Version-msvc64.zip"
    $tempZip = Join-Path ([System.IO.Path]::GetTempPath()) "qpdf-$Version-$([System.Guid]::NewGuid()).zip"
    $tempExtract = Join-Path ([System.IO.Path]::GetTempPath()) "qpdf-$Version-$([System.Guid]::NewGuid())"

    try {
        Write-Host "Downloading from $url..."
        Invoke-WebRequest -Uri $url -OutFile $tempZip -UseBasicParsing

        Write-Host "Extracting archive..."
        Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

        $binFolder = Join-Path $tempExtract "qpdf-$Version-msvc64\bin"
        if (-not (Test-Path $binFolder)) {
            $binFolder = (Get-ChildItem -Path $tempExtract -Recurse -Directory -Filter "bin" | Select-Object -First 1).FullName
        }

        if (-not $binFolder -or -not (Test-Path $binFolder)) {
            throw "Failed to locate 'bin' directory inside extracted package."
        }

        Write-Host "Copying binary and runtime DLLs to $targetDir..."
        Get-ChildItem -Path $binFolder -File | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $targetDir -Force
            Write-Host "  Copied: $($_.Name)"
        }

        $qpdfExe = Join-Path $targetDir "qpdf.exe"
        if (Test-Path $qpdfExe) {
            $verOutput = & $qpdfExe --version
            Write-Host "Successfully verified: $verOutput" -ForegroundColor Green
        } else {
            throw "qpdf.exe was not found in destination!"
        }
    }
    finally {
        if (Test-Path $tempZip) { Remove-Item -Force $tempZip -ErrorAction SilentlyContinue }
        if (Test-Path $tempExtract) { Remove-Item -Recurse -Force $tempExtract -ErrorAction SilentlyContinue }
    }
}
else {
    Write-Warning "Auto-download currently supports win-x64. For other platforms, please install qpdf natively or provide binaries in runtimes/$Rid/native."
}
