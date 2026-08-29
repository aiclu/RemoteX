//! remotex-updater — standalone self-updater for RemoteX.
//!
//! Invoked by the WPF app as a separate process:
//!     updater <downloadUrl> <appExePath> [--sha256 <hex>] [--restart]
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

fn log_line(message: &str) {
    let path = env::temp_dir().join("RemoteX-updater.log");
    if let Ok(mut file) = fs::OpenOptions::new().create(true).append(true).open(path) {
        let _ = writeln!(file, "{message}");
    }
}

fn emit(obj: &str) {
    println!("{obj}");
    let _ = std::io::stdout().flush();
}

fn emit_stage(stage: &str) {
    log_line(&format!("stage={stage}"));
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

/// Returns true if the process with the given PID is still running.
fn process_running_by_pid(pid: u32) -> bool {
    let out = match Command::new("tasklist")
        .args(["/FI", &format!("PID eq {pid}"), "/FO", "CSV", "/NH"])
        .stdout(Stdio::piped())
        .output()
    {
        Ok(o) => o,
        Err(e) => {
            log_line(&format!("tasklist failed for pid {pid}: {e}"));
            // Fail closed: do not replace files while the target may still be
            // running if process inspection itself failed.
            return true;
        }
    };
    let expected = pid.to_string();
    String::from_utf8_lossy(&out.stdout).lines().any(|line| {
        line.split(',')
            .nth(1)
            .map(|value| value.trim().trim_matches('"') == expected)
            .unwrap_or(false)
    })
}

/// Fallback for callers that do not provide a PID. New app builds always pass
/// the PID, but keeping this path preserves compatibility with older callers.
fn process_running_by_name(exe_path: &Path) -> bool {
    let exe_name = exe_path
        .file_name()
        .map(|s| s.to_string_lossy().to_string())
        .unwrap_or_default();
    let out = match Command::new("tasklist")
        .args(["/FI", &format!("IMAGENAME eq {exe_name}"), "/NH"])
        .stdout(Stdio::piped())
        .output()
    {
        Ok(o) => o,
        Err(e) => {
            log_line(&format!("tasklist failed for {exe_name}: {e}"));
            return true;
        }
    };
    String::from_utf8_lossy(&out.stdout)
        .to_ascii_lowercase()
        .contains(&exe_name.to_ascii_lowercase())
}

fn wait_for_exit(exe_path: &Path, pid: Option<u32>, timeout_secs: u64) -> Result<()> {
    emit_stage("wait-exit");
    let deadline = Instant::now() + Duration::from_secs(timeout_secs);
    let is_running = || pid.map(process_running_by_pid)
        .unwrap_or_else(|| process_running_by_name(exe_path));

    while Instant::now() < deadline {
        if !is_running() {
            return Ok(());
        }
        thread::sleep(Duration::from_millis(300));
    }

    if is_running() {
        let detail = pid
            .map(|value| format!("pid {value}"))
            .unwrap_or_else(|| exe_path.display().to_string());
        return Err(UpdError::Swap(format!(
            "target process ({detail}) did not exit within {timeout_secs}s"
        )));
    }
    Ok(())
}

fn restart(exe_path: &Path) -> Result<()> {
    // The updater is already detached from the app, so spawning without waiting
    // lets the replacement start after this process exits.
    Command::new(exe_path)
        .spawn()
        .map(|_| ())
        .map_err(|e| UpdError::Swap(format!("restart failed: {e}")))
}

fn copy_with_retry(source: &Path, destination: &Path) -> Result<()> {
    let mut last_error = None;
    for _ in 0..10 {
        match fs::copy(source, destination) {
            Ok(_) => return Ok(()),
            Err(e) => {
                last_error = Some(e);
                thread::sleep(Duration::from_millis(300));
            }
        }
    }
    Err(UpdError::Io(last_error.expect("copy retry must record an error")))
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
        copy_with_retry(target_exe, &backup)
            .map_err(|e| UpdError::Swap(format!("backup failed: {e}")))?;
    }

    // copy the whole staging dir (dlls, rust libs, etc.) next to the exe
    let staging_root = new_exe
        .parent()
        .ok_or_else(|| UpdError::Swap("staging has no parent".into()))?;
    for e in fs::read_dir(staging_root)? {
        let e = e?;
        let src = e.path();
        let name = e.file_name();
        if name.to_string_lossy().eq_ignore_ascii_case("RemoteX.exe")
            || name.to_string_lossy().eq_ignore_ascii_case("updater.exe")
        {
            // RemoteX.exe is replaced below. The running updater cannot replace
            // its own executable, so keep the existing helper for this update.
            continue;
        }
        let dst = exe_dir.join(&name);
        if src.is_dir() {
            // replace any existing directory
            if dst.exists() {
                fs::remove_dir_all(&dst)
                    .map_err(|e| UpdError::Swap(format!("remove {} failed: {e}", dst.display())))?;
            }
            copy_dir(&src, &dst)?;
        } else if src.is_file() {
            copy_with_retry(&src, &dst)
                .map_err(|e| UpdError::Swap(format!("copy {} failed: {e}", name.to_string_lossy())))?;
        }
    }

    // finally replace the exe
    copy_with_retry(new_exe, target_exe)
        .map_err(|e| UpdError::Swap(format!("replace {} failed: {e}", target_exe.display())))?;
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
            copy_with_retry(&s, &d)?;
        }
    }
    Ok(())
}

// ---------------------------------------------------------------------------
// main
// ---------------------------------------------------------------------------

fn main() {
    let args: Vec<String> = env::args().skip(1).collect();
    log_line("started");
    if let Err(e) = run(&args) {
        log_line(&format!("error={e}"));
        emit_error(&e.to_string());
        std::process::exit(1);
    }
    log_line("done");
    emit("{\"type\":\"done\"}");
}

fn run(args: &[String]) -> Result<()> {
    // Args (positional, matching SelfUpdateService.RunUpdaterAsync on the C#
    // side): <downloadUrl> <appExePath> [--sha256 <hex>] [--pid <pid>] [--restart]
    //
    // --sha256 is OPTIONAL: when provided, the downloaded zip is verified
    // against the given hex digest; when absent (current C# call path), the
    // check is skipped. The PID can also be supplied through
    // REMOTEX_TARGET_PID for compatibility with older updater binaries.
    if args.len() < 2 {
        return Err(UpdError::Args(
            "usage: updater <downloadUrl> <appExePath> [--sha256 <hex>] [--pid <pid>] [--restart]".into(),
        ));
    }
    let url = &args[0];
    let exe_path = PathBuf::from(&args[1]);
    let mut expected_sha: Option<String> = None;
    // New C# clients pass the PID through the environment for compatibility
    // with older updater binaries that reject unknown command-line options.
    let mut target_pid = env::var("REMOTEX_TARGET_PID")
        .ok()
        .and_then(|value| value.parse::<u32>().ok());
    let mut want_restart = false;
    let mut i = 2;
    while i < args.len() {
        match args[i].as_str() {
            "--sha256" => {
                if i + 1 < args.len() {
                    expected_sha = Some(args[i + 1].to_lowercase());
                    i += 2;
                } else {
                    return Err(UpdError::Args("--sha256 requires a value".into()));
                }
            }
            "--pid" => {
                if i + 1 < args.len() {
                    target_pid = Some(args[i + 1].parse().map_err(|_| {
                        UpdError::Args("--pid requires a numeric process id".into())
                    })?);
                    i += 2;
                } else {
                    return Err(UpdError::Args("--pid requires a value".into()));
                }
            }
            "--restart" => {
                want_restart = true;
                i += 1;
            }
            _ => {
                return Err(UpdError::Args(format!("unknown argument: {}", args[i])));
            }
        }
    }

    let tmp = tempfile::Builder::new()
        .prefix("remotex-upd-")
        .tempdir()
        .map_err(|e| UpdError::Io(e))?;
    let zip_path = tmp.path().join("update.zip");
    let staging = tmp.path().join("stage");

    // 1. download
    download(url, &zip_path)?;

    // 2. verify (optional)
    if let Some(expected) = &expected_sha {
        emit_stage("verify");
        let actual_sha = sha256_of(&zip_path)?;
        if *expected != actual_sha {
            return Err(UpdError::ShaMismatch {
                expected: expected.clone(),
                got: actual_sha,
            });
        }
    }

    // 3. extract
    extract_zip(&zip_path, &staging)?;

    // 4. wait for the app to exit (it usually is still running while we work)
    wait_for_exit(&exe_path, target_pid, 30)?;

    // 5. swap
    let new_exe = find_new_exe(&staging)?;
    swap_in(&exe_path, &new_exe)?;

    // 6. restart
    if want_restart {
        restart(&exe_path)?;
    }

    Ok(())
}
