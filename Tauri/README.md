# Fichadas Colombraro — App de Escritorio (Tauri)

Empaqueta el frontend React y el backend .NET 8 en un único instalador Windows (`.msi` / `.exe`).
El backend corre como **sidecar** dentro de la app y se conecta al SQL Server remoto configurado en `appsettings.json`.

## Estructura

```
Tauri/
├── src-tauri/
│   ├── Cargo.toml
│   ├── tauri.conf.json        # Config Tauri v2 (ventana, bundle, recursos)
│   ├── build.rs
│   ├── capabilities/
│   │   └── default.json       # Permisos Tauri v2
│   ├── icons/                 # (pendiente — ver sección Iconos)
│   ├── resources/
│   │   └── backend/           # .NET publicado (lo genera el script)
│   └── src/
│       ├── main.rs
│       └── lib.rs             # Spawnea el backend al iniciar, lo mata al cerrar
├── dist/                      # Frontend compilado (lo genera el script)
└── scripts/
    ├── build-prod.ps1         # Orquesta todo el build
    └── build-prod.sh          # Equivalente bash
```

## Prerequisitos

- **Rust + Cargo** (`rustup` recomendado)
- **Tauri CLI v2**: `cargo install tauri-cli --version "^2.0" --locked`
- **Node.js 20+** y **npm**
- **.NET 8 SDK** (`dotnet --version` debe decir 8.x)
- **Microsoft Visual Studio Build Tools** con C++ (ya suelen estar con Rust)
- **WebView2 Runtime** (incluido en Windows 11)

## Configuración de conexión al SQL Server

El backend lee la connection string desde `Backend/FichadasAPI/appsettings.json`.
Antes de empacar, asegurarse de que `ConnectionStrings:FichadasDB` apunte al servidor correcto:

```json
"ConnectionStrings": {
  "FichadasDB": "Server=testserver.dynalias.net,1433;Database=FichadasDB;User Id=sa;Password=XXXX;TrustServerCertificate=True;"
}
```

Este archivo se copia dentro del bundle durante `build-prod.ps1` (va junto al `FichadasAPI.exe` publicado).

> Para permitir cambiar la connection string post-instalación sin re-empacar:
> editar `C:\Program Files\Colombraro Fichadas\resources\backend\appsettings.json` en la máquina cliente.

## Desarrollo (hot reload)

En **dos terminales**, desde la raíz del repo:

```bash
# Terminal 1 — backend
cd Backend/FichadasAPI
dotnet watch run        # queda escuchando en http://localhost:5210

# Terminal 2 — Tauri + Vite
cd Frontend
npm run dev             # sirve el front en http://localhost:5173

# Terminal 3 — app Tauri
cd Tauri/src-tauri
cargo tauri dev         # abre la ventana apuntando a localhost:5173
```

En modo dev, Tauri **no** spawnea el sidecar (el backend lo corrés vos con `dotnet watch`). La lógica en `lib.rs` sólo intenta spawnear si el binario empacado existe en la ruta de recursos, que únicamente se popula al hacer `build-prod`.

## Build de producción (instalador)

```powershell
# Desde la raíz del repo
powershell -ExecutionPolicy Bypass -File Tauri/scripts/build-prod.ps1

# Luego
cd Tauri/src-tauri
cargo tauri build
```

El instalador queda en `Tauri/src-tauri/target/release/bundle/msi/` (y `/nsis/`).

Lo que hace `build-prod.ps1`:
1. `npm run build` del frontend con `VITE_API_URL=http://localhost:5210/api`.
2. Copia `Frontend/dist` → `Tauri/dist`.
3. `dotnet publish -c Release -r win-x64 --self-contained` del backend.
4. Copia la salida a `Tauri/src-tauri/resources/backend/` (queda bundleado por Tauri).

## Iconos

Antes del primer `cargo tauri build` hace falta generar los iconos.
Partiendo de un PNG cuadrado de al menos 1024×1024:

```bash
cd Tauri/src-tauri
cargo tauri icon ../../ruta/al/icono.png
```

Esto genera `32x32.png`, `128x128.png`, `128x128@2x.png`, `icon.ico`, `icon.icns`
en `src-tauri/icons/`, que es lo que referencia `tauri.conf.json`.

## Cómo funciona el sidecar

`src-tauri/src/lib.rs` al hacer `setup()`:

1. Resuelve la ruta del recurso `backend/FichadasAPI.exe` (se monta en `resources/` dentro del bundle instalado).
2. Si existe, lo spawnea como proceso hijo (`CREATE_NO_WINDOW` en Windows — sin consola).
3. Le setea `ASPNETCORE_URLS=http://127.0.0.1:5210` y `ASPNETCORE_ENVIRONMENT=Production`.
4. Guarda el `Child` en el estado Tauri.
5. Al recibir `RunEvent::Exit`, lo mata con `child.kill()`.

El frontend sigue hablando HTTP con `http://localhost:5210/api` como siempre — no hay IPC Tauri de por medio.

## Troubleshooting

- **"No se pudo iniciar el backend .NET"** en logs: el exe no está en `resources/backend/`. Correr `build-prod.ps1` antes de `cargo tauri build`.
- **La app abre pero las llamadas fallan con ERR_CONNECTION_REFUSED**: el backend no arrancó — probablemente la connection string apunta a un SQL Server inaccesible. Revisar `appsettings.json` dentro del bundle.
- **CORS**: el backend tiene `AllowAnyOrigin` en `Program.cs`, así que la WebView de Tauri puede llamar sin problemas.
- **Puerto 5210 ocupado**: cambiar `ASPNETCORE_URLS` en `lib.rs` y `VITE_API_URL` en `scripts/build-prod.ps1` al mismo puerto nuevo.
