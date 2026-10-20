@echo off
rem Builds Task Timer (needs the .NET 10 SDK) in two steps:
rem   1. the app, into the "publish" folder next to this file (used by install.cmd);
rem   2. the MSI installer, TaskTimer.Installer\bin\Release\TaskTimerSetup.msi.
rem Both include the .NET runtime, so they work on PCs without .NET installed.
rem Nothing is installed by this script.
rem
rem The installer's version is raised by one on every build, because Windows Installer only
rem upgrades an existing installation to a higher version. To keep the current version instead:
rem     build.cmd keepversion
setlocal

set "SRC=%~dp0"
set "PUBLISH=%SRC%publish"
set "INSTALLER=%SRC%TaskTimer.Installer\TaskTimer.Installer.wixproj"
set "MSI=%SRC%TaskTimer.Installer\bin\Release\TaskTimerSetup.msi"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET SDK was not found. Install the .NET 10 SDK from https://dotnet.microsoft.com/download
    goto :fail
)

echo Building Task Timer...
dotnet publish "%SRC%TaskTimer\TaskTimer.csproj" -c Release -r win-x64 --self-contained true -o "%PUBLISH%" -nologo -v q
if errorlevel 1 goto :fail

if /i "%~1"=="keepversion" goto :installer
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$p = $env:INSTALLER; $t = [IO.File]::ReadAllText($p);" ^
  "$m = [regex]::Match($t, '<ProductVersion>(\d+)\.(\d+)\.(\d+)</ProductVersion>');" ^
  "if (-not $m.Success) { Write-Host 'ProductVersion was not found in the installer project.'; exit 1 }" ^
  "$v = '{0}.{1}.{2}' -f $m.Groups[1].Value, $m.Groups[2].Value, ([int]$m.Groups[3].Value + 1);" ^
  "[IO.File]::WriteAllText($p, $t.Replace($m.Value, '<ProductVersion>' + $v + '</ProductVersion>'), (New-Object Text.UTF8Encoding $false));" ^
  "Write-Host ('Installer version: ' + $v)"
if errorlevel 1 goto :fail

:installer
echo Building the installer...
dotnet build "%INSTALLER%" -c Release -nologo -v q
if errorlevel 1 goto :fail

echo.
echo Build succeeded:
echo   App:       "%PUBLISH%\TaskTimer.exe"
echo   Installer: "%MSI%"
endlocal
exit /b 0

:fail
echo.
echo Build failed.
endlocal
exit /b 1
