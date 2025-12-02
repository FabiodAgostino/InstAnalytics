#pragma once

#include <string>

namespace InstAnalyticsInstaller {

// Color palette matching WPF application
namespace Colors {
    constexpr unsigned long PRIMARY_BG = 0x2E1A1A;      // #1A1A2E
    constexpr unsigned long SECONDARY_BG = 0x3E2116;    // #16213E
    constexpr unsigned long ACCENT = 0x6045E9;          // #E94560
    constexpr unsigned long TEXT_PRIMARY = 0xFFFFFF;    // White
    constexpr unsigned long TEXT_SECONDARY = 0xC0C0C0;  // Light gray
    constexpr unsigned long BORDER = 0x4A4A4A;          // Dark gray
}

// URLs
namespace URLs {
    const std::wstring DOTNET_X64 = L"https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-win-x64.exe";
    const std::wstring DOTNET_X86 = L"https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-win-x86.exe";
    const std::wstring INSTANALYTICS_ZIP = L"https://github.com/FabiodAgostino/InstAnalytics/releases/download/release/InstAnalytics.1.0.0.zip";
}

// Application info
namespace AppInfo {
    const std::wstring NAME = L"InstAnalytics Installer";
    const std::wstring VERSION = L"1.0.0";
    const std::wstring DEFAULT_INSTALL_PATH = L"C:\\Program Files\\InstAnalytics";
}

// Window dimensions
namespace WindowSize {
    constexpr int WIDTH = 600;
    constexpr int HEIGHT = 500;
    constexpr int TITLE_BAR_HEIGHT = 40;
}

} // namespace InstAnalyticsInstaller
