#include "ZipExtractor.h"
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <atlbase.h>

namespace InstAnalyticsInstaller {

bool ZipExtractor::Extract(const std::wstring& zipPath, const std::wstring& destinationPath, ExtractionProgressCallback callback)
{
    return ExtractWithShell(zipPath, destinationPath);
}

bool ZipExtractor::ExtractWithShell(const std::wstring& zipPath, const std::wstring& destinationPath)
{
    CoInitialize(nullptr);

    bool success = false;

    // Create temporary extraction folder
    wchar_t tempDir[MAX_PATH];
    GetTempPathW(MAX_PATH, tempDir);
    std::wstring tempExtractPath = std::wstring(tempDir) + L"InstAnalytics_temp_extract";

    // Create temp and destination folders
    SHCreateDirectoryExW(nullptr, tempExtractPath.c_str(), nullptr);
    SHCreateDirectoryExW(nullptr, destinationPath.c_str(), nullptr);

    // Use Shell to extract ZIP
    CComPtr<IShellDispatch> pShellDispatch;
    HRESULT hr = CoCreateInstance(CLSID_Shell, nullptr, CLSCTX_INPROC_SERVER,
        IID_IShellDispatch, (void**)&pShellDispatch);

    if (SUCCEEDED(hr)) {
        // Get source folder (ZIP file)
        CComVariant varZipPath(zipPath.c_str());
        CComPtr<Folder> pZipFolder;
        hr = pShellDispatch->NameSpace(varZipPath, &pZipFolder);

        if (SUCCEEDED(hr) && pZipFolder) {
            // Get temp destination folder
            CComVariant varTempPath(tempExtractPath.c_str());
            CComPtr<Folder> pTempFolder;
            hr = pShellDispatch->NameSpace(varTempPath, &pTempFolder);

            if (SUCCEEDED(hr) && pTempFolder) {
                // Get items from ZIP
                CComPtr<FolderItems> pItems;
                hr = pZipFolder->Items(&pItems);

                if (SUCCEEDED(hr) && pItems) {
                    CComVariant varItems(pItems);
                    CComVariant varOptions(FOF_NO_UI); // No UI dialogs

                    // Copy items to temp folder (this extracts the ZIP)
                    hr = pTempFolder->CopyHere(varItems, varOptions);
                    success = SUCCEEDED(hr);

                    // Wait for extraction to complete
                    Sleep(3000);

                    // Now find the first (and should be only) folder in temp directory
                    WIN32_FIND_DATAW findData;
                    std::wstring searchPath = tempExtractPath + L"\\*";
                    HANDLE hFind = FindFirstFileW(searchPath.c_str(), &findData);

                    if (hFind != INVALID_HANDLE_VALUE) {
                        do {
                            if ((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) &&
                                wcscmp(findData.cFileName, L".") != 0 &&
                                wcscmp(findData.cFileName, L"..") != 0) {

                                // Found the wrapper folder - now copy its contents to destination
                                std::wstring wrapperPath = tempExtractPath + L"\\" + findData.cFileName;

                                CComVariant varWrapperPath(wrapperPath.c_str());
                                CComPtr<Folder> pWrapperFolder;
                                hr = pShellDispatch->NameSpace(varWrapperPath, &pWrapperFolder);

                                if (SUCCEEDED(hr) && pWrapperFolder) {
                                    CComVariant varDestPath(destinationPath.c_str());
                                    CComPtr<Folder> pDestFolder;
                                    hr = pShellDispatch->NameSpace(varDestPath, &pDestFolder);

                                    if (SUCCEEDED(hr) && pDestFolder) {
                                        // Get items from wrapper folder
                                        CComPtr<FolderItems> pWrapperItems;
                                        hr = pWrapperFolder->Items(&pWrapperItems);

                                        if (SUCCEEDED(hr) && pWrapperItems) {
                                            CComVariant varWrapperItems(pWrapperItems);
                                            CComVariant varOptions2(FOF_NO_UI);

                                            // Copy contents to final destination
                                            hr = pDestFolder->CopyHere(varWrapperItems, varOptions2);
                                            Sleep(2000);
                                            success = SUCCEEDED(hr);
                                        }
                                    }
                                }
                                break;
                            }
                        } while (FindNextFileW(hFind, &findData));

                        FindClose(hFind);
                    }
                }
            }
        }
    }

    // Clean up temp directory
    std::wstring cleanupCmd = L"cmd.exe /c rd /s /q \"" + tempExtractPath + L"\"";
    _wsystem(cleanupCmd.c_str());

    CoUninitialize();

    return success;
}

} // namespace InstAnalyticsInstaller
