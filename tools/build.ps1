$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"

if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
    Write-Host "Using system dotnet (local SDK not found)." -ForegroundColor Yellow
}

Write-Host "=== SmartCopy build pipeline ===" -ForegroundColor Cyan

Push-Location $root
try {
    Write-Host "`n[1/4] Restoring + building solution..."
    & $dotnet build SmartCopy.sln -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }

    Write-Host "`n[2/4] Running tests..."
    & $dotnet test SmartCopy.sln -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

    Write-Host "`n[3/4] Publishing self-contained single-file exe..."
    if (Test-Path (Join-Path $root "publish")) { Remove-Item (Join-Path $root "publish") -Recurse -Force }
    & $dotnet publish src/SmartCopy.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -o publish --nologo
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

    Write-Host "`n[4/4] Building installer..."
    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $iscc)) {
        Write-Host "Inno Setup not found at $iscc - skipping installer." -ForegroundColor Yellow
    } else {
        & $iscc "installer\smartcopy.iss"
        if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }
    }
}
finally {
    Pop-Location
}

Write-Host "`n=== Pipeline complete ===" -ForegroundColor Green
