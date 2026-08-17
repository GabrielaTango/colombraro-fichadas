use std::process::{Child, Command};
use std::sync::Mutex;
use tauri::{Manager, RunEvent};

#[cfg(target_os = "windows")]
use std::os::windows::process::CommandExt;

#[cfg(target_os = "windows")]
const CREATE_NO_WINDOW: u32 = 0x08000000;

struct BackendProcess(Mutex<Option<Child>>);

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(BackendProcess(Mutex::new(None)))
        .setup(|app| {
            if let Ok(resource_path) = app.path().resolve(
                "resources/backend/FichadasAPI.exe",
                tauri::path::BaseDirectory::Resource,
            ) {
                if resource_path.exists() {
                    let mut cmd = Command::new(&resource_path);
                    if let Some(parent) = resource_path.parent() {
                        cmd.current_dir(parent);
                    }
                    cmd.env("ASPNETCORE_URLS", "http://127.0.0.1:5210");
                    cmd.env("ASPNETCORE_ENVIRONMENT", "Production");

                    #[cfg(target_os = "windows")]
                    cmd.creation_flags(CREATE_NO_WINDOW);

                    match cmd.spawn() {
                        Ok(child) => {
                            let state = app.state::<BackendProcess>();
                            *state.0.lock().unwrap() = Some(child);
                        }
                        Err(e) => {
                            eprintln!("No se pudo iniciar el backend .NET: {e}");
                        }
                    }
                }
            }
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![])
        .build(tauri::generate_context!())
        .expect("error al construir la aplicación Tauri")
        .run(|app_handle, event| {
            if let RunEvent::Exit = event {
                let state = app_handle.state::<BackendProcess>();
                let child_opt = state.0.lock().unwrap().take();
                if let Some(mut child) = child_opt {
                    let _ = child.kill();
                    let _ = child.wait();
                }
            }
        });
}
