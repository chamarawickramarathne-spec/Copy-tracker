@echo off
rem SmartCopy dev launcher
setlocal
set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"
"%DOTNET%" run --project src\SmartCopy.App -c Debug
endlocal
