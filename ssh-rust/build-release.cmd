@echo off
setlocal
rem Locate vcvars64.bat via vswhere (VS 2017+ / Build Tools), falling back to
rem the known VS2015 location. Works both locally and on GitHub Actions
rem (windows-latest).
set "VCVARS="
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2^>nul`) do (
    if exist "%%i\VC\Auxiliary\Build\vcvars64.bat" set "VCVARS=%%i\VC\Auxiliary\Build\vcvars64.bat"
)
if not defined VCVARS (
    if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat" set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
)
if not defined VCVARS (
    if exist "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
)
if not defined VCVARS (
    echo Error: vcvars64.bat not found. Install VS2022 Build Tools with VC++ workload. 1>&2
    exit /b 1
)
call "%VCVARS%" >nul 2>&1
cargo build --release
set BUILD_EXIT=%ERRORLEVEL%
exit /b %BUILD_EXIT%
