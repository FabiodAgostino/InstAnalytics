#pragma once

#include <string>
#include <windows.h>

namespace InstAnalyticsInstaller {

enum class Architecture {
    X86,
    X64,
    Unknown
};

class DotNetChecker {
public:
    static bool IsDotNet10Installed();
    static Architecture GetSystemArchitecture();
    static std::wstring GetDotNetDownloadUrl();
    static bool VerifyAndFixDotNetPath();

private:
    static bool CheckRegistryForDotNet();
    static bool CheckCommandLineForDotNet();
    static bool AddDotNetToPath();
    static std::wstring FindDotNetInstallPath();
};

} // namespace InstAnalyticsInstaller
