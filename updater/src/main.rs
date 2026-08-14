//! remotex-updater — standalone self-updater for RemoteX.
//!
//! Invoked by the WPF app as a separate process:
//!     updater <downloadUrl> <sha256hex> <appExePath> [--restart]
//!
//! Flow:
//!   1. download the release zip to a temp file (progress -> stdout as JSON lines)
//!   2. verify the sha256 of the downloaded file
//!   3. extract to a temp staging dir
//!   4. wait for the target exe process to exit (poll, up to N seconds)
//!   5. back up the current exe, swap in the new one
//!   6. restart the app if --restart was passed
//!
//! stdout protocol (JSON lines) — consumed by the C# side:
//!   {"type":"stage","stage":"download"}          at each phase transition
//!   {"type":"progress","pct":42.3}               download progress 0..100
//!   {"type":"error","message":"..."}             fatal error, then exit(1)
//!   {"type":"done"}                              success

use std::env;
use std::fs;
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};
use std::thread;
use std::time::{Duration, Instant};

use sha2::{Digest, Sha256};
use thiserror::Error;

#[derive(Error, Debug)]
enum UpdError {
    #[error("missing/invalid arguments: {0}")]
    Args(String),
    #[error("download failed: {0}")]
    Download(String),
    #[error("sha256 mismatch: expected {expected}, got {got}")]
    ShaMismatch { expected: String, got: String },
    #[error("extract failed: {0}")]
    Extract(String),
    #[error("io error: {0}")]
    Io(#[from] std::io::Error),
    #[error("swap failed: {0}")]
    Swap(String),
}

type Result<T> = std::result::Result<T, UpdError>;

fn emit(obj: &str) {
    println!("{obj}");
    let _ = std::io::stdout().flush();
}

fn emit_stage(stage: &str) {
    emit(&format!("{{\"type\":\"stage\",\"stage\":\"{stage}\"}}"));
}

fn emit_progress(pct: f64) {
    emit(&format!("{{\"type\":\"progress\",\"pct\":{:.1}}}", pct));
}

fn emit_error(msg: &str) {
    emit(&format!(
        "{{\"type\":\"error\",\"message\":\"{}\"}}",
        msg.replace('\\', "\\\\").replace('"', "\\\"")
    ));
}

// ---------------------------------------------------------------------------
// download
// ---------------------------------------------------------------------------

fn download(url: &str, dest: &Path) -> Result<()> {
    emit_stage("download");
    let client = reqwest::blocking::Client::builder()
        .connect_timeout(Duration::from_secs(15))
        .build()
        .map_err(|e| UpdError::Download(e.to_string()))?;

    let mut resp = client
        .get(url)
        .send()
        .map_err(|e| UpdError::Download(format!("{e}")))?;

    if !resp.status().is_success() {
        return Err(UpdError::Download(format!(
            "server returned {}",
            resp.status()
        )));
    }

    let total = resp
        .content_length()
        .unwrap_or(0);

    let mut file = fs::File::create(dest)?;
    let mut buf = [0u8; 64 * 1024];
    let mut downloaded: u64 = 0;
    loop {
        let n = resp
            .read(&mut buf)
            .map_err(|e| UpdError::Download(e.to_string()))?;
        if n == 0 {
            break;
        }
        file.write_all(&buf[..n])?;
        downloaded += n as u64;
        if total > 0 {
            emit_progress(100.0 * downloaded as f64 / total as f64);
        }
    }
    file.flush()?;
    emit_stage("downloaded");
    Ok(())
}

fn sha256_of(path: &Path) -> Result<String> {
    let mut file = fs::File::open(path)?;
    let mut hasher = Sha256::new();
    let mut buf = [0u8; 64 * 1024];
    loop {
        let n = file.read(&mut buf)?;
        if n == 0 {
            break;
        }
        hasher.update(&buf[..n]);
    }
    Ok(format!("{:x}", hasher.finalize()))
}

// ---------------------------------------------------------------------------
// extraction
// ---------------------------------------------------------------------------

fn extract_zip(zip_path: &Path, dest_dir: &Path) -> Result<()> {
    emit_stage("extract");
    fs::create_dir_all(dest_dir)?;

    let file = fs::File::open(zip_path)?;
    let mut archive =
        zip::ZipArchive::new(file).map_err(|e| UpdError::Extract(e.to_string()))?;

    // The release zip contains a top-level folder (e.g. "RemoteX-1.0.2-net9-x64");
    // we want the RemoteX.exe inside it. Extract everything to the staging dir.
    for i in 0..archive.len() {
        let mut entry = archive
            .by_index(i)
            .map_err(|e| UpdError::Extract(e.to_string()))?;
        let entry_path = entry
            .enclosed_name()
            .ok_or_else(|| UpdError::Extract("unsafe zip entry name".into()))?
            .to_owned();
        let out_path = dest_dir.join(&entry_path);
        if entry.is_dir() {
            fs::create_dir_all(&out_path)?;
            continue;
        }
        if let Some(parent) = out_path.parent() {
            fs::create_dir_all(parent)?;
        }
        let mut out = fs::File::create(&out_path)?;
        std::io::copy(&mut entry, &mut out)?;
    }
    emit_stage("extracted");
    Ok(())
}

// ---------------------------------------------------------------------------
// process helpers
// ---------------------------------------------------------------------------

/// Returns true if any process is currently running with the given exe path.
fn process_running(exe_path: &Path) -> bool {
    let exe_name = exe_path
        .file_name()
        .map(|s| s.to_string_lossy().to_string())
        .unwrap_or_default();
    let out = match Command::new("tasklist")
        .args(["/FI", &format!("IMAGENAME eq {}", exe_name), "/NH"])
        .stdout(Stdio::piped())
        .output()
    {
        Ok(o) => o,
        Err(_) => return false,
    };
    let text = String::from_utf8_lossy(&out.stdout).to_string();
    // tasklist lists processes even if the path differs; we approximate by name.
    text.contains(&exe_name.to_lowercase()) || text.contains(&exe_name)
}

fn wait_for_exit(exe_path: &Path, timeout_secs: u64) {
    emit_stage("wait-exit");
    let deadline = Instant::now() + Duration::from_secs(timeout_secs);
    while Instant::now() < deadline {
        if !process_running(exe_path) {
            break;
        }
        thread::sleep(Duration::from_millis(300));
    }
}

fn restart(exe_path: &Path) {
    // detach: spawn with CREATE_NEW_PROCESS_GROUP and don't wait
    let _ = Command::new(exe_path).spawn();
}

// ---------------------------------------------------------------------------
// swap: backup + replace
// ---------------------------------------------------------------------------

fn find_new_exe(staging: &Path) -> Result<PathBuf> {
    // walk the staging dir for a top-level file named RemoteX.exe
    fn walk(dir: &Path, depth: usize) -> Option<PathBuf> {
        if depth > 3 {
            return None;
        }
        for e in fs::read_dir(dir).ok()? {
            let e = e.ok()?;
            let p = e.path();
            if p.is_file() && p.file_name().map(|n| n.to_string_lossy().eq_ignore_ascii_case("RemoteX.exe")).unwrap_or(false) {
                return Some(p);
            }
            if p.is_dir() {
                if let Some(found) = walk(&p, depth + 1) {
                    return Some(found);
                }
            }
        }
        None
    }
    walk(staging, 0).ok_or_else(|| UpdError::Swap("RemoteX.exe not found in release zip".into()))
}

fn swap_in(target_exe: &Path, new_exe: &Path) -> Result<()> {
    emit_stage("swap");
    let exe_dir = target_exe
        .parent()
        .ok_or_else(|| UpdError::Swap("exe has no parent dir".into()))?;

    // back up current exe so we can restore on next launch if the new one fails
    let backup = exe_dir.join("RemoteX.exe.bak");
    if target_exe.exists() {
        let _ = fs::remove_file(&backup);
        fs::copy(target_exe, &backup)?;
    }

    // copy the whole staging dir (dlls, rust libs, etc.) next to the exe
    let staging_root = new_exe
        .parent()
        .ok_or_else(|| UpdError::Swap("staging has no parent".into()))?;
    for e in fs::read_dir(staging_root)? {
        let e = e?;
        let src = e.path();
        let name = src.file_name().unwrap_or_default();
        if name.to_string_lossy().eq_ignore_ascii_case("RemoteX.exe") {
            continue; // handled below
        }
        let dst = exe_dir.join(name);
        if src.is_dir() {
            // replace any existing directory
            if dst.exists() {
                let _ = fs::remove_dir_all(&dst);
            }
            copy_dir(&src, &dst)?;
        } else if src.is_file() {
            let _ = fs::copy(&src, &dst);
        }
    }

    // finally replace the exe
    fs::copy(new_exe, target_exe)?;
    emit_stage("swapped");
    Ok(())
}

fn copy_dir(src: &Path, dst: &Path) -> Result<()> {
    fs::create_dir_all(dst)?;
    for e in fs::read_dir(src)? {
        let e = e?;
        let s = e.path();
        let d = dst.join(e.file_name());
        if s.is_dir() {
            copy_dir(&s, &d)?;
        } else if s.is_file() {
            let _ = fs::copy(&s, &d);
        }
    }
    Ok(())
}

// ---------------------------------------------------------------------------
// main
// ---------------------------------------------------------------------------

fn main() {
    let args: Vec<String> = env::args().skip(1).collect();
    if let Err(e) = run(&args) {
        emit_error(&e.to_string());
        std::process::exit(1);
    }
    emit("{\"type\":\"done\"}");
}

fn run(args: &[String]) -> Result<()> {
    // args: <downloadUrl> <sha256hex> <appExePath> [--restart]
    if args.len() < 3 {
        return Err(UpdError::Args(
            "usage: updater <downloadUrl> <sha256hex> <appExePath> [--restart]".into(),
        ));
    }
    let url = &args[0];
    let expected_sha = &args[1].to_lowercase();
    let exe_path = PathBuf::from(&args[2]);
    let want_restart = args.iter().any(|a| a == "--restart");

    let tmp = tempfile::Builder::new()
        .prefix("remotex-upd-")
        .tempdir()
        .map_err(|e| UpdError::Io(e))?;
    let zip_path = tmp.path().join("update.zip");
    let staging = tmp.path().join("stage");

    // 1. download
    download(url, &zip_path)?;

    // 2. verify
    emit_stage("verify");
    let actual_sha = sha256_of(&zip_path)?;
    if expected_sha != actual_sha {
        return Err(UpdError::ShaMismatch {
            expected: expected_sha.clone(),
            got: actual_sha,
        });
    }

    // 3. extract
    extract_zip(&zip_path, &staging)?;

    // 4. wait for the app to exit (it usually is still running while we work)
    wait_for_exit(&exe_path, 30);

    // 5. swap
    let new_exe = find_new_exe(&staging)?;
    swap_in(&exe_path, &new_exe)?;

    // 6. restart
    if want_restart {
        restart(&exe_path);
    }

    Ok(())
}
