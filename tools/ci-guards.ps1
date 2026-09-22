$ErrorActionPreference = "Stop"

Write-Host "Running CI Guard 1: Checking for hardcoded hex colors in Views (*.axaml)..."
$colorMatches = Get-ChildItem -Path "src/QpdfGui.App/Views" -Recurse -Filter *.axaml | Select-String -Pattern '#[0-9A-Fa-f]{6,8}'
if ($colorMatches) {
    Write-Error "Found hardcoded hex colors in Views:`n$($colorMatches | Out-String)"
    exit 1
}
Write-Host "✓ No hardcoded hex colors found in Views."

Write-Host "Running CI Guard 2: Checking for Chinese string literals in Core (*.cs)..."
$chineseMatches = Get-ChildItem -Path "src/QpdfGui.Core" -Recurse -Filter *.cs | 
    Select-String -Pattern '"[^"\r\n]*[\u4e00-\u9fa5][^"\r\n]*"' | 
    Where-Object { 
        $line = $_.Line.Trim()
        -not ($line.StartsWith("//") -or $line.StartsWith("/*") -or $line.StartsWith("*"))
    }
if ($chineseMatches) {
    Write-Error "Found Chinese string literals in Core code:`n$($chineseMatches | Out-String)"
    exit 1
}
Write-Host "✓ No Chinese string literals found in Core code."

Write-Host "All CI guards passed successfully."
