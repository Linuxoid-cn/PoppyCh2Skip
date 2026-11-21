@echo off
rem ===========================================================================
rem  PoppyCh2Skip - build script
rem  Author : Kernix   |   https://github.com/Linuxoid-cn
rem
rem  Builds ..\dist\PoppyCh2Skip.exe with the C# compiler that ships with
rem  .NET Framework 4.x (no Visual Studio / SDK required).
rem ===========================================================================
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [X] csc.exe not found. .NET Framework 4.x is required.
    pause
    exit /b 1
)

if not exist "..\dist" mkdir "..\dist"

"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 /out:"..\dist\PoppyCh2Skip.exe" PoppyCh2Skip.cs
if errorlevel 1 (
    echo [X] Build failed.
    pause
    exit /b 1
)

echo [OK] Built: dist\PoppyCh2Skip.exe
pause
endlocal