@echo off
setlocal

pushd "%~dp0.."
set DOTNET_CLI_HOME=%CD%\.dotnet_home
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1

if not exist build mkdir build
if not exist build\ConfigBuilder mkdir build\ConfigBuilder

dotnet restore .\ConfigBuilder\ConfigBuilder.csproj --configfile .\NuGet.config
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\ConfigBuilder\ConfigBuilder.csproj -c Release -o .\build\ConfigBuilder --no-restore
if errorlevel 1 exit /b %errorlevel%

echo.
echo ConfigBuilder build completed: %CD%\build\ConfigBuilder\ConfigBuilder.exe

popd
endlocal
