@echo off
rem Installs Task Timer for the current user and makes it start with Windows.
rem No administrator rights are needed. Run again to upgrade.
setlocal

set "SRC=%~dp0"
set "PUBLISH=%SRC%publish"
set "TARGET=%LOCALAPPDATA%\Programs\TaskTimer"
set "EXE=%TARGET%\TaskTimer.exe"
set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Task Timer.lnk"
set "STARTMENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Task Timer.lnk"

rem With the source and the .NET SDK present, always rebuild so the latest code is installed.
rem Otherwise use the ready-made "publish" folder shipped next to this file.
set "CANBUILD=1"
if not exist "%SRC%TaskTimer\TaskTimer.csproj" set "CANBUILD="
where dotnet >nul 2>nul
if errorlevel 1 set "CANBUILD="

if defined CANBUILD (
    echo Building Task Timer...
    dotnet publish "%SRC%TaskTimer\TaskTimer.csproj" -c Release -r win-x64 --self-contained true -o "%PUBLISH%" -nologo -v q
    if errorlevel 1 goto :fail
)
if not exist "%PUBLISH%\TaskTimer.exe" (
    echo The "publish" folder is missing and the app cannot be built here ^(no source or no .NET SDK^).
    goto :fail
)

rem The files cannot be replaced while the app is running.
taskkill /im TaskTimer.exe /f >nul 2>nul

echo Installing to "%TARGET%"...
robocopy "%PUBLISH%" "%TARGET%" /mir /njh /njs /ndl /nfl /np >nul
if errorlevel 8 (
    echo Copying the files failed.
    goto :fail
)

echo Creating shortcuts...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s = New-Object -ComObject WScript.Shell;" ^
  "foreach ($p in $env:STARTUP, $env:STARTMENU) {" ^
  "  $l = $s.CreateShortcut($p); $l.TargetPath = $env:EXE; $l.WorkingDirectory = $env:TARGET;" ^
  "  $l.Description = 'Task Timer'; $l.Save() }"
if errorlevel 1 goto :fail

start "" "%EXE%"
echo.
echo Task Timer is installed and will start automatically when you sign in to Windows.
endlocal
exit /b 0

:fail
echo.
echo Installation failed.
pause
endlocal
exit /b 1
