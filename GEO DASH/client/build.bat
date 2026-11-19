@echo off
chcp 65001 >nul
echo ============================================
echo  GEO DASH - сборка одного exe (win-x64)
echo ============================================
cd /d "%~dp0"

dotnet publish "GEO DASH.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -o "publish"

if errorlevel 1 (
  echo.
  echo Сборка не удалась. Проверь, что установлен .NET 8 SDK.
  pause
  exit /b 1
)

echo.
echo Готово: "%~dp0publish\GEO DASH.exe"
echo Данные игры (уровни, saved, музыка) лежат в папке assets рядом с exe.
pause
