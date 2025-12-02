#include "Installer.h"
#include "ZipExtractor.h"
#include <shlobj.h>
#include <thread>
#include <chrono>

namespace InstAnalyticsInstaller {

Installer::Installer()
    : cancelled_(false)
    , lastExitCode_(0)
{
}

void Installer::Cancel()
{
    cancelled_ = true;
}

bool Installer::InstallDotNet(const std::wstring& installerPath, InstallProgressCallback callback)
{
    cancelled_ = false;

    if (callback) {
        callback(0, L"Avvio installazione .NET 10...");
    }

    // Use ShellExecuteEx to launch installer with elevation (runas)
    std::wstring parameters = L"/install /quiet /norestart";

    SHELLEXECUTEINFOW sei = { sizeof(sei) };
    sei.fMask = SEE_MASK_NOCLOSEPROCESS;
    sei.lpVerb = L"runas";  // Request elevation
    sei.lpFile = installerPath.c_str();
    sei.lpParameters = parameters.c_str();
    sei.nShow = SW_HIDE;

    if (!ShellExecuteExW(&sei)) {
        // Check if user cancelled UAC prompt
        DWORD error = GetLastError();
        if (error == ERROR_CANCELLED) {
            if (callback) {
                callback(0, L"Installazione annullata dall'utente");
            }
        }
        return false;
    }

    if (!sei.hProcess) {
        return false;
    }

    // Wait for installation to complete
    bool success = WaitForProcessCompletion(sei.hProcess, callback);

    lastExitCode_ = 0;
    GetExitCodeProcess(sei.hProcess, &lastExitCode_);

    CloseHandle(sei.hProcess);

    // Exit codes: 0 = success, 3010 = success with reboot required
    // 1638 = product already installed, 1641 = success with reboot initiated
    bool isSuccess = success && !cancelled_ &&
                     (lastExitCode_ == 0 || lastExitCode_ == 3010 ||
                      lastExitCode_ == 1638 || lastExitCode_ == 1641);

    if (!isSuccess && callback) {
        wchar_t errorMsg[256];
        swprintf_s(errorMsg, L"Installazione fallita. Exit code: %d", lastExitCode_);
        callback(0, errorMsg);
    }

    return isSuccess;
}

bool Installer::WaitForProcessCompletion(HANDLE hProcess, InstallProgressCallback callback)
{
    int progress = 10;
    const int maxProgress = 90;
    const int progressStep = 2;

    while (true) {
        DWORD waitResult = WaitForSingleObject(hProcess, 1000);

        if (waitResult == WAIT_OBJECT_0) {
            // Process completed
            if (callback) {
                callback(100, L"Installazione completata");
            }
            return true;
        }

        if (cancelled_) {
            TerminateProcess(hProcess, 1);
            return false;
        }

        // Update progress (simulated)
        if (callback && progress < maxProgress) {
            progress += progressStep;
            callback(progress, L"Installazione in corso...");
        }
    }
}

bool Installer::ExtractInstAnalytics(const std::wstring& zipPath, const std::wstring& destinationPath, InstallProgressCallback callback)
{
    cancelled_ = false;

    if (callback) {
        callback(0, L"Estrazione files in corso...");
    }

    // Create destination directory
    SHCreateDirectoryEx(nullptr, destinationPath.c_str(), nullptr);

    // Extract zip file
    bool success = ZipExtractor::Extract(zipPath, destinationPath,
        [this, callback](int progress, const std::wstring& currentFile) {
            if (cancelled_) return;
            if (callback) {
                callback(progress, L"Estrazione: " + currentFile);
            }
        });

    if (success && callback) {
        callback(100, L"Estrazione completata");
    }

    return success && !cancelled_;
}

bool Installer::CreateShortcuts(const std::wstring& installPath)
{
    // Get Desktop path
    wchar_t desktopPath[MAX_PATH];
    if (FAILED(SHGetFolderPathW(nullptr, CSIDL_DESKTOP, nullptr, 0, desktopPath))) {
        return false;
    }

    // Get Start Menu path
    wchar_t startMenuPath[MAX_PATH];
    if (FAILED(SHGetFolderPathW(nullptr, CSIDL_COMMON_PROGRAMS, nullptr, 0, startMenuPath))) {
        return false;
    }

    // Create shortcut on Desktop
    std::wstring desktopShortcut = std::wstring(desktopPath) + L"\\InstAnalytics.lnk";
    std::wstring exePath = installPath + L"\\InstAnalytics.exe";

    // Note: Full shortcut creation requires IShellLink COM interface
    // For simplicity, we'll create a basic shortcut using COM

    CoInitialize(nullptr);

    IShellLinkW* psl;
    HRESULT hres = CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER,
        IID_IShellLinkW, (LPVOID*)&psl);

    if (SUCCEEDED(hres)) {
        IPersistFile* ppf;

        psl->SetPath(exePath.c_str());
        psl->SetWorkingDirectory(installPath.c_str());
        psl->SetDescription(L"InstAnalytics - Instagram Analytics Tool");

        hres = psl->QueryInterface(IID_IPersistFile, (LPVOID*)&ppf);

        if (SUCCEEDED(hres)) {
            ppf->Save(desktopShortcut.c_str(), TRUE);
            ppf->Release();
        }

        psl->Release();
    }

    CoUninitialize();

    return SUCCEEDED(hres);
}

} // namespace InstAnalyticsInstaller
