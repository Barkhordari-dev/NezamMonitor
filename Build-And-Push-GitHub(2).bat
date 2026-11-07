@echo off
color 0A
echo =====================================================
echo   Offline Restore + Build + GitHub Update
echo   (Using Local Packages)
echo =====================================================

echo.
echo [1/5] Setting local cache path...
set NUGET_PACKAGES=C:\Users\m.barkhordari\.nuget\packages

echo.
echo [2/5] Disabling internet (nuget.org)...
"E:\abzar\.dotnet-sdk\dotnet.exe" nuget disable source nuget.org

echo.
echo [3/5] Restoring packages from local cache...
"E:\abzar\.dotnet-sdk\dotnet.exe" restore "E:\abzar\NezamMonitor-Pro\NezamMonitorPro.App\NezamMonitorPro.App.csproj" --ignore-failed-sources
if %ERRORLEVEL% neq 0 goto :failed

echo.
echo [4/5] Building and Publishing...
"E:\abzar\.dotnet-sdk\dotnet.exe" publish "E:\abzar\NezamMonitor-Pro\NezamMonitorPro.App\NezamMonitorPro.App.csproj" -c Release -o "E:\abzar\NezamMonitorPro-Portable" --no-restore
if %ERRORLEVEL% neq 0 goto :failed

echo.
echo [5/5] Updating GitHub (commit + push)...
cd /d "E:\abzar\NezamMonitor-Pro"

rem Safety: never commit while HEAD is detached (e.g. in the middle of a
rem rebase/merge) - that would create a commit that cannot be pushed and
rem can swallow unresolved conflicts. Finish the rebase/merge first (or
rem run "git rebase --abort" to undo it), then run this script again.
git symbolic-ref -q HEAD >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo   [ERROR] HEAD is detached - a rebase or merge is probably in
    echo   progress. Finish it first, then run this script again.
    goto :failed
)

where git >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo   [GitHub] git is not in PATH - skipping GitHub update.
    echo   Install Git or add it to PATH, then push manually.
    goto :done
)
git rev-parse --git-dir >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo   [GitHub] E:\abzar\NezamMonitor-Pro is not a git repository - skipping.
    goto :done
)

git status --porcelain > "%TEMP%\nezm_git_status.txt"
for %%A in ("%TEMP%\nezm_git_status.txt") do set SIZE=%%~zA
if "%SIZE%"=="0" (
    echo   [GitHub] No new changes to commit - going straight to push.
    goto :push
)

echo   [GitHub] Changes found. Staging...
git add -A
if %ERRORLEVEL% neq 0 goto :gitfailed

rem Commit message: pass it as the first argument, e.g.
rem   Build-And-Push-GitHub.bat "Round 8 fixes: alignment, select-all"
rem otherwise you will be asked; empty = dated default message.
set MSG=%~1
if "%MSG%"=="" set /p MSG=Commit message (Enter for default): 
if "%MSG%"=="" set MSG=Update %DATE% %TIME%
git commit -m "%MSG%"
if %ERRORLEVEL% neq 0 goto :gitfailed

:push
echo   [GitHub] Pushing to origin...
git push origin HEAD
if %ERRORLEVEL% neq 0 goto :gitfailed
echo   [GitHub] Pushed successfully.
goto :done

:gitfailed
echo.
echo   [GitHub] Git step failed. The build itself succeeded.
echo   If the push was REJECTED because GitHub has commits you do not have
echo   yet, run these two commands and then run this script again:
echo     cd /d E:\abzar\NezamMonitor-Pro
echo     git pull --rebase
echo     git push origin HEAD
echo   (If the rebase reports a conflict, keep YOUR local version of the
echo   conflicting files with: git checkout --theirs -- .  then
echo   git add -A  and  git rebase --continue. To undo a rebase entirely:
echo   git rebase --abort)
echo   Other causes: no git user configured (git config --global user.name
echo   "Your Name" and git config --global user.email "you@mail").
goto :done

:failed
echo.
echo =====================================================
echo   BUILD FAILED - GitHub was NOT updated.
echo =====================================================

:done
echo.
echo =====================================================
echo   Re-enabling internet (nuget.org)...
"E:\abzar\.dotnet-sdk\dotnet.exe" nuget enable source nuget.org
echo =====================================================
pause
