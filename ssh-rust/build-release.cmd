@echo off
call "D:\Program Files\Microsoft Visual Studio\18\Enterprise\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1
cargo build --release
set BUILD_EXIT=%ERRORLEVEL%
exit /b %BUILD_EXIT%
