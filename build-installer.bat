@echo off
REM ---------------------------------------------------------------------------
REM VybeDesk Installer Build Script
REM ---------------------------------------------------------------------------
REM Prerequisites:
REM   - .NET 9 SDK (or later with rollForward policy)
REM   - Inno Setup 6.x installed and iscc.exe on PATH
REM     (default: "C:\Program Files (x86)\Inno Setup 6\iscc.exe")
REM ---------------------------------------------------------------------------

setlocal
cd /d "%~dp0"

echo.
echo ============================================================
echo  Step 1: Publishing VybeDesk (win-x64, self-contained)
echo ============================================================
echo.

dotnet publish src\VybeDesk.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: dotnet publish failed. Fix build errors and retry.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo  Step 2: Compile the Inno Setup installer
echo ============================================================
echo.

REM Try iscc on PATH first, then fall back to the default install location.
where iscc >nul 2>&1
if %ERRORLEVEL% equ 0 (
    iscc installer.iss
) else if exist "C:\Program Files (x86)\Inno Setup 6\iscc.exe" (
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe" installer.iss
) else (
    echo.
    echo Inno Setup compiler (iscc.exe) not found.
    echo.
    echo Either:
    echo   1. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php
    echo   2. Add its install folder to your PATH
    echo   3. Open Inno Setup IDE and compile installer.iss manually
    echo.
    echo The publish output is ready at:
    echo   src\VybeDesk.App\bin\Release\net9.0\win-x64\publish\
    echo.
    pause
    exit /b 1
)

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Inno Setup compilation failed.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo  Done!
echo ============================================================
echo.
echo Installer written to: installer-output\VybeDesk-Setup-^<version^>.exe
echo.
pause
