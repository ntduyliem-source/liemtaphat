@echo off
where node.exe >nul 2>&1
if errorlevel 1 (
  echo Can Node.js 22 tro len de chay Locus Web local.
  pause
  exit /b 1
)
node "%~dp0local\local.mjs" start --port 4193
if errorlevel 1 (
  pause
  exit /b 1
)
start "Locus Web" "http://127.0.0.1:4193/"
