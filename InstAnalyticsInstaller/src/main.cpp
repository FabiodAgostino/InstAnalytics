#include "UIManager.h"
#include "DotNetChecker.h"
#include "Downloader.h"
#include "Installer.h"
#include "Constants.h"
#include <windows.h>
#include <thread>
#include <shlobj.h>

using namespace InstAnalyticsInstaller;

UIManager* g_uiManager = nullptr;
Downloader* g_downloader = nullptr;
Installer* g_installer = nullptr;

void PerformInstallation()
{
    if (!g_uiManager) return;

    std::wstring tempPath;
    wchar_t tempDir[MAX_PATH];
    GetTempPathW(MAX_PATH, tempDir);
    tempPath = tempDir;

    try {
        // Step 1: Check if .NET 10 is installed
        g_uiManager->SetState(InstallState::CheckingDotNet);
        g_uiManager->UpdateProgress(5, L"Controllo presenza .NET 10...");

        Sleep(1000); // Brief pause for UI update

        bool dotNetInstalled = DotNetChecker::IsDotNet10Installed();

        if (!dotNetInstalled) {
            // Step 2: Download .NET 10
            g_uiManager->SetState(InstallState::DownloadingDotNet);
            g_uiManager->UpdateProgress(10, L"Download .NET 10 in corso...");

            std::wstring dotnetUrl = DotNetChecker::GetDotNetDownloadUrl();
            std::wstring dotnetInstallerPath = tempPath + L"dotnet-sdk-10.0.100-installer.exe";

            g_downloader = new Downloader();

            bool downloadSuccess = g_downloader->DownloadFile(
                dotnetUrl,
                dotnetInstallerPath,
                [](int progress, const std::wstring& status) {
                    if (g_uiManager) {
                        // Map download progress to 10-40% range
                        int mappedProgress = 10 + (progress * 30 / 100);
                        g_uiManager->UpdateProgress(mappedProgress, status);
                    }
                }
            );

            delete g_downloader;
            g_downloader = nullptr;

            if (!downloadSuccess) {
                g_uiManager->SetError(L"Errore durante il download di .NET 10");
                return;
            }

            // Step 3: Install .NET 10
            g_uiManager->SetState(InstallState::InstallingDotNet);
            g_uiManager->UpdateProgress(40, L"Installazione .NET 10...");

            g_installer = new Installer();

            bool installSuccess = g_installer->InstallDotNet(
                dotnetInstallerPath,
                [](int progress, const std::wstring& status) {
                    if (g_uiManager) {
                        // Map installation progress to 40-60% range
                        int mappedProgress = 40 + (progress * 20 / 100);
                        g_uiManager->UpdateProgress(mappedProgress, status);
                    }
                }
            );

            // Clean up installer file
            DeleteFileW(dotnetInstallerPath.c_str());

            if (!installSuccess) {
                DWORD exitCode = g_installer->GetLastExitCode();
                delete g_installer;
                g_installer = nullptr;

                wchar_t errorMsg[512];
                swprintf_s(errorMsg, L"Errore durante l'installazione di .NET 10 (exit code: %d)", exitCode);
                g_uiManager->SetError(errorMsg);
                return;
            }

            // Verify .NET installation and fix PATH if needed
            g_uiManager->UpdateProgress(55, L"Verifica installazione .NET 10...");
            Sleep(500);

            bool pathFixed = DotNetChecker::VerifyAndFixDotNetPath();
            if (!pathFixed) {
                g_uiManager->SetError(L"Impossibile configurare il PATH per .NET 10");
                delete g_installer;
                g_installer = nullptr;
                return;
            }

            g_uiManager->UpdateProgress(60, L".NET 10 configurato correttamente");
            Sleep(300);
        } else {
            g_uiManager->UpdateProgress(30, L".NET 10 già installato");
            Sleep(500);
        }

        // Step 4: Download InstAnalytics
        g_uiManager->SetState(InstallState::DownloadingApp);
        g_uiManager->UpdateProgress(65, L"Download InstAnalytics...");

        std::wstring appZipPath = tempPath + L"InstAnalytics.zip";

        g_downloader = new Downloader();

        bool appDownloadSuccess = g_downloader->DownloadFile(
            URLs::INSTANALYTICS_ZIP,
            appZipPath,
            [](int progress, const std::wstring& status) {
                if (g_uiManager) {
                    // Map download progress to 65-80% range
                    int mappedProgress = 65 + (progress * 15 / 100);
                    g_uiManager->UpdateProgress(mappedProgress, status);
                }
            }
        );

        delete g_downloader;
        g_downloader = nullptr;

        if (!appDownloadSuccess) {
            g_uiManager->SetError(L"Errore durante il download di InstAnalytics");
            return;
        }

        // Step 5: Extract InstAnalytics
        g_uiManager->SetState(InstallState::ExtractingApp);
        g_uiManager->UpdateProgress(80, L"Estrazione files...");

        std::wstring installPath = g_uiManager->GetInstallPath();

        if (!g_installer) {
            g_installer = new Installer();
        }

        bool extractSuccess = g_installer->ExtractInstAnalytics(
            appZipPath,
            installPath,
            [](int progress, const std::wstring& status) {
                if (g_uiManager) {
                    // Map extraction progress to 80-93% range
                    int mappedProgress = 80 + (progress * 13 / 100);
                    g_uiManager->UpdateProgress(mappedProgress, status);
                }
            }
        );

        // Clean up zip file
        DeleteFileW(appZipPath.c_str());

        if (!extractSuccess) {
            delete g_installer;
            g_installer = nullptr;
            g_uiManager->SetError(L"Errore durante l'estrazione di InstAnalytics");
            return;
        }

        // Step 6: Create shortcuts
        g_uiManager->UpdateProgress(94, L"Creazione collegamenti...");
        g_installer->CreateShortcuts(installPath);

        delete g_installer;
        g_installer = nullptr;

        // Step 7: Complete
        g_uiManager->UpdateProgress(100, L"Installazione completata!");
        g_uiManager->SetState(InstallState::Completed);

    }
    catch (...) {
        g_uiManager->SetError(L"Errore imprevisto durante l'installazione");

        if (g_downloader) {
            delete g_downloader;
            g_downloader = nullptr;
        }

        if (g_installer) {
            delete g_installer;
            g_installer = nullptr;
        }
    }
}

// Thread function to run installation without blocking UI
DWORD WINAPI InstallationThreadProc(LPVOID lpParam)
{
    PerformInstallation();
    return 0;
}

void StartInstallation()
{
    HANDLE hThread = CreateThread(nullptr, 0, InstallationThreadProc, nullptr, 0, nullptr);
    if (hThread) {
        CloseHandle(hThread);
    }
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    // Initialize COM
    CoInitialize(nullptr);

    // Create UI Manager
    UIManager uiManager(hInstance);
    g_uiManager = &uiManager;

    if (!uiManager.Initialize()) {
        MessageBoxW(nullptr,
            L"Impossibile inizializzare l'interfaccia utente.",
            L"Errore",
            MB_OK | MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    // Set install callback
    uiManager.SetInstallCallback(StartInstallation);

    // Run message loop
    int result = uiManager.Run();

    // Cleanup
    g_uiManager = nullptr;
    CoUninitialize();

    return result;
}
