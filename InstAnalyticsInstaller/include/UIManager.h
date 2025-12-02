#pragma once

#include <windows.h>
#include <string>
#include <functional>

namespace InstAnalyticsInstaller {

enum class InstallState {
    Welcome,
    CheckingDotNet,
    DownloadingDotNet,
    InstallingDotNet,
    SelectingPath,
    DownloadingApp,
    ExtractingApp,
    Completed,
    Error
};

class UIManager {
public:
    UIManager(HINSTANCE hInstance);
    ~UIManager();

    bool Initialize();
    int Run();
    void SetState(InstallState state);
    void UpdateProgress(int progress, const std::wstring& status);
    void SetError(const std::wstring& errorMessage);
    std::wstring GetInstallPath() const { return installPath_; }
    void SetInstallPath(const std::wstring& path) { installPath_ = path; }

    using InstallCallback = std::function<void()>;
    void SetInstallCallback(InstallCallback callback) { installCallback_ = callback; }

private:
    HINSTANCE hInstance_;
    HWND hwnd_;
    HWND progressBar_;
    HWND statusLabel_;
    HWND pathEdit_;
    HWND browseButton_;
    HWND installButton_;
    HWND cancelButton_;
    HWND closeButton_;
    HWND minimizeButton_;

    InstallState currentState_;
    std::wstring installPath_;
    std::wstring errorMessage_;

    HFONT titleFont_;
    HFONT normalFont_;
    HFONT footerFont_;
    HBRUSH backgroundBrush_;
    HBRUSH secondaryBrush_;
    HBRUSH accentBrush_;

    POINT dragPoint_;
    bool isDragging_;

    HWND hoveredButton_;
    bool trackingMouse_;

    InstallCallback installCallback_;

    static LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
    LRESULT HandleMessage(UINT uMsg, WPARAM wParam, LPARAM lParam);

    void CreateControls();
    void UpdateUI();
    void OnInstallButtonClick();
    void OnBrowseButtonClick();
    void OnCancelButtonClick();
    void OnCloseButtonClick();
    void OnMinimizeButtonClick();
    void PaintWindow(HDC hdc);
    void PaintTitleBar(HDC hdc);

    HWND CreateStyledButton(const wchar_t* text, int x, int y, int width, int height, int id, bool isAccent = false);
    HWND CreateStyledEdit(const wchar_t* text, int x, int y, int width, int height, int id);
    HWND CreateStyledLabel(const wchar_t* text, int x, int y, int width, int height, int id);
};

} // namespace InstAnalyticsInstaller
