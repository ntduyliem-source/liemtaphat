@echo off
node "%~dp0local\local.mjs" stop --port 4193
if errorlevel 1 pause
