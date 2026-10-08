@echo off
setlocal
where node >nul 2>nul
if errorlevel 1 (
    echo Node.js was not found in PATH. Install Node.js and reopen this script.
    pause
    exit /b 1
)
node "%~dp0WeChatResourceServer.cjs" %*
if errorlevel 1 pause
endlocal
