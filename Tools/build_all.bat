@echo off
setlocal

pushd "%~dp0.."
set DOTNET_CLI_HOME=%CD%\.dotnet_home
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1

if not exist build mkdir build

dotnet restore .\Launcher\Launcher.csproj --configfile .\NuGet.config
if errorlevel 1 exit /b %errorlevel%

dotnet restore .\PatchBuilder\PatchBuilder.csproj --configfile .\NuGet.config
if errorlevel 1 exit /b %errorlevel%

dotnet restore .\ConfigBuilder\ConfigBuilder.csproj --configfile .\NuGet.config
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\Launcher\Launcher.csproj -c Release -o .\build\Launcher --no-restore
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\PatchBuilder\PatchBuilder.csproj -c Release -o .\build\PatchBuilder --no-restore
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\ConfigBuilder\ConfigBuilder.csproj -c Release -o .\build\ConfigBuilder --no-restore
if errorlevel 1 exit /b %errorlevel%

echo.
echo Build completed.
echo Launcher:     %CD%\build\Launcher\Launcher.exe
echo PatchBuilder: %CD%\build\PatchBuilder\PatchBuilder.exe
echo ConfigBuilder:%CD%\build\ConfigBuilder\ConfigBuilder.exe

popd
endlocal
