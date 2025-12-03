#include "DotNetChecker.h"
#include "Constants.h"
#include <string>
#include <sstream>
#include <array>
#include <memory>
#include <vector>

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

bool DotNetChecker::VerifyAndFixDotNetPath()
{
    // First, check if dotnet command works
    FILE* pipe = _wpopen(L"dotnet --version 2>&1", L"r");
    if (!pipe) {
        // Command not found, try to add to PATH
        return AddDotNetToPath();
    }

    std::array<char, 128> buffer;
    std::string result;

    while (fgets(buffer.data(), buffer.size(), pipe) != nullptr) {
        result += buffer.data();
    }

    int exitCode = _pclose(pipe);

    // Check if command succeeded and version 10 is present
    if (exitCode == 0 && result.find("10.0.") != std::string::npos) {
        return true; // dotnet command works and version 10 is available
    }

    // Command failed or version 10 not found, try to fix PATH
    return AddDotNetToPath();
}

std::wstring DotNetChecker::FindDotNetInstallPath()
{
    // Common installation paths for .NET
    std::vector<std::wstring> possiblePaths = {
        L"C:\\Program Files\\dotnet",
        L"C:\\Program Files (x86)\\dotnet"
    };

    // Check each path for dotnet.exe
    for (const auto& path : possiblePaths) {
        std::wstring dotnetExe = path + L"\\dotnet.exe";
        DWORD fileAttr = GetFileAttributesW(dotnetExe.c_str());
        if (fileAttr != INVALID_FILE_ATTRIBUTES && !(fileAttr & FILE_ATTRIBUTE_DIRECTORY)) {
            return path;
        }
    }

    return L"";
}

bool DotNetChecker::AddDotNetToPath()
{
    // Find .NET installation path
    std::wstring dotnetPath = FindDotNetInstallPath();
    if (dotnetPath.empty()) {
        return false; // .NET not found
    }

    // Get current system PATH
    HKEY hKey;
    LONG result = RegOpenKeyExW(HKEY_LOCAL_MACHINE,
        L"SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Environment",
        0, KEY_READ | KEY_WRITE, &hKey);

    if (result != ERROR_SUCCESS) {
        return false;
    }

    // Read current PATH value
    wchar_t currentPath[32768];
    DWORD pathSize = sizeof(currentPath);
    DWORD pathType;

    result = RegQueryValueExW(hKey, L"Path", nullptr, &pathType,
        (LPBYTE)currentPath, &pathSize);

    if (result != ERROR_SUCCESS) {
        RegCloseKey(hKey);
        return false;
    }

    // Check if dotnet path is already in PATH
    std::wstring currentPathStr(currentPath);
    if (currentPathStr.find(dotnetPath) != std::wstring::npos) {
        RegCloseKey(hKey);

        // Broadcast environment change message
        SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
            (LPARAM)L"Environment", SMTO_ABORTIFHUNG, 5000, nullptr);

        return true; // Already in PATH, just broadcast change
    }

    // Add dotnet path to PATH
    std::wstring newPath = currentPathStr;
    if (!newPath.empty() && newPath.back() != L';') {
        newPath += L";";
    }
    newPath += dotnetPath;

    // Write updated PATH back to registry
    result = RegSetValueExW(hKey, L"Path", 0, pathType,
        (LPBYTE)newPath.c_str(), (DWORD)((newPath.length() + 1) * sizeof(wchar_t)));

    RegCloseKey(hKey);

    if (result != ERROR_SUCCESS) {
        return false;
    }

    // Broadcast environment change message to notify all windows
    SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
        (LPARAM)L"Environment", SMTO_ABORTIFHUNG, 5000, nullptr);

    return true;
}

} // namespace InstAnalyticsInstaller
