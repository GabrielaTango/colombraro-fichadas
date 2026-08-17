# Equivalente PowerShell de build-prod.sh.
# Compila el frontend, lo copia a Tauri/dist y publica el backend self-contained
# en Tauri/src-tauri/resources/backend para que cargo tauri build lo empaquete.

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$TauriDir = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent $TauriDir

$FrontendDir = Join-Path $RepoRoot "Frontend"
$BackendProj = Join-Path $RepoRoot "Backend\FichadasAPI\FichadasAPI.csproj"
$FrontendDistSrc = Join-Path $FrontendDir "dist"
$FrontendDistDest = Join-Path $TauriDir "dist"
$BackendPublishDir = Join-Path $TauriDir "src-tauri\resources\backend"

Write-Host "==> 1/3 Compilando frontend React..." -ForegroundColor Cyan
Push-Location $FrontendDir
try {
    if (-not (Test-Path "node_modules")) { npm install; if ($LASTEXITCODE -ne 0) { throw "npm install fallo" } }
    $env:VITE_API_URL = "http://localhost:5210/api"
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build fallo" }
} finally {
    Pop-Location
}

Write-Host "==> Copiando dist del frontend a Tauri/dist..." -ForegroundColor Cyan
if (Test-Path $FrontendDistDest) { Remove-Item -Recurse -Force $FrontendDistDest }
Copy-Item -Recurse $FrontendDistSrc $FrontendDistDest

Write-Host "==> 2/3 Publicando backend .NET (self-contained win-x64)..." -ForegroundColor Cyan
if (Test-Path $BackendPublishDir) { Remove-Item -Recurse -Force $BackendPublishDir }
New-Item -ItemType Directory -Path $BackendPublishDir | Out-Null
dotnet publish $BackendProj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $BackendPublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo" }

Write-Host "==> 3/3 Listo. Ahora ejecutar:" -ForegroundColor Green
Write-Host "    cd Tauri/src-tauri; cargo tauri build" -ForegroundColor Yellow
