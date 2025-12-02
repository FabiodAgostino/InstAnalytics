#pragma once

#include <string>
#include <functional>
#include <windows.h>

namespace InstAnalyticsInstaller {

using ProgressCallback = std::function<void(int progress, const std::wstring& status)>;

class Downloader {
public:
    Downloader();
    ~Downloader();

    bool DownloadFile(const std::wstring& url, const std::wstring& outputPath, ProgressCallback callback = nullptr);
    void Cancel();

private:
    bool cancelled_;
    static DWORD CALLBACK ProgressRoutine(
        LARGE_INTEGER TotalFileSize,
        LARGE_INTEGER TotalBytesTransferred,
        LARGE_INTEGER StreamSize,
        LARGE_INTEGER StreamBytesTransferred,
        DWORD dwStreamNumber,
        DWORD dwCallbackReason,
        HANDLE hSourceFile,
        HANDLE hDestinationFile,
        LPVOID lpData
    );
};

} // namespace InstAnalyticsInstaller
