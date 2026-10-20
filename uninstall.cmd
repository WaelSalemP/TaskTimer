@echo off
rem Removes Task Timer and its startup entry. Settings and the time log in %APPDATA%\TaskTimer are kept.
setlocal

taskkill /im TaskTimer.exe /f >nul 2>nul
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Task Timer.lnk" >nul 2>nul
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Task Timer.lnk" >nul 2>nul
if exist "%LOCALAPPDATA%\Programs\TaskTimer" rmdir /s /q "%LOCALAPPDATA%\Programs\TaskTimer"

echo Task Timer has been removed.
endlocal
