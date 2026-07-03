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

/// Opens the Windows language-neutral custom dictionary in the system default editor
/// so the user can add/remove custom words by hand. Creates the file if it does not
/// exist yet (Windows only creates it on first "add to dictionary" from any app).
#[tauri::command]
fn open_dictionary(app: tauri::AppHandle) -> Result<(), String> {
    use tauri_plugin_opener::OpenerExt;

    let appdata = std::env::var("APPDATA").map_err(|_| "APPDATA not set".to_string())?;
    let path = std::path::PathBuf::from(appdata)
        .join("Microsoft")
        .join("Spelling")
        .join("neutral")
        .join("default.dic");

    if !path.exists() {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        }
        // UTF-16LE BOM so the editor and Windows treat the new file correctly.
        fs::write(&path, [0xFF, 0xFE]).map_err(|e| e.to_string())?;
    }

    app.opener()
        .open_path(path.to_string_lossy(), None::<&str>)
        .map_err(|e| e.to_string())
}

/// Centers the window on the monitor containing the mouse cursor. Falls back to
/// leaving the window where config placed it if the cursor's monitor can't be found.
fn position_on_cursor_monitor(win: &tauri::WebviewWindow) {
    let cursor = match win.cursor_position() {
        Ok(p) => p,
        Err(_) => return,
    };

    // Find the monitor whose bounds contain the cursor.
    let monitors = match win.available_monitors() {
        Ok(m) => m,
        Err(_) => return,
    };
    let target = monitors.into_iter().find(|m| {
        let pos = m.position();
        let size = m.size();
        let cx = cursor.x as i32;
        let cy = cursor.y as i32;
        cx >= pos.x
            && cx < pos.x + size.width as i32
            && cy >= pos.y
            && cy < pos.y + size.height as i32
    });

    let monitor = match target {
        Some(m) => m,
        None => return, // cursor not on a known monitor → leave as-is
    };

    if let Ok(win_size) = win.outer_size() {
        let mpos = monitor.position();
        let msize = monitor.size();
        let x = mpos.x + (msize.width as i32 - win_size.width as i32) / 2;
        let y = mpos.y + (msize.height as i32 - win_size.height as i32) / 2;
        let _ = win.set_position(tauri::PhysicalPosition::new(x, y));
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .invoke_handler(tauri::generate_handler![read_file, write_file, open_dictionary])
        .setup(|app| {
            // Open on whichever monitor the mouse cursor is currently on, centered.
            // Done before the window is shown (it starts hidden in config) so there is
            // no visible jump. No saved state — it just follows the cursor.
            if let Some(win) = app.get_webview_window("main") {
                position_on_cursor_monitor(&win);
                let _ = win.show();
            }

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
