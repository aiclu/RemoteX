@echo off
call "D:\Program Files\Microsoft Visual Studio\18\Enterprise\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1
cargo test
set TEST_EXIT=%ERRORLEVEL%
exit /b %TEST_EXIT%
