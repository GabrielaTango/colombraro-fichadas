#!/usr/bin/env bash
# Equivalente bash de build-prod.ps1 para usar desde Git Bash / WSL.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TAURI_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(dirname "$TAURI_DIR")"

FRONTEND_DIR="$REPO_ROOT/Frontend"
BACKEND_PROJ="$REPO_ROOT/Backend/FichadasAPI/FichadasAPI.csproj"
FRONTEND_DIST_SRC="$FRONTEND_DIR/dist"
FRONTEND_DIST_DEST="$TAURI_DIR/dist"
BACKEND_PUBLISH_DIR="$TAURI_DIR/src-tauri/resources/backend"

echo "==> 1/3 Compilando frontend React..."
cd "$FRONTEND_DIR"
if [ ! -d node_modules ]; then npm install; fi
VITE_API_URL="http://localhost:5210/api" npm run build

echo "==> Copiando dist del frontend a Tauri/dist..."
rm -rf "$FRONTEND_DIST_DEST"
cp -r "$FRONTEND_DIST_SRC" "$FRONTEND_DIST_DEST"

echo "==> 2/3 Publicando backend .NET (self-contained win-x64)..."
rm -rf "$BACKEND_PUBLISH_DIR"
mkdir -p "$BACKEND_PUBLISH_DIR"
dotnet publish "$BACKEND_PROJ" \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=true \
    -o "$BACKEND_PUBLISH_DIR"

echo "==> 3/3 Todo listo. Ahora ejecutar:"
echo "    cd Tauri/src-tauri && cargo tauri build"
