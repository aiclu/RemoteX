//! remotex-updater — standalone self-updater for RemoteX.
//!
//! Normal invocation:
//!     updater <downloadUrl> <appExePath> [--sha256 <hex>] [--pid <pid>]
//!              [--target-version <version>] [--state-file <path>] [--restart]
//!
//! Apply-stage invocation is used to replace the running updater itself:
//!     updater --apply-stage <stageDir> <appExePath> [--parent-pid <pid>]
//!              [--target-pid <pid>] [--ready-file <path>]
//!              [--target-version <version>] [--state-file <path>] [--restart]
//!
//! The normal process downloads and validates the release before emitting
//! `ready-to-swap`. The WPF application closes only after that stage. A staged
//! updater proves its detached process is ready, waits for the target app and
//! old updater to exit, performs the reversible swap (including updater.exe),
//! and restarts RemoteX.

use std::env;
use std::fs;
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

#[cfg(windows)]
use std::os::windows::process::CommandExt;

use sha2::{Digest, Sha256};
use thiserror::Error;

const UPDATER_PROTOCOL_VERSION: &str = "2";
const HELPER_READY_TIMEOUT_SECS: u64 = 15;
const PROCESS_EXIT_TIMEOUT_SECS: u64 = 60;

#[cfg(windows)]
const CREATE_BREAKAWAY_FROM_JOB: u32 = 0x0100_0000;
#[cfg(windows)]
const DETACHED_PROCESS: u32 = 0x0000_0008;

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

#[derive(Clone, Debug)]
struct UpdateOptions {
    url: String,
    exe_path: PathBuf,
    expected_sha: Option<String>,
    target_pid: Option<u32>,
    target_version: Option<String>,
    state_file: Option<PathBuf>,
    want_restart: bool,
}

#[derive(Clone, Debug)]
struct ApplyOptions {
    stage: PathBuf,
    exe_path: PathBuf,
    parent_pid: Option<u32>,
    target_pid: Option<u32>,
    ready_file: Option<PathBuf>,
    target_version: Option<String>,
    state_file: Option<PathBuf>,
    want_restart: bool,
}

fn log_line(message: &str) {
    let path = env::temp_dir().join("RemoteX-updater.log");
    if let Ok(mut file) = fs::OpenOptions::new().create(true).append(true).open(path) {
        let timestamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|value| value.as_millis())
            .unwrap_or_default();
        let _ = writeln!(
            file,
            "{message} pid={} ts_ms={timestamp}",
            std::process::id()
        );
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
        json_escape(msg)
    ));
}

fn write_ready_marker(path: &Path) -> Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }

    let temp_path = PathBuf::from(format!("{}.tmp", path.to_string_lossy()));
    if temp_path.exists() {
        let _ = fs::remove_file(&temp_path);
    }

    let mut file = fs::OpenOptions::new()
        .create(true)
        .truncate(true)
        .write(true)
        .open(&temp_path)?;
    writeln!(file, "ready=1")?;
    writeln!(file, "pid={}", std::process::id())?;
    file.flush()?;
    file.sync_all()?;
    drop(file);

    if path.exists() {
        fs::remove_file(path)?;
    }
    fs::rename(temp_path, path)?;
    Ok(())
}

fn wait_for_ready_marker(path: &Path, child: &mut Child, timeout_secs: u64) -> Result<()> {
    log_line(&format!(
        "helper-wait-ready path={} timeout_secs={timeout_secs}",
        path.display()
    ));
    let deadline = Instant::now() + Duration::from_secs(timeout_secs);
    while Instant::now() < deadline {
        if let Ok(contents) = fs::read_to_string(path) {
            if contents.lines().any(|line| line.trim() == "ready=1") {
                log_line(&format!("helper-ready path={}", path.display()));
                return Ok(());
            }
        }

        if let Some(status) = child.try_wait()? {
            return Err(UpdError::Swap(format!(
                "staged updater exited before ready (status={status})"
            )));
        }
        thread::sleep(Duration::from_millis(100));
    }

    Err(UpdError::Swap(format!(
        "staged updater did not signal ready within {timeout_secs}s"
    )))
}

fn spawn_detached_helper(command: &mut Command) -> std::io::Result<Child> {
    command
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null());

    #[cfg(windows)]
    {
        command.creation_flags(DETACHED_PROCESS | CREATE_BREAKAWAY_FROM_JOB);
        match command.spawn() {
            Ok(child) => Ok(child),
            Err(first_error) => {
                log_line(&format!(
                    "helper-spawn-breakaway-failed error={first_error}"
                ));
                command.creation_flags(DETACHED_PROCESS);
                command.spawn().map_err(|second_error| {
                    std::io::Error::new(
                        second_error.kind(),
                        format!(
                            "breakaway spawn failed: {first_error}; fallback spawn failed: {second_error}"
                        ),
                    )
                })
            }
        }
    }

    #[cfg(not(windows))]
    {
        command.spawn()
    }
}

fn json_escape(value: &str) -> String {
    value
        .replace('\\', "\\\\")
        .replace('"', "\\\"")
        .replace('\r', "\\r")
        .replace('\n', "\\n")
        .replace('\t', "\\t")
}

fn write_state(
    state_file: Option<&Path>,
    target_version: Option<&str>,
    phase: &str,
    message: Option<&str>,
    backup_path: Option<&Path>,
    progress: f64,
) -> Result<()> {
    let Some(path) = state_file else {
        return Ok(());
    };

    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }

    let target = target_version.unwrap_or("");
    let msg = message.unwrap_or("");
    let backup = backup_path
        .map(|value| value.to_string_lossy().to_string())
        .unwrap_or_default();
    let contents = format!(
        "{{\"targetVersion\":\"{}\",\"phase\":\"{}\",\"message\":\"{}\",\"backupPath\":\"{}\",\"progress\":{:.1}}}",
        json_escape(target),
        json_escape(phase),
        json_escape(msg),
        json_escape(&backup),
        progress.clamp(0.0, 100.0)
    );

    let temp_path = PathBuf::from(format!("{}.tmp", path.to_string_lossy()));
    let mut file = fs::OpenOptions::new()
        .create(true)
        .truncate(true)
        .write(true)
        .open(&temp_path)?;
    file.write_all(contents.as_bytes())?;
    file.flush()?;
    file.sync_all()?;
    drop(file);

    // Windows does not replace an existing file with std::fs::rename. Remove
    // the old marker only after the new contents have been flushed; startup
    // also accepts the .tmp file if termination happens during this window.
    if path.exists() {
        let _ = fs::remove_file(path);
    }
    fs::rename(&temp_path, path)?;
    Ok(())
}

fn record_state(
    state_file: Option<&Path>,
    target_version: Option<&str>,
    phase: &str,
    message: Option<&str>,
    backup_path: Option<&Path>,
    progress: f64,
) {
    if let Err(error) = write_state(
        state_file,
        target_version,
        phase,
        message,
        backup_path,
        progress,
    ) {
        log_line(&format!("state write failed for phase={phase}: {error}"));
    }
}

fn record_stage(stage: &str, state_file: Option<&Path>, target_version: Option<&str>) {
    emit_stage(stage);
    record_state(state_file, target_version, stage, None, None, 0.0);
}

fn state_has_phase(state_file: Option<&Path>, phase: &str) -> bool {
    let Some(path) = state_file else {
        return false;
    };
    let marker = format!("\"phase\":\"{phase}\"");
    fs::read_to_string(path)
        .map(|contents| contents.contains(&marker))
        .unwrap_or(false)
}

fn record_failure(state_file: Option<&Path>, target_version: Option<&str>, error: &UpdError) {
    // Keep the terminal transaction state and its backup reference intact. A
    // later launch uses `swapped` to confirm success, and uses the rollback
    // states to present recovery diagnostics instead of losing that context.
    if state_has_phase(state_file, "swapped")
        || state_has_phase(state_file, "rolled-back")
        || state_has_phase(state_file, "rollback-failed")
    {
        log_line(&format!("terminal state preserved after error: {error}"));
        return;
    }

    record_state(
        state_file,
        target_version,
        "failed",
        Some(&error.to_string()),
        None,
        0.0,
    );
}

// ---------------------------------------------------------------------------
// download
// ---------------------------------------------------------------------------

fn download(
    url: &str,
    dest: &Path,
    state_file: Option<&Path>,
    target_version: Option<&str>,
) -> Result<()> {
    record_stage("download", state_file, target_version);
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

    let total = resp.content_length().unwrap_or(0);
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
    record_stage("downloaded", state_file, target_version);
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

fn extract_zip(
    zip_path: &Path,
    dest_dir: &Path,
    state_file: Option<&Path>,
    target_version: Option<&str>,
) -> Result<()> {
    record_stage("extract", state_file, target_version);
    fs::create_dir_all(dest_dir)?;

    let file = fs::File::open(zip_path)?;
    let mut archive = zip::ZipArchive::new(file).map_err(|e| UpdError::Extract(e.to_string()))?;

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
    record_stage("extracted", state_file, target_version);
    Ok(())
}

// ---------------------------------------------------------------------------
// process and permission helpers
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

fn wait_for_exit(
    exe_path: &Path,
    pid: Option<u32>,
    timeout_secs: u64,
    state_file: Option<&Path>,
    target_version: Option<&str>,
) -> Result<()> {
    wait_for_processes_exit(
        exe_path,
        &[pid],
        timeout_secs,
        state_file,
        target_version,
        true,
    )
}

fn wait_for_processes_exit(
    exe_path: &Path,
    pids: &[Option<u32>],
    timeout_secs: u64,
    state_file: Option<&Path>,
    target_version: Option<&str>,
    publish_stage: bool,
) -> Result<()> {
    if publish_stage {
        record_stage("wait-exit", state_file, target_version);
    }
    let descriptions: Vec<String> = pids
        .iter()
        .map(|pid| {
            pid.map(|value| format!("pid {value}"))
                .unwrap_or_else(|| exe_path.display().to_string())
        })
        .collect();
    log_line(&format!(
        "wait-start processes={} timeout_secs={timeout_secs}",
        descriptions.join(",")
    ));

    let deadline = Instant::now() + Duration::from_secs(timeout_secs);
    let running_processes = || {
        pids.iter()
            .filter_map(|pid| {
                let running = pid
                    .map(process_running_by_pid)
                    .unwrap_or_else(|| process_running_by_name(exe_path));
                if running {
                    Some(
                        pid.map(|value| format!("pid {value}"))
                            .unwrap_or_else(|| exe_path.display().to_string()),
                    )
                } else {
                    None
                }
            })
            .collect::<Vec<_>>()
    };

    while Instant::now() < deadline {
        if running_processes().is_empty() {
            log_line("wait-complete");
            return Ok(());
        }
        thread::sleep(Duration::from_millis(300));
    }

    let running = running_processes();
    if !running.is_empty() {
        return Err(UpdError::Swap(format!(
            "target process(es) ({}) did not exit within {timeout_secs}s",
            running.join(", ")
        )));
    }
    Ok(())
}

fn verify_target_writable(exe_path: &Path) -> Result<()> {
    let exe_dir = exe_path
        .parent()
        .ok_or_else(|| UpdError::Swap("exe has no parent dir".into()))?;
    let probe = exe_dir.join(format!(".remotex-update-probe-{}.tmp", std::process::id()));
    let result = (|| {
        let mut file = fs::OpenOptions::new()
            .create_new(true)
            .write(true)
            .open(&probe)
            .map_err(|e| UpdError::Swap(format!("target directory is not writable: {e}")))?;
        file.write_all(b"RemoteX update preflight")?;
        file.flush()?;
        drop(file);
        fs::remove_file(&probe)?;
        Ok(())
    })();
    if result.is_err() {
        let _ = fs::remove_file(&probe);
    }
    result
}

fn restart(exe_path: &Path) -> Result<()> {
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
    Err(UpdError::Io(
        last_error.expect("copy retry must record an error"),
    ))
}

// ---------------------------------------------------------------------------
// staging and swap
// ---------------------------------------------------------------------------

fn find_named_file(staging: &Path, wanted_name: &str) -> Option<PathBuf> {
    fn walk(dir: &Path, wanted_name: &str, depth: usize) -> Option<PathBuf> {
        if depth > 3 {
            return None;
        }
        for entry in fs::read_dir(dir).ok()? {
            let entry = entry.ok()?;
            let path = entry.path();
            if path.is_file()
                && path
                    .file_name()
                    .map(|name| name.to_string_lossy().eq_ignore_ascii_case(wanted_name))
                    .unwrap_or(false)
            {
                return Some(path);
            }
            if path.is_dir() {
                if let Some(found) = walk(&path, wanted_name, depth + 1) {
                    return Some(found);
                }
            }
        }
        None
    }
    walk(staging, wanted_name, 0)
}

fn find_new_exe(staging: &Path) -> Result<PathBuf> {
    find_named_file(staging, "RemoteX.exe")
        .ok_or_else(|| UpdError::Swap("RemoteX.exe not found in release zip".into()))
}

fn remove_path(path: &Path) -> Result<()> {
    if path.is_dir() {
        fs::remove_dir_all(path)?;
    } else if path.exists() {
        fs::remove_file(path)?;
    }
    Ok(())
}

fn copy_dir(src: &Path, dst: &Path) -> Result<()> {
    fs::create_dir_all(dst)?;
    for entry in fs::read_dir(src)? {
        let entry = entry?;
        let source = entry.path();
        let destination = dst.join(entry.file_name());
        if source.is_dir() {
            copy_dir(&source, &destination)?;
        } else if source.is_file() {
            copy_with_retry(&source, &destination)?;
        }
    }
    Ok(())
}

fn backup_entries(exe_dir: &Path, backup_root: &Path, names: &[std::ffi::OsString]) -> Result<()> {
    for name in names {
        let source = exe_dir.join(name);
        if !source.exists() {
            continue;
        }
        let destination = backup_root.join(name);
        if source.is_dir() {
            copy_dir(&source, &destination)?;
        } else {
            copy_with_retry(&source, &destination)?;
        }
    }
    Ok(())
}

fn restore_entries(exe_dir: &Path, backup_root: &Path, names: &[std::ffi::OsString]) -> Result<()> {
    for name in names {
        let destination = exe_dir.join(name);
        if destination.exists() {
            remove_path(&destination)?;
        }

        let backup = backup_root.join(name);
        if backup.is_dir() {
            copy_dir(&backup, &destination)?;
        } else if backup.is_file() {
            copy_with_retry(&backup, &destination)?;
        }
    }
    Ok(())
}

fn swap_in(
    target_exe: &Path,
    new_exe: &Path,
    replace_updater: bool,
    state_file: Option<&Path>,
    target_version: Option<&str>,
) -> Result<()> {
    record_stage("swap", state_file, target_version);
    let exe_dir = target_exe
        .parent()
        .ok_or_else(|| UpdError::Swap("exe has no parent dir".into()))?;
    let staging_root = new_exe
        .parent()
        .ok_or_else(|| UpdError::Swap("staging has no parent".into()))?;
    let backup_root = exe_dir.join(format!(".remotex-update-backup-{}", std::process::id()));

    if backup_root.exists() {
        remove_path(&backup_root)?;
    }
    fs::create_dir_all(&backup_root)?;
    log_line(&format!("backup-start path={}", backup_root.display()));

    let entries: Vec<fs::DirEntry> =
        fs::read_dir(staging_root)?.collect::<std::result::Result<_, _>>()?;
    let names: Vec<std::ffi::OsString> = entries.iter().map(|entry| entry.file_name()).collect();
    if let Err(error) = backup_entries(exe_dir, &backup_root, &names) {
        log_line(&format!("backup-failed error={error}"));
        let _ = remove_path(&backup_root);
        return Err(UpdError::Swap(format!("backup failed: {error}")));
    }
    log_line(&format!("backup-complete path={}", backup_root.display()));

    let apply_result = (|| -> Result<()> {
        log_line("swap-start");
        let main_name = new_exe
            .file_name()
            .ok_or_else(|| UpdError::Swap("new executable has no file name".into()))?;

        for entry in &entries {
            let source = entry.path();
            let name = entry.file_name();
            if name == main_name {
                continue;
            }
            if !replace_updater && name.to_string_lossy().eq_ignore_ascii_case("updater.exe") {
                continue;
            }

            let destination = exe_dir.join(&name);
            if destination.exists() {
                remove_path(&destination).map_err(|e| {
                    UpdError::Swap(format!("remove {} failed: {e}", destination.display()))
                })?;
            }
            if source.is_dir() {
                copy_dir(&source, &destination)?;
            } else if source.is_file() {
                copy_with_retry(&source, &destination).map_err(|e| {
                    UpdError::Swap(format!("copy {} failed: {e}", name.to_string_lossy()))
                })?;
            }
        }

        copy_with_retry(new_exe, target_exe)
            .map_err(|e| UpdError::Swap(format!("replace {} failed: {e}", target_exe.display())))?;
        Ok(())
    })();

    match apply_result {
        Ok(()) => {
            log_line("swap-complete");
            record_state(
                state_file,
                target_version,
                "swapped",
                None,
                Some(&backup_root),
                100.0,
            );
            Ok(())
        }
        Err(error) => {
            log_line(&format!("rollback-start cause={error}"));
            let rollback_result = restore_entries(exe_dir, &backup_root, &names);
            match rollback_result {
                Ok(()) => {
                    log_line("rollback-complete");
                    record_state(
                        state_file,
                        target_version,
                        "rolled-back",
                        Some(&error.to_string()),
                        None,
                        0.0,
                    );
                    let _ = remove_path(&backup_root);
                    Err(error)
                }
                Err(rollback_error) => {
                    let message = format!("{error}; rollback failed: {rollback_error}");
                    log_line(&format!("rollback-failed error={message}"));
                    record_state(
                        state_file,
                        target_version,
                        "rollback-failed",
                        Some(&message),
                        Some(&backup_root),
                        0.0,
                    );
                    Err(UpdError::Swap(message))
                }
            }
        }
    }
}

// ---------------------------------------------------------------------------
// argument parsing and execution
// ---------------------------------------------------------------------------

fn parse_normal_options(args: &[String]) -> Result<UpdateOptions> {
    if args.len() < 2 {
        return Err(UpdError::Args(
            "usage: updater <downloadUrl> <appExePath> [--sha256 <hex>] [--pid <pid>] [--target-version <version>] [--state-file <path>] [--restart]".into(),
        ));
    }

    let mut options = UpdateOptions {
        url: args[0].clone(),
        exe_path: PathBuf::from(&args[1]),
        expected_sha: None,
        target_pid: env::var("REMOTEX_TARGET_PID")
            .ok()
            .and_then(|value| value.parse::<u32>().ok()),
        target_version: env::var("REMOTEX_TARGET_VERSION").ok(),
        state_file: env::var_os("REMOTEX_STATE_FILE").map(PathBuf::from),
        want_restart: false,
    };

    let mut i = 2;
    while i < args.len() {
        match args[i].as_str() {
            "--sha256" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--sha256 requires a value".into()));
                }
                options.expected_sha = Some(args[i + 1].to_lowercase());
                i += 2;
            }
            "--pid" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--pid requires a value".into()));
                }
                options.target_pid =
                    Some(args[i + 1].parse().map_err(|_| {
                        UpdError::Args("--pid requires a numeric process id".into())
                    })?);
                i += 2;
            }
            "--target-version" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--target-version requires a value".into()));
                }
                options.target_version = Some(args[i + 1].clone());
                i += 2;
            }
            "--state-file" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--state-file requires a value".into()));
                }
                options.state_file = Some(PathBuf::from(&args[i + 1]));
                i += 2;
            }
            "--restart" => {
                options.want_restart = true;
                i += 1;
            }
            _ => return Err(UpdError::Args(format!("unknown argument: {}", args[i]))),
        }
    }
    Ok(options)
}

fn parse_apply_options(args: &[String]) -> Result<ApplyOptions> {
    if args.len() < 3 {
        return Err(UpdError::Args(
            "usage: updater --apply-stage <stageDir> <appExePath> [--parent-pid <pid>] [--target-pid <pid>] [--ready-file <path>] [--target-version <version>] [--state-file <path>] [--restart]".into(),
        ));
    }

    let mut options = ApplyOptions {
        stage: PathBuf::from(&args[1]),
        exe_path: PathBuf::from(&args[2]),
        parent_pid: None,
        target_pid: None,
        ready_file: None,
        target_version: None,
        state_file: None,
        want_restart: false,
    };

    let mut i = 3;
    while i < args.len() {
        match args[i].as_str() {
            "--parent-pid" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--parent-pid requires a value".into()));
                }
                options.parent_pid = Some(args[i + 1].parse().map_err(|_| {
                    UpdError::Args("--parent-pid requires a numeric process id".into())
                })?);
                i += 2;
            }
            "--target-pid" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--target-pid requires a value".into()));
                }
                options.target_pid = Some(args[i + 1].parse().map_err(|_| {
                    UpdError::Args("--target-pid requires a numeric process id".into())
                })?);
                i += 2;
            }
            "--ready-file" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--ready-file requires a value".into()));
                }
                options.ready_file = Some(PathBuf::from(&args[i + 1]));
                i += 2;
            }
            "--target-version" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--target-version requires a value".into()));
                }
                options.target_version = Some(args[i + 1].clone());
                i += 2;
            }
            "--state-file" => {
                if i + 1 >= args.len() {
                    return Err(UpdError::Args("--state-file requires a value".into()));
                }
                options.state_file = Some(PathBuf::from(&args[i + 1]));
                i += 2;
            }
            "--restart" => {
                options.want_restart = true;
                i += 1;
            }
            _ => return Err(UpdError::Args(format!("unknown argument: {}", args[i]))),
        }
    }
    Ok(options)
}

fn run_normal(options: &UpdateOptions) -> Result<()> {
    record_state(
        options.state_file.as_deref(),
        options.target_version.as_deref(),
        "starting",
        None,
        None,
        0.0,
    );

    let tmp = tempfile::Builder::new()
        .prefix("remotex-upd-")
        .tempdir()
        .map_err(UpdError::Io)?;
    let zip_path = tmp.path().join("update.zip");
    let staging = tmp.path().join("stage");

    download(
        &options.url,
        &zip_path,
        options.state_file.as_deref(),
        options.target_version.as_deref(),
    )?;

    if let Some(expected) = &options.expected_sha {
        record_stage(
            "verify",
            options.state_file.as_deref(),
            options.target_version.as_deref(),
        );
        let actual_sha = sha256_of(&zip_path)?;
        if *expected != actual_sha {
            return Err(UpdError::ShaMismatch {
                expected: expected.clone(),
                got: actual_sha,
            });
        }
    }

    extract_zip(
        &zip_path,
        &staging,
        options.state_file.as_deref(),
        options.target_version.as_deref(),
    )?;
    let new_exe = find_new_exe(&staging)?;
    verify_target_writable(&options.exe_path)?;

    // The current updater is locked by Windows. Run the freshly downloaded
    // helper from staging before telling the application to close. The helper
    // proves that it started successfully, then waits for both the application
    // and this updater before replacing updater.exe.
    if let Some(staged_updater) = find_named_file(&staging, "updater.exe") {
        let ready_file = tmp.path().join("helper-ready");
        if ready_file.exists() {
            let _ = fs::remove_file(&ready_file);
        }
        record_stage(
            "helper-handoff",
            options.state_file.as_deref(),
            options.target_version.as_deref(),
        );
        log_line(&format!(
            "helper-spawn-start path={} target_pid={:?}",
            staged_updater.display(),
            options.target_pid
        ));
        let mut command = Command::new(&staged_updater);
        command
            .arg("--apply-stage")
            .arg(&staging)
            .arg(&options.exe_path)
            .arg("--parent-pid")
            .arg(std::process::id().to_string())
            .arg("--ready-file")
            .arg(&ready_file);
        if let Some(target_pid) = options.target_pid {
            command.arg("--target-pid").arg(target_pid.to_string());
        }
        if options.want_restart {
            command.arg("--restart");
        }
        if let Some(version) = &options.target_version {
            command.arg("--target-version").arg(version);
        }
        if let Some(state_file) = &options.state_file {
            command.arg("--state-file").arg(state_file);
        }

        let mut child = spawn_detached_helper(&mut command)
            .map_err(|error| UpdError::Swap(format!("start staged updater failed: {error}")))?;
        log_line(&format!(
            "helper-spawned child_pid={} child_path={}",
            child.id(),
            staged_updater.display()
        ));
        if let Err(error) =
            wait_for_ready_marker(&ready_file, &mut child, HELPER_READY_TIMEOUT_SECS)
        {
            log_line(&format!("helper-ready-failed error={error}"));
            let _ = child.kill();
            let _ = child.wait();
            return Err(error);
        }

        // Transfer ownership of the temp directory to the staged helper. It
        // remains until the helper has copied the release. Persist this before
        // emitting ready-to-swap so the application's shutdown cannot race
        // with TempDir cleanup.
        let path = tmp.keep();
        log_line(&format!("handoff-root path={}", path.display()));

        record_stage(
            "ready-to-swap",
            options.state_file.as_deref(),
            options.target_version.as_deref(),
        );
        return Ok(());
    }

    // Compatibility fallback for packages that do not contain updater.exe.
    record_stage(
        "ready-to-swap",
        options.state_file.as_deref(),
        options.target_version.as_deref(),
    );
    wait_for_exit(
        &options.exe_path,
        options.target_pid,
        PROCESS_EXIT_TIMEOUT_SECS,
        options.state_file.as_deref(),
        options.target_version.as_deref(),
    )?;
    let swap_result = swap_in(
        &options.exe_path,
        &new_exe,
        false,
        options.state_file.as_deref(),
        options.target_version.as_deref(),
    );
    if swap_result.is_err() {
        let _ = restart(&options.exe_path);
    }
    swap_result?;
    if options.want_restart {
        restart(&options.exe_path)?;
    }
    Ok(())
}

fn run_apply(options: &ApplyOptions) -> Result<()> {
    // Validate the staged package and the target directory before waiting for
    // the parent.  The WPF bootstrap path uses this signal as the handoff
    // point, so it must be emitted before the application is asked to close.
    let new_exe = find_new_exe(&options.stage)?;
    verify_target_writable(&options.exe_path)?;
    let has_ready_file = options.ready_file.is_some();
    if has_ready_file {
        let ready_file = options.ready_file.as_deref().expect("checked above");
        write_ready_marker(ready_file)?;
        log_line(&format!(
            "helper-ready-marker path={}",
            ready_file.display()
        ));
    } else {
        record_stage(
            "ready-to-swap",
            options.state_file.as_deref(),
            options.target_version.as_deref(),
        );
    }

    if options.target_pid.is_some() || options.parent_pid.is_some() {
        let mut wait_targets = Vec::new();
        if let Some(target_pid) = options.target_pid {
            wait_targets.push(Some(target_pid));
        } else if options.ready_file.is_some() {
            // The normal handoff still supports callers that do not provide
            // --target-pid by falling back to the executable name. Legacy
            // bootstrap callers pass their application PID as parent_pid and
            // should not add a second name-based wait.
            wait_targets.push(None);
        }
        if let Some(parent_pid) = options.parent_pid {
            wait_targets.push(Some(parent_pid));
        }
        let wait_result = wait_for_processes_exit(
            &options.exe_path,
            &wait_targets,
            PROCESS_EXIT_TIMEOUT_SECS,
            options.state_file.as_deref(),
            options.target_version.as_deref(),
            !has_ready_file,
        );
        if let Err(error) = wait_result {
            log_line(&format!("wait-failed error={error}"));
            if has_ready_file {
                match restart(&options.exe_path) {
                    Ok(()) => log_line("old-version-restarted-after-wait-failure"),
                    Err(restart_error) => {
                        log_line(&format!("old-version-restart-failed error={restart_error}"));
                    }
                }
            }
            return Err(error);
        }
    }

    let swap_result = swap_in(
        &options.exe_path,
        &new_exe,
        true,
        options.state_file.as_deref(),
        options.target_version.as_deref(),
    );
    if swap_result.is_err() {
        match restart(&options.exe_path) {
            Ok(()) => log_line("old-version-restarted-after-swap-failure"),
            Err(error) => log_line(&format!("old-version-restart-failed error={error}")),
        }
    }
    swap_result?;
    if options.want_restart {
        restart(&options.exe_path)?;
    }
    Ok(())
}

fn run(args: &[String]) -> Result<()> {
    if args.first().map(String::as_str) == Some("--apply-stage") {
        let options = parse_apply_options(args)?;
        let result = run_apply(&options);
        if let Err(error) = &result {
            record_failure(
                options.state_file.as_deref(),
                options.target_version.as_deref(),
                error,
            );
        }
        return result;
    }

    let options = parse_normal_options(args)?;
    let result = run_normal(&options);
    if let Err(error) = &result {
        record_failure(
            options.state_file.as_deref(),
            options.target_version.as_deref(),
            error,
        );
    }
    result
}

fn main() {
    let args: Vec<String> = env::args().skip(1).collect();
    log_line("started");
    if args.len() == 1 && args[0] == "--version" {
        emit(&format!(
            "{{\"type\":\"version\",\"protocol\":\"{}\"}}",
            UPDATER_PROTOCOL_VERSION
        ));
        log_line("version-probe");
        return;
    }
    if let Err(error) = run(&args) {
        log_line(&format!("error={error}"));
        emit_error(&error.to_string());
        std::process::exit(1);
    }
    log_line("done");
    emit("{\"type\":\"done\"}");
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use std::io::Cursor;
    use tempfile::tempdir;
    use zip::write::SimpleFileOptions;
    use zip::ZipWriter;

    #[test]
    fn json_escape_handles_quotes_and_control_characters() {
        assert_eq!(json_escape("a\\b\"c\nd"), "a\\\\b\\\"c\\nd");
    }

    #[test]
    fn parse_apply_options_accepts_handoff_arguments() {
        let args = vec![
            "--apply-stage".into(),
            "stage".into(),
            "RemoteX.exe".into(),
            "--parent-pid".into(),
            "101".into(),
            "--target-pid".into(),
            "202".into(),
            "--ready-file".into(),
            "ready.marker".into(),
            "--target-version".into(),
            "1.0.15".into(),
        ];

        let options = parse_apply_options(&args).unwrap();
        assert_eq!(options.parent_pid, Some(101));
        assert_eq!(options.target_pid, Some(202));
        assert_eq!(options.ready_file, Some(PathBuf::from("ready.marker")));
        assert_eq!(options.target_version.as_deref(), Some("1.0.15"));
    }

    #[test]
    fn ready_marker_is_written_atomically() {
        let temp = tempdir().unwrap();
        let marker = temp.path().join("handoff").join("ready.marker");

        write_ready_marker(&marker).unwrap();

        let contents = fs::read_to_string(&marker).unwrap();
        assert!(contents.lines().any(|line| line == "ready=1"));
        assert!(contents.lines().any(|line| line.starts_with("pid=")));
        assert!(!marker.with_extension("marker.tmp").exists());
    }

    #[test]
    fn ready_marker_is_observed_before_child_exit() {
        let temp = tempdir().unwrap();
        let marker = temp.path().join("ready.marker");
        write_ready_marker(&marker).unwrap();

        let mut child = if cfg!(windows) {
            Command::new("cmd")
                .args(["/C", "exit", "0"])
                .spawn()
                .unwrap()
        } else {
            Command::new("sh").args(["-c", "exit 0"]).spawn().unwrap()
        };

        wait_for_ready_marker(&marker, &mut child, 1).unwrap();
        let _ = child.wait();
    }

    #[test]
    fn find_new_exe_supports_release_folder() {
        let temp = tempdir().unwrap();
        let release = temp.path().join("RemoteX-1.0.15-net9-x64");
        fs::create_dir_all(&release).unwrap();
        let exe = release.join("RemoteX.exe");
        fs::write(&exe, b"new").unwrap();

        assert_eq!(find_new_exe(temp.path()).unwrap(), exe);
    }

    #[test]
    fn extract_zip_rejects_parent_traversal() {
        let temp = tempdir().unwrap();
        let zip_path = temp.path().join("bad.zip");
        let bytes = {
            let mut writer = ZipWriter::new(Cursor::new(Vec::new()));
            writer
                .start_file("../outside.txt", SimpleFileOptions::default())
                .unwrap();
            std::io::Write::write_all(&mut writer, b"bad").unwrap();
            writer.finish().unwrap().into_inner()
        };
        fs::write(&zip_path, bytes).unwrap();

        let result = extract_zip(&zip_path, &temp.path().join("stage"), None, None);
        assert!(matches!(result, Err(UpdError::Extract(_))));
        assert!(!temp.path().join("outside.txt").exists());
    }

    #[test]
    fn swap_replaces_main_exe_and_updater() {
        let temp = tempdir().unwrap();
        let target_dir = temp.path().join("installed");
        let stage_dir = temp.path().join("stage").join("RemoteX-1.0.15");
        fs::create_dir_all(&target_dir).unwrap();
        fs::create_dir_all(&stage_dir).unwrap();

        let target_exe = target_dir.join("RemoteX.exe");
        fs::write(&target_exe, b"old app").unwrap();
        fs::write(target_dir.join("updater.exe"), b"old updater").unwrap();
        fs::write(target_dir.join("shared.dll"), b"old library").unwrap();
        fs::write(stage_dir.join("RemoteX.exe"), b"new app").unwrap();
        fs::write(stage_dir.join("updater.exe"), b"new updater").unwrap();
        fs::write(stage_dir.join("shared.dll"), b"new library").unwrap();

        swap_in(
            &target_exe,
            &stage_dir.join("RemoteX.exe"),
            true,
            None,
            Some("1.0.15"),
        )
        .unwrap();

        assert_eq!(fs::read(&target_exe).unwrap(), b"new app");
        assert_eq!(
            fs::read(target_dir.join("updater.exe")).unwrap(),
            b"new updater"
        );
        assert_eq!(
            fs::read(target_dir.join("shared.dll")).unwrap(),
            b"new library"
        );
    }
}
