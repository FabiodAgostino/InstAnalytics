#include "Downloader.h"
#include <urlmon.h>
#include <wininet.h>

#pragma comment(lib, "urlmon.lib")
#pragma comment(lib, "wininet.lib")

namespace InstAnalyticsInstaller {

struct DownloadCallbackData {
    ProgressCallback callback;
    bool* cancelled;
};

Downloader::Downloader()
    : cancelled_(false)
{
}

Downloader::~Downloader()
{
}

void Downloader::Cancel()
{
    cancelled_ = true;
}

DWORD CALLBACK Downloader::ProgressRoutine(
    LARGE_INTEGER TotalFileSize,
    LARGE_INTEGER TotalBytesTransferred,
    LARGE_INTEGER StreamSize,
    LARGE_INTEGER StreamBytesTransferred,
    DWORD dwStreamNumber,
    DWORD dwCallbackReason,
    HANDLE hSourceFile,
    HANDLE hDestinationFile,
    LPVOID lpData)
{
    if (lpData == nullptr) {
        return PROGRESS_CONTINUE;
    }

    DownloadCallbackData* data = (DownloadCallbackData*)lpData;

    if (data->cancelled && *(data->cancelled)) {
        return PROGRESS_CANCEL;
    }

    if (TotalFileSize.QuadPart > 0 && data->callback) {
        int progress = (int)((TotalBytesTransferred.QuadPart * 100) / TotalFileSize.QuadPart);

        // Calculate download size in MB
        double downloadedMB = TotalBytesTransferred.QuadPart / (1024.0 * 1024.0);
        double totalMB = TotalFileSize.QuadPart / (1024.0 * 1024.0);

        wchar_t statusBuffer[256];
        swprintf_s(statusBuffer, L"Download in corso: %.1f MB / %.1f MB", downloadedMB, totalMB);

        data->callback(progress, statusBuffer);
    }

    return PROGRESS_CONTINUE;
}

bool Downloader::DownloadFile(const std::wstring& url, const std::wstring& outputPath, ProgressCallback callback)
{
    cancelled_ = false;

    // Use URLDownloadToFile with progress callback
    DownloadCallbackData callbackData;
    callbackData.callback = callback;
    callbackData.cancelled = &cancelled_;

    // Use WinINet for download with progress
    HINTERNET hInternet = InternetOpenW(L"InstAnalyticsInstaller",
        INTERNET_OPEN_TYPE_DIRECT, nullptr, nullptr, 0);

    if (!hInternet) {
        return false;
    }

    // Open URL with flags to follow redirects automatically
    HINTERNET hUrl = InternetOpenUrlW(hInternet, url.c_str(), nullptr, 0,
        INTERNET_FLAG_RELOAD | INTERNET_FLAG_NO_CACHE_WRITE | INTERNET_FLAG_KEEP_CONNECTION, 0);

    if (!hUrl) {
        InternetCloseHandle(hInternet);
        return false;
    }

    // Get file size
    DWORD fileSize = 0;
    DWORD bufferSize = sizeof(fileSize);
    DWORD index = 0;
    HttpQueryInfoW(hUrl, HTTP_QUERY_CONTENT_LENGTH | HTTP_QUERY_FLAG_NUMBER,
        &fileSize, &bufferSize, &index);

    // Open output file
    HANDLE hFile = CreateFileW(outputPath.c_str(), GENERIC_WRITE, 0, nullptr,
        CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);

    if (hFile == INVALID_HANDLE_VALUE) {
        InternetCloseHandle(hUrl);
        InternetCloseHandle(hInternet);
        return false;
    }

    // Download in chunks
    const DWORD BUFFER_SIZE = 8192;
    BYTE buffer[BUFFER_SIZE];
    DWORD totalBytesRead = 0;
    DWORD bytesRead = 0;
    DWORD bytesWritten = 0;

    bool success = true;

    while (!cancelled_ && InternetReadFile(hUrl, buffer, BUFFER_SIZE, &bytesRead) && bytesRead > 0) {
        if (!WriteFile(hFile, buffer, bytesRead, &bytesWritten, nullptr)) {
            success = false;
            break;
        }

        totalBytesRead += bytesRead;

        // Report progress
        if (callback && fileSize > 0) {
            int progress = (int)((totalBytesRead * 100) / fileSize);

            double downloadedMB = totalBytesRead / (1024.0 * 1024.0);
            double totalMB = fileSize / (1024.0 * 1024.0);

            wchar_t statusBuffer[256];
            swprintf_s(statusBuffer, L"Download in corso: %.1f MB / %.1f MB", downloadedMB, totalMB);

            callback(progress, statusBuffer);
        }
    }

    CloseHandle(hFile);
    InternetCloseHandle(hUrl);
    InternetCloseHandle(hInternet);

    if (cancelled_) {
        DeleteFileW(outputPath.c_str());
        return false;
    }

    return success && totalBytesRead > 0;
}

} // namespace InstAnalyticsInstaller
