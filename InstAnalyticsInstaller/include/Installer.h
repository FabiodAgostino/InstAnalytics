#pragma once

#include <string>
#include <functional>
#include <windows.h>

namespace InstAnalyticsInstaller {

using InstallProgressCallback = std::function<void(int progress, const std::wstring& status)>;

class Installer {
public:
    Installer();

    bool InstallDotNet(const std::wstring& installerPath, InstallProgressCallback callback = nullptr);
    bool ExtractInstAnalytics(const std::wstring& zipPath, const std::wstring& destinationPath, InstallProgressCallback callback = nullptr);
    bool CreateShortcuts(const std::wstring& installPath);
    void Cancel();
    DWORD GetLastExitCode() const { return lastExitCode_; }

private:
    bool cancelled_;
    DWORD lastExitCode_;
    bool RunInstaller(const std::wstring& path);
    bool WaitForProcessCompletion(HANDLE hProcess, InstallProgressCallback callback);
};

} // namespace InstAnalyticsInstaller
