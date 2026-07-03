use std::fs;
use std::io::Write;
use std::path::PathBuf;

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

/// Append a word to the Windows language-neutral custom dictionary so it is no longer
/// flagged as misspelled — in this app and every other Windows app, across languages.
/// The file is UTF-16LE, one word per line (Windows' format). The web layer cannot
/// write here (sandbox), so it delegates to this native command.
#[tauri::command]
fn add_to_dictionary(word: String) -> Result<(), String> {
    let word = word.trim();
    if word.is_empty() {
        return Err("empty word".into());
    }

    let dir: PathBuf = dirs_neutral_dictionary_dir()?;
    fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    let path = dir.join("default.dic");

    // Read existing words (UTF-16LE) to avoid duplicates.
    let existing = read_utf16_lines(&path).unwrap_or_default();
    if existing.iter().any(|w| w.eq_ignore_ascii_case(word)) {
        return Ok(()); // already present
    }

    // Append the word as UTF-16LE followed by CRLF, matching Windows' format.
    let mut file = fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(&path)
        .map_err(|e| e.to_string())?;

    let mut bytes = Vec::new();
    for unit in word.encode_utf16() {
        bytes.extend_from_slice(&unit.to_le_bytes());
    }
    for unit in "\r\n".encode_utf16() {
        bytes.extend_from_slice(&unit.to_le_bytes());
    }
    file.write_all(&bytes).map_err(|e| e.to_string())?;
    Ok(())
}

fn dirs_neutral_dictionary_dir() -> Result<PathBuf, String> {
    let appdata = std::env::var("APPDATA").map_err(|_| "APPDATA not set".to_string())?;
    Ok(PathBuf::from(appdata)
        .join("Microsoft")
        .join("Spelling")
        .join("neutral"))
}

/// Reads a UTF-16LE file into lines, tolerating a leading BOM. Best-effort.
fn read_utf16_lines(path: &PathBuf) -> Option<Vec<String>> {
    let raw = fs::read(path).ok()?;
    let mut units: Vec<u16> = raw
        .chunks_exact(2)
        .map(|c| u16::from_le_bytes([c[0], c[1]]))
        .collect();
    if units.first() == Some(&0xFEFF) {
        units.remove(0); // strip BOM
    }
    let text = String::from_utf16_lossy(&units);
    Some(
        text.lines()
            .map(|l| l.trim().to_string())
            .filter(|l| !l.is_empty())
            .collect(),
    )
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .invoke_handler(tauri::generate_handler![
            read_file,
            write_file,
            add_to_dictionary
        ])
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
