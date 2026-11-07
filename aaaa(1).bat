@echo off
setlocal

set "tagName=v1.1.0"
set "releaseTitle=NezamMonitorPro v1.1.0 - Optimized & Fixed"

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

echo 1. Adding files to git...
git status --porcelain > "%TEMP%\nezm_git_status.txt"
for %%A in ("%TEMP%\nezm_git_status.txt") do set SIZE=%%~zA
if "%SIZE%"=="0" (
    echo   No changes to commit.
    goto :push
)
git add -A
if %ERRORLEVEL% neq 0 goto :failed

rem Commit message: pass it as the first argument, e.g.
rem   aaaa.bat "Round 8 fixes: alignment, select-all"
rem otherwise you will be asked; empty = dated default message.
rem (The old hard-coded message described a previous release, so it no
rem longer matched what was actually being committed.)
set MSG=%~1
if "%MSG%"=="" set /p MSG=Commit message (Enter for default): 
if "%MSG%"=="" set MSG=Update %DATE% %TIME%
git commit -m "%MSG%"
if %ERRORLEVEL% neq 0 goto :failed

:push
echo 2. Pushing to GitHub...
git push origin main
if %ERRORLEVEL% neq 0 (
    echo.
    echo   [ERROR] Push to GitHub was REJECTED. The tag and release were
    echo   NOT created. GitHub has commits you do not have yet. Run:
    echo     git pull --rebase
    echo   then run this script again. If the rebase reports a conflict,
    echo   keep YOUR local version with: git checkout --theirs -- .
    echo   then: git add -A  and  git rebase --continue.
    goto :failed
)

echo 3. Publishing app...
set "PATH=E:\abzar\.dotnet-sdk;%PATH%"
dotnet publish NezamMonitorPro.App -c Release -o ./publish_output
if %ERRORLEVEL% neq 0 goto :failed

if exist "NezamMonitorPro-Portable.zip" del "NezamMonitorPro-Portable.zip"
tar -a -c -f "NezamMonitorPro-Portable.zip" -C "publish_output" .
if %ERRORLEVEL% neq 0 goto :failed

echo 4. Recreating tag on GitHub...
gh release delete %tagName% --yes 2>nul
git tag -d %tagName% 2>nul
git push --delete origin %tagName% 2>nul

git tag %tagName%
if %ERRORLEVEL% neq 0 goto :failed
git push origin %tagName%
if %ERRORLEVEL% neq 0 goto :failed

echo 5. Creating release on GitHub...
gh release create %tagName% "./NezamMonitorPro-Portable.zip" --title "%releaseTitle%" --notes "تغییرات این نسخه: - ایمپورت فوق‌سریع و کامل دیتابیس‌های نسخه قبلی با استفاده از Prepared Commands و Transaction - اصلاح ادغام (Merge) اسنپ‌شات‌ها به صورت هوشمند در سطح دیتابیس - تغییر فونت پیش‌فرض برنامه به B Nazanin - اصلاح نمایش تعداد پرونده‌ها در اسنپ‌شات‌ها - بهبود رنگ و قابلیت دیدن اسکرول‌بارها در تم‌های مختلف"
if %ERRORLEVEL% neq 0 goto :failed

echo Done! Release created successfully.
goto :done

:failed
echo.
echo =====================================================
echo   FAILED - see the error above. Nothing after the
echo   failed step was run.
echo =====================================================

:done
endlocal
pause
