@echo off
set "locusLocalRunner=%~dp0local.mjs"
if not exist "%locusLocalRunner%" set "locusLocalRunner=%~dp0local\local.mjs"
node "%locusLocalRunner%" stop
if errorlevel 1 pause
