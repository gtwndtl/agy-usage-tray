@echo off
cd /d "%~dp0"
echo ========================================================
echo  Antigravity HUD: Rebuilding, Committing, and Restarting
echo ========================================================
echo.
echo [1/5] Stopping running AntigravityHUD process...
taskkill /f /im AntigravityHUD.exe 2>nul
taskkill /f /im AgyUsageTray.exe 2>nul
timeout /t 1 /nobreak >nul

echo.
echo [2/5] Staging modified files...
git add Program.cs Services/AgyCliService.cs commit.bat

echo.
echo [3/5] Committing fix...
git commit -m "fix(cli): prevent terminal popups via hidden console inheritance and clean JSON parsing"

echo.
echo [4/5] Pushing to GitHub (gtwndtl/agy-usage-tray)...
git push origin main

echo.
echo [5/5] Building standalone Release executable...
dotnet publish -c Release -o publish

if %ERRORLEVEL% equ 0 (
    echo [OK] Build successful!
    copy /y "%~dp0publish\AntigravityHUD.exe" "%~dp0publish\AgyUsageTray.exe" >nul
    echo.
    echo ========================================================
    echo  Restarting Antigravity HUD...
    echo ========================================================
    start "" "%~dp0publish\AntigravityHUD.exe"
    echo Antigravity HUD is now running in system tray!
) else (
    echo.
    echo ========================================================
    echo  [ERROR] Build failed! Check errors above.
    echo ========================================================
)
echo.
pause
