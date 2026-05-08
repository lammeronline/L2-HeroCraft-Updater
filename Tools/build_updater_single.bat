@echo off
setlocal

pushd "%~dp0.."
set DOTNET_CLI_HOME=%CD%\.dotnet_home
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1

if not exist build mkdir build
if not exist build\updater mkdir build\updater

dotnet restore .\Launcher\Launcher.csproj -r win-x64 --source https://api.nuget.org/v3/index.json
if errorlevel 1 exit /b %errorlevel%

dotnet restore .\PatchBuilder\PatchBuilder.csproj -r win-x64 --source https://api.nuget.org/v3/index.json
if errorlevel 1 exit /b %errorlevel%

dotnet restore .\ConfigBuilder\ConfigBuilder.csproj -r win-x64 --source https://api.nuget.org/v3/index.json
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\Launcher\Launcher.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o .\build\updater ^
  --no-restore ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\PatchBuilder\PatchBuilder.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o .\build\updater ^
  --no-restore ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false
if errorlevel 1 exit /b %errorlevel%

dotnet publish .\ConfigBuilder\ConfigBuilder.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o .\build\updater ^
  --no-restore ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false
if errorlevel 1 exit /b %errorlevel%

echo.
echo Single-file updater build completed:
echo   %CD%\build\updater\Launcher.exe
echo   %CD%\build\updater\PatchBuilder.exe
echo   %CD%\build\updater\ConfigBuilder.exe

popd
endlocal
