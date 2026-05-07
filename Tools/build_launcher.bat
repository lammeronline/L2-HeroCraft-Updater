@echo off
setlocal

pushd "%~dp0.."
set DOTNET_CLI_HOME=%CD%\.dotnet_home
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1

if not exist build mkdir build

dotnet restore .\Launcher\Launcher.csproj --configfile .\NuGet.config
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\Launcher\Launcher.csproj -c Release -o .\build\Launcher --no-restore
if errorlevel 1 exit /b %errorlevel%

echo.
echo Launcher build completed: %CD%\build\Launcher\Launcher.exe

popd
endlocal
