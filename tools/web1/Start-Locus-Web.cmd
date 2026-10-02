@echo off
where node.exe >nul 2>&1
if errorlevel 1 (
  echo Can Node.js 22 tro len tren may de chay ban Web local.
  pause
  exit /b 1
)
set "locusLocalRunner=%~dp0local.mjs"
if not exist "%locusLocalRunner%" set "locusLocalRunner=%~dp0local\local.mjs"
node "%locusLocalRunner%" start
if errorlevel 1 (
  pause
  exit /b 1
)
start "Locus Web" "http://127.0.0.1:4183/"
