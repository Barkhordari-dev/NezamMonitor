@echo off
setlocal EnableDelayedExpansion
cd /d E:\abzar\NezamMonitor-Pro
if not exist NezamMonitorPro.sln (
    echo [ERROR] NezamMonitorPro.sln not found in E:\abzar\NezamMonitor-Pro
    echo Nothing was changed.
    pause
    exit /b 1
)
echo =====================================================
echo   NezamMonitor - update source + delete extra files
echo =====================================================
echo.
echo This will:
echo   [1] Update the source from GitHub (git fetch + reset --hard)
echo       - brings in the latest version (v12.4)
echo       - NOTE: your own uncommitted edits would be discarded;
echo         the data folder (database) is NOT touched.
echo   [2] Delete the extra files:
echo       - NezamMonitor.App.dll, *.bak files, WelcomeWindow files
echo       - NezamMonitor-cleanup-extra-files.bat (old bat)
echo   [3] Commit the deletions and push to GitHub.
echo.
echo Press any key to start, or Ctrl+C to cancel.
pause >nul
echo.
echo [1/3] Updating source from GitHub...
where git >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] git is not in PATH - cannot update the source.
    pause
    exit /b 1
)
git rev-parse --git-dir >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] This folder is not a git repository.
    pause
    exit /b 1
)
git symbolic-ref -q HEAD >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] HEAD is detached - a rebase or merge is probably in progress.
    echo Finish it first, then run this script again.
    pause
    exit /b 1
)
git fetch origin
if %ERRORLEVEL% neq 0 (
    echo [ERROR] git fetch failed - check your internet connection.
    pause
    exit /b 1
)
git reset --hard origin/main
if %ERRORLEVEL% neq 0 (
    echo [ERROR] git reset failed.
    pause
    exit /b 1
)
git log -1 --oneline
echo   source updated to origin/main.
echo.
echo [2/3] Deleting extra files...
set DEL=0
if exist "NezamMonitor.App.dll" (
    del "NezamMonitor.App.dll"
    set /a DEL+=1
    echo   deleted NezamMonitor.App.dll
) else (
    echo   not found: NezamMonitor.App.dll
)
if exist "NezamMonitorPro.App\NezamMonitor.App.csproj.bak" (
    del "NezamMonitorPro.App\NezamMonitor.App.csproj.bak"
    set /a DEL+=1
    echo   deleted NezamMonitor.App.csproj.bak
) else (
    echo   not found: NezamMonitor.App.csproj.bak
)
if exist "NezamMonitorPro.App\Views\FeesView.xaml.bak" (
    del "NezamMonitorPro.App\Views\FeesView.xaml.bak"
    set /a DEL+=1
    echo   deleted FeesView.xaml.bak
) else (
    echo   not found: FeesView.xaml.bak
)
if exist "NezamMonitorPro.App\WelcomeWindow.xaml" (
    del "NezamMonitorPro.App\WelcomeWindow.xaml"
    set /a DEL+=1
    echo   deleted WelcomeWindow.xaml
) else (
    echo   not found: WelcomeWindow.xaml
)
if exist "NezamMonitorPro.App\WelcomeWindow.xaml.cs" (
    del "NezamMonitorPro.App\WelcomeWindow.xaml.cs"
    set /a DEL+=1
    echo   deleted WelcomeWindow.xaml.cs
) else (
    echo   not found: WelcomeWindow.xaml.cs
)
if exist "NezamMonitor-cleanup-extra-files.bat" (
    del "NezamMonitor-cleanup-extra-files.bat"
    set /a DEL+=1
    echo   deleted NezamMonitor-cleanup-extra-files.bat
) else (
    echo   not found: NezamMonitor-cleanup-extra-files.bat
)
echo   deleted !DEL! files.
echo.
echo [3/3] Committing the deletions and pushing...
findstr /C:"*.bat" ".gitignore" >nul 2>&1
if %ERRORLEVEL%==0 (
    echo   .bat files are already ignored by git.
) else (
    echo *.bat>> ".gitignore"
    echo   added *.bat to .gitignore - this script itself is never committed.
)
git add -A
git commit -m "Remove dead files (WelcomeWindow, stray dll/bak)" 2>nul
if %ERRORLEVEL% neq 0 echo   nothing new to commit.
git push origin HEAD
if %ERRORLEVEL% neq 0 (
    echo [WARNING] git push failed - the deletions are committed locally.
    echo Run "git push origin HEAD" again when your connection is back.
    pause
    exit /b 1
)
echo.
echo Done. Source is updated and the extra files are gone from disk and GitHub.
pause
endlocal
