#pragma once

#include <string>
#include <functional>

namespace InstAnalyticsInstaller {

using ExtractionProgressCallback = std::function<void(int progress, const std::wstring& currentFile)>;

class ZipExtractor {
public:
    static bool Extract(const std::wstring& zipPath, const std::wstring& destinationPath, ExtractionProgressCallback callback = nullptr);

private:
    static bool ExtractWithShell(const std::wstring& zipPath, const std::wstring& destinationPath);
};

} // namespace InstAnalyticsInstaller
