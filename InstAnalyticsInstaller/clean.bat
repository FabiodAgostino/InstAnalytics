@echo off
echo ========================================
echo InstAnalytics Installer - Clean Script
echo ========================================
echo.

if exist "build" (
    echo Rimozione directory build...
    rmdir /s /q build
    echo Build directory rimossa.
) else (
    echo Nessuna directory build da rimuovere.
)

echo.
echo Pulizia completata!
echo.
pause
