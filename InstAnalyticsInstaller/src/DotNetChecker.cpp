#include "DotNetChecker.h"
#include "Constants.h"
#include <string>
#include <sstream>
#include <array>
#include <memory>

namespace InstAnalyticsInstaller {

bool DotNetChecker::IsDotNet10Installed()
{
    // Try checking via command line first (most reliable)
    if (CheckCommandLineForDotNet()) {
        return true;
    }

    // Fallback to registry check
    return CheckRegistryForDotNet();
}

bool DotNetChecker::CheckCommandLineForDotNet()
{
    // Execute 'dotnet --list-sdks' command
    FILE* pipe = _wpopen(L"dotnet --list-sdks 2>&1", L"r");
    if (!pipe) {
        return false;
    }

    std::array<char, 128> buffer;
    std::string result;

    while (fgets(buffer.data(), buffer.size(), pipe) != nullptr) {
        result += buffer.data();
    }

    int exitCode = _pclose(pipe);

    // Check if command succeeded
    if (exitCode != 0) {
        return false;
    }

    // Check if output contains version 10.0
    return result.find("10.0.") != std::string::npos;
}

bool DotNetChecker::CheckRegistryForDotNet()
{
    HKEY hKey;
    bool found = false;

    // Check for .NET SDK installations in registry
    // Path: HKEY_LOCAL_MACHINE\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sdk or x86\sdk
    const wchar_t* paths[] = {
        L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64\\sdk",
        L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x86\\sdk"
    };

    for (const auto& path : paths) {
        LONG result = RegOpenKeyEx(HKEY_LOCAL_MACHINE, path, 0, KEY_READ, &hKey);

        if (result == ERROR_SUCCESS) {
            // Enumerate values to find version 10.0.x
            DWORD index = 0;
            wchar_t valueName[256];
            DWORD valueNameSize = sizeof(valueName) / sizeof(wchar_t);

            while (RegEnumValue(hKey, index++, valueName, &valueNameSize, nullptr, nullptr, nullptr, nullptr) == ERROR_SUCCESS) {
                std::wstring version(valueName);
                if (version.find(L"10.0.") == 0) {
                    found = true;
                    break;
                }
                valueNameSize = sizeof(valueName) / sizeof(wchar_t);
            }

            RegCloseKey(hKey);

            if (found) {
                break;
            }
        }
    }

    return found;
}

Architecture DotNetChecker::GetSystemArchitecture()
{
    SYSTEM_INFO si;
    GetNativeSystemInfo(&si);

    switch (si.wProcessorArchitecture) {
    case PROCESSOR_ARCHITECTURE_AMD64:
        return Architecture::X64;
    case PROCESSOR_ARCHITECTURE_INTEL:
        return Architecture::X86;
    default:
        return Architecture::Unknown;
    }
}

std::wstring DotNetChecker::GetDotNetDownloadUrl()
{
    Architecture arch = GetSystemArchitecture();

    switch (arch) {
    case Architecture::X64:
        return URLs::DOTNET_X64;
    case Architecture::X86:
        return URLs::DOTNET_X86;
    default:
        return URLs::DOTNET_X64; // Default to x64
    }
}

} // namespace InstAnalyticsInstaller
