@echo off
echo ========================================
echo InstAnalytics Installer - Build Script
echo ========================================
echo.

REM Check if CMake is installed
cmake --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: CMake non trovato. Installare CMake prima di continuare.
    echo Download: https://cmake.org/download/
    pause
    exit /b 1
)

REM Create build directory
if not exist "build" (
    echo Creazione directory build...
    mkdir build
)

cd build

REM Configure CMake
echo Configurazione progetto con CMake...
cmake .. -G "Visual Studio 18 2026" -A x64
if %errorlevel% neq 0 (
    echo.
    echo Tentativo con Visual Studio 2022...
    cmake .. -G "Visual Studio 17 2022" -A x64
    if %errorlevel% neq 0 (
        echo.
        echo Tentativo con Visual Studio 2019...
        cmake .. -G "Visual Studio 16 2019" -A x64
        if %errorlevel% neq 0 (
            echo.
            echo ERROR: Impossibile configurare il progetto.
            echo Verificare di avere Visual Studio installato.
            cd ..
            pause
            exit /b 1
        )
    )
)

echo.
echo Compilazione in corso (Release)...
cmake --build . --config Release
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Compilazione fallita.
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo ========================================
echo Build completata con successo!
echo ========================================
echo.
echo Eseguibile: build\bin\Release\InstAnalyticsInstaller.exe
echo.
pause
