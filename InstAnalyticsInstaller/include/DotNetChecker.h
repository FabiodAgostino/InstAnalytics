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

private:
    static bool CheckRegistryForDotNet();
    static bool CheckCommandLineForDotNet();
};

} // namespace InstAnalyticsInstaller
