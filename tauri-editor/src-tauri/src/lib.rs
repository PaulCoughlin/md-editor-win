use std::fs;

use tauri::menu::{MenuBuilder, MenuItemBuilder, SubmenuBuilder};
use tauri::{Emitter, Manager};

/// Read a UTF-8 text file. Returns the contents or an error string.
#[tauri::command]
fn read_file(path: String) -> Result<String, String> {
    fs::read_to_string(&path).map_err(|e| e.to_string())
}

/// Write a UTF-8 text file (used on save).
#[tauri::command]
fn write_file(path: String, contents: String) -> Result<(), String> {
    fs::write(&path, contents).map_err(|e| e.to_string())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        // Remembers window size/position across launches.
        .plugin(tauri_plugin_window_state::Builder::default().build())
        .invoke_handler(tauri::generate_handler![read_file, write_file])
        .setup(|app| {
            // Native File menu. Each item emits an event the frontend handles, so the
            // menu drives the same code paths as toolbar/keyboard actions.
            let file_menu = SubmenuBuilder::new(app, "File")
                .item(&MenuItemBuilder::with_id("new", "New").accelerator("CmdOrCtrl+N").build(app)?)
                .item(&MenuItemBuilder::with_id("open", "Open…").accelerator("CmdOrCtrl+O").build(app)?)
                .separator()
                .item(&MenuItemBuilder::with_id("save", "Save").accelerator("CmdOrCtrl+S").build(app)?)
                .item(&MenuItemBuilder::with_id("save_as", "Save As…").accelerator("CmdOrCtrl+Shift+S").build(app)?)
                .separator()
                .item(&MenuItemBuilder::with_id("print", "Print…").accelerator("CmdOrCtrl+P").build(app)?)
                .separator()
                .item(&MenuItemBuilder::with_id("preferences", "Preferences…").build(app)?)
                .separator()
                .item(&MenuItemBuilder::with_id("exit", "Exit").build(app)?)
                .build()?;

            let menu = MenuBuilder::new(app).item(&file_menu).build()?;
            app.set_menu(menu)?;

            app.on_menu_event(move |app, event| {
                let id = event.id().0.as_str();
                if id == "exit" {
                    if let Some(w) = app.get_webview_window("main") {
                        let _ = w.close();
                    }
                } else {
                    // Forward to the frontend, which owns document logic.
                    let _ = app.emit("menu", id.to_string());
                }
            });

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
