#include "UIManager.h"
#include "Constants.h"
#include <commctrl.h>
#include <shlobj.h>
#include <gdiplus.h>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "gdiplus.lib")

namespace InstAnalyticsInstaller {

constexpr int ID_INSTALL_BUTTON = 1001;
constexpr int ID_BROWSE_BUTTON = 1002;
constexpr int ID_CLOSE_BUTTON = 1003;
constexpr int ID_MINIMIZE_BUTTON = 1004;
constexpr int ID_PATH_EDIT = 1005;
constexpr int ID_PROGRESS_BAR = 1006;
constexpr int ID_STATUS_LABEL = 1007;
constexpr int ID_CANCEL_BUTTON = 1008;

UIManager::UIManager(HINSTANCE hInstance)
    : hInstance_(hInstance)
    , hwnd_(nullptr)
    , progressBar_(nullptr)
    , statusLabel_(nullptr)
    , pathEdit_(nullptr)
    , browseButton_(nullptr)
    , installButton_(nullptr)
    , cancelButton_(nullptr)
    , closeButton_(nullptr)
    , minimizeButton_(nullptr)
    , currentState_(InstallState::Welcome)
    , installPath_(AppInfo::DEFAULT_INSTALL_PATH)
    , titleFont_(nullptr)
    , normalFont_(nullptr)
    , footerFont_(nullptr)
    , backgroundBrush_(nullptr)
    , secondaryBrush_(nullptr)
    , accentBrush_(nullptr)
    , isDragging_(false)
    , hoveredButton_(nullptr)
    , trackingMouse_(false)
{
    dragPoint_ = { 0, 0 };
}

UIManager::~UIManager()
{
    if (titleFont_) DeleteObject(titleFont_);
    if (normalFont_) DeleteObject(normalFont_);
    if (footerFont_) DeleteObject(footerFont_);
    if (backgroundBrush_) DeleteObject(backgroundBrush_);
    if (secondaryBrush_) DeleteObject(secondaryBrush_);
    if (accentBrush_) DeleteObject(accentBrush_);
}

bool UIManager::Initialize()
{
    // Initialize Common Controls
    INITCOMMONCONTROLSEX icex;
    icex.dwSize = sizeof(INITCOMMONCONTROLSEX);
    icex.dwICC = ICC_STANDARD_CLASSES | ICC_PROGRESS_CLASS;
    InitCommonControlsEx(&icex);

    // Register window class
    WNDCLASSEX wc = { };
    wc.cbSize = sizeof(WNDCLASSEX);
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance_;
    wc.lpszClassName = L"InstAnalyticsInstallerClass";
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = CreateSolidBrush(Colors::PRIMARY_BG);
    wc.hIcon = LoadIcon(hInstance_, MAKEINTRESOURCE(101));  // Load icon from resources
    wc.hIconSm = LoadIcon(hInstance_, MAKEINTRESOURCE(101));

    if (!RegisterClassEx(&wc)) {
        return false;
    }

    // Create brushes
    backgroundBrush_ = CreateSolidBrush(Colors::PRIMARY_BG);
    secondaryBrush_ = CreateSolidBrush(Colors::SECONDARY_BG);
    accentBrush_ = CreateSolidBrush(Colors::ACCENT);

    // Create fonts
    titleFont_ = CreateFont(
        24, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI"
    );

    normalFont_ = CreateFont(
        16, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI"
    );

    footerFont_ = CreateFont(
        12, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI"
    );

    // Create main window
    hwnd_ = CreateWindowEx(
        WS_EX_LAYERED | WS_EX_APPWINDOW,
        L"InstAnalyticsInstallerClass",
        AppInfo::NAME.c_str(),
        WS_POPUP | WS_VISIBLE,
        CW_USEDEFAULT, CW_USEDEFAULT,
        WindowSize::WIDTH, WindowSize::HEIGHT,
        nullptr, nullptr, hInstance_, this
    );

    if (!hwnd_) {
        return false;
    }

    // Set window transparency for rounded corners effect
    SetLayeredWindowAttributes(hwnd_, 0, 255, LWA_ALPHA);

    // Center window on screen
    RECT rect;
    GetWindowRect(hwnd_, &rect);
    int x = (GetSystemMetrics(SM_CXSCREEN) - (rect.right - rect.left)) / 2;
    int y = (GetSystemMetrics(SM_CYSCREEN) - (rect.bottom - rect.top)) / 2;
    SetWindowPos(hwnd_, nullptr, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);

    CreateControls();
    UpdateUI();

    return true;
}

void UIManager::CreateControls()
{
    int margin = 40;
    int currentY = WindowSize::TITLE_BAR_HEIGHT + 60;

    // Title bar buttons (using Unicode symbols like WPF)
    minimizeButton_ = CreateStyledButton(L"─", WindowSize::WIDTH - 80, 5, 30, 30, ID_MINIMIZE_BUTTON);
    closeButton_ = CreateStyledButton(L"✕", WindowSize::WIDTH - 45, 5, 30, 30, ID_CLOSE_BUTTON);

    // Welcome message
    HWND welcomeLabel = CreateStyledLabel(
        L"Benvenuto nell'installer di InstAnalytics",
        margin, currentY, WindowSize::WIDTH - 2 * margin, 30, 2000
    );
    SendMessage(welcomeLabel, WM_SETFONT, (WPARAM)titleFont_, TRUE);
    currentY += 50;

    // Description
    HWND descLabel = CreateStyledLabel(
        L"Questo installer configurerà il tuo sistema e installerà InstAnalytics.",
        margin, currentY, WindowSize::WIDTH - 2 * margin, 40, 2001
    );
    currentY += 60;

    // Installation path label
    HWND pathLabel = CreateStyledLabel(
        L"Cartella di installazione:",
        margin, currentY, WindowSize::WIDTH - 2 * margin, 25, 2002
    );
    currentY += 30;

    // Path edit and browse button
    pathEdit_ = CreateStyledEdit(
        installPath_.c_str(),
        margin, currentY, WindowSize::WIDTH - 2 * margin - 100, 35, ID_PATH_EDIT
    );
    SendMessage(pathEdit_, WM_SETFONT, (WPARAM)normalFont_, TRUE);

    browseButton_ = CreateStyledButton(
        L"Sfoglia...",
        WindowSize::WIDTH - margin - 90, currentY, 90, 35, ID_BROWSE_BUTTON
    );
    currentY += 55;

    // Progress bar
    progressBar_ = CreateWindowEx(
        0, PROGRESS_CLASS, nullptr,
        WS_CHILD | WS_VISIBLE | PBS_SMOOTH,
        margin, currentY, WindowSize::WIDTH - 2 * margin, 25,
        hwnd_, (HMENU)ID_PROGRESS_BAR, hInstance_, nullptr
    );
    SendMessage(progressBar_, PBM_SETRANGE, 0, MAKELPARAM(0, 100));
    SendMessage(progressBar_, PBM_SETPOS, 0, 0);
    currentY += 35;

    // Status label
    statusLabel_ = CreateStyledLabel(
        L"Pronto per l'installazione",
        margin, currentY, WindowSize::WIDTH - 2 * margin, 25, ID_STATUS_LABEL
    );
    currentY += 50;

    // Install and Cancel buttons (side by side)
    int buttonWidth = (WindowSize::WIDTH - 2 * margin - 10) / 2; // 10px gap
    installButton_ = CreateStyledButton(
        L"Installa",
        margin, currentY, buttonWidth, 45, ID_INSTALL_BUTTON, true
    );

    cancelButton_ = CreateStyledButton(
        L"Annulla",
        margin + buttonWidth + 10, currentY, buttonWidth, 45, ID_CANCEL_BUTTON, false
    );
    ShowWindow(cancelButton_, SW_HIDE); // Initially hidden
}

HWND UIManager::CreateStyledButton(const wchar_t* text, int x, int y, int width, int height, int id, bool isAccent)
{
    HWND button = CreateWindowEx(
        0, L"BUTTON", text,
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        x, y, width, height,
        hwnd_, (HMENU)(UINT_PTR)id, hInstance_, nullptr
    );
    SendMessage(button, WM_SETFONT, (WPARAM)normalFont_, TRUE);
    return button;
}

HWND UIManager::CreateStyledEdit(const wchar_t* text, int x, int y, int width, int height, int id)
{
    HWND edit = CreateWindowEx(
        WS_EX_CLIENTEDGE, L"EDIT", text,
        WS_CHILD | WS_VISIBLE | ES_LEFT | ES_AUTOHSCROLL,
        x, y, width, height,
        hwnd_, (HMENU)(UINT_PTR)id, hInstance_, nullptr
    );
    return edit;
}

HWND UIManager::CreateStyledLabel(const wchar_t* text, int x, int y, int width, int height, int id)
{
    HWND label = CreateWindowEx(
        0, L"STATIC", text,
        WS_CHILD | WS_VISIBLE | SS_LEFT,
        x, y, width, height,
        hwnd_, (HMENU)(UINT_PTR)id, hInstance_, nullptr
    );
    SendMessage(label, WM_SETFONT, (WPARAM)normalFont_, TRUE);
    return label;
}

int UIManager::Run()
{
    MSG msg;
    while (GetMessage(&msg, nullptr, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    return (int)msg.wParam;
}

void UIManager::SetState(InstallState state)
{
    currentState_ = state;
    UpdateUI();
}

void UIManager::UpdateProgress(int progress, const std::wstring& status)
{
    SendMessage(progressBar_, PBM_SETPOS, progress, 0);
    SetWindowText(statusLabel_, status.c_str());
}

void UIManager::SetError(const std::wstring& errorMessage)
{
    errorMessage_ = errorMessage;
    currentState_ = InstallState::Error;
    UpdateUI();
}

void UIManager::UpdateUI()
{
    switch (currentState_) {
    case InstallState::Welcome:
        EnableWindow(installButton_, TRUE);
        EnableWindow(pathEdit_, TRUE);
        EnableWindow(browseButton_, TRUE);
        ShowWindow(cancelButton_, SW_HIDE);
        SetWindowText(statusLabel_, L"Pronto per l'installazione");
        break;

    case InstallState::CheckingDotNet:
    case InstallState::DownloadingDotNet:
    case InstallState::InstallingDotNet:
    case InstallState::DownloadingApp:
    case InstallState::ExtractingApp:
        EnableWindow(installButton_, FALSE);
        EnableWindow(pathEdit_, FALSE);
        EnableWindow(browseButton_, FALSE);
        ShowWindow(cancelButton_, SW_SHOW);
        EnableWindow(cancelButton_, TRUE);

        if (currentState_ == InstallState::CheckingDotNet)
            SetWindowText(statusLabel_, L"Controllo installazione .NET 10...");
        else if (currentState_ == InstallState::DownloadingDotNet)
            SetWindowText(statusLabel_, L"Download .NET 10 in corso...");
        else if (currentState_ == InstallState::InstallingDotNet)
            SetWindowText(statusLabel_, L"Installazione .NET 10 in corso...");
        else if (currentState_ == InstallState::DownloadingApp)
            SetWindowText(statusLabel_, L"Download InstAnalytics in corso...");
        else if (currentState_ == InstallState::ExtractingApp)
            SetWindowText(statusLabel_, L"Estrazione files in corso...");
        break;

    case InstallState::Completed:
        EnableWindow(installButton_, FALSE);
        ShowWindow(cancelButton_, SW_HIDE);
        SetWindowText(statusLabel_, L"Installazione completata con successo!");
        SetWindowText(installButton_, L"Chiudi");
        EnableWindow(installButton_, TRUE);
        SendMessage(progressBar_, PBM_SETPOS, 100, 0);
        break;

    case InstallState::Error:
        EnableWindow(installButton_, TRUE);
        ShowWindow(cancelButton_, SW_HIDE);
        SetWindowText(statusLabel_, (L"Errore: " + errorMessage_).c_str());
        SetWindowText(installButton_, L"Riprova");
        break;
    }

    InvalidateRect(hwnd_, nullptr, TRUE);
}

void UIManager::OnInstallButtonClick()
{
    if (currentState_ == InstallState::Completed) {
        PostQuitMessage(0);
        return;
    }

    if (currentState_ == InstallState::Error) {
        currentState_ = InstallState::Welcome;
        errorMessage_.clear();
        SendMessage(progressBar_, PBM_SETPOS, 0, 0);
        UpdateUI();
        return;
    }

    // Get installation path
    wchar_t buffer[MAX_PATH];
    GetWindowText(pathEdit_, buffer, MAX_PATH);
    installPath_ = buffer;

    // Call installation callback if set
    if (installCallback_) {
        installCallback_();
    }
}

void UIManager::OnBrowseButtonClick()
{
    BROWSEINFO bi = { };
    bi.hwndOwner = hwnd_;
    bi.lpszTitle = L"Seleziona la cartella di installazione";
    bi.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;

    LPITEMIDLIST pidl = SHBrowseForFolder(&bi);
    if (pidl != nullptr) {
        wchar_t path[MAX_PATH];
        if (SHGetPathFromIDList(pidl, path)) {
            installPath_ = path;
            installPath_ += L"\\InstAnalytics";
            SetWindowText(pathEdit_, installPath_.c_str());
        }
        CoTaskMemFree(pidl);
    }
}

void UIManager::OnCloseButtonClick()
{
    PostQuitMessage(0);
}

void UIManager::OnMinimizeButtonClick()
{
    ShowWindow(hwnd_, SW_MINIMIZE);
}

LRESULT CALLBACK UIManager::WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    UIManager* pThis = nullptr;

    if (uMsg == WM_NCCREATE) {
        CREATESTRUCT* pCreate = (CREATESTRUCT*)lParam;
        pThis = (UIManager*)pCreate->lpCreateParams;
        SetWindowLongPtr(hwnd, GWLP_USERDATA, (LONG_PTR)pThis);
        pThis->hwnd_ = hwnd;
    } else {
        pThis = (UIManager*)GetWindowLongPtr(hwnd, GWLP_USERDATA);
    }

    if (pThis) {
        return pThis->HandleMessage(uMsg, wParam, lParam);
    }

    return DefWindowProc(hwnd, uMsg, wParam, lParam);
}

LRESULT UIManager::HandleMessage(UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    switch (uMsg) {
    case WM_CLOSE:
        PostQuitMessage(0);
        return 0;

    case WM_LBUTTONDOWN:
        if (HIWORD(lParam) < WindowSize::TITLE_BAR_HEIGHT) {
            isDragging_ = true;
            SetCapture(hwnd_);
            GetCursorPos(&dragPoint_);
            RECT rect;
            GetWindowRect(hwnd_, &rect);
            dragPoint_.x -= rect.left;
            dragPoint_.y -= rect.top;
        }
        return 0;

    case WM_LBUTTONUP:
        if (isDragging_) {
            isDragging_ = false;
            ReleaseCapture();
        }
        return 0;

    case WM_MOUSEMOVE:
        if (isDragging_) {
            POINT cursor;
            GetCursorPos(&cursor);
            SetWindowPos(hwnd_, nullptr,
                cursor.x - dragPoint_.x,
                cursor.y - dragPoint_.y,
                0, 0, SWP_NOSIZE | SWP_NOZORDER);
        }
        return 0;

    case WM_COMMAND:
        switch (LOWORD(wParam)) {
        case ID_INSTALL_BUTTON:
            OnInstallButtonClick();
            return 0;
        case ID_BROWSE_BUTTON:
            OnBrowseButtonClick();
            return 0;
        case ID_CLOSE_BUTTON:
            OnCloseButtonClick();
            return 0;
        case ID_MINIMIZE_BUTTON:
            OnMinimizeButtonClick();
            return 0;
        }
        break;

    case WM_DRAWITEM: {
        DRAWITEMSTRUCT* pDIS = (DRAWITEMSTRUCT*)lParam;
        if (pDIS->CtlType == ODT_BUTTON) {
            HDC hdc = pDIS->hDC;
            RECT rect = pDIS->rcItem;

            // Determine button color
            HBRUSH brush;
            COLORREF textColor = Colors::TEXT_PRIMARY;

            if (pDIS->CtlID == ID_INSTALL_BUTTON) {
                brush = accentBrush_;
            } else if (pDIS->CtlID == ID_CLOSE_BUTTON) {
                brush = CreateSolidBrush(RGB(220, 50, 50));
            } else {
                brush = secondaryBrush_;
            }

            // Draw button background
            FillRect(hdc, &rect, brush);

            if (pDIS->CtlID == ID_CLOSE_BUTTON) {
                DeleteObject(brush);
            }

            // Draw button text
            wchar_t text[256];
            GetWindowText(pDIS->hwndItem, text, 256);

            SetBkMode(hdc, TRANSPARENT);
            SetTextColor(hdc, textColor);
            SelectObject(hdc, normalFont_);
            DrawText(hdc, text, -1, &rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

            return TRUE;
        }
        break;
    }

    case WM_CTLCOLORSTATIC: {
        HDC hdcStatic = (HDC)wParam;
        SetTextColor(hdcStatic, Colors::TEXT_PRIMARY);
        SetBkColor(hdcStatic, Colors::PRIMARY_BG);
        return (LRESULT)backgroundBrush_;
    }

    case WM_CTLCOLOREDIT: {
        HDC hdcEdit = (HDC)wParam;
        SetTextColor(hdcEdit, Colors::TEXT_PRIMARY);
        SetBkColor(hdcEdit, Colors::SECONDARY_BG);
        return (LRESULT)secondaryBrush_;
    }

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd_, &ps);
        PaintWindow(hdc);
        EndPaint(hwnd_, &ps);
        return 0;
    }
    }

    return DefWindowProc(hwnd_, uMsg, wParam, lParam);
}

void UIManager::PaintWindow(HDC hdc)
{
    RECT rect;
    GetClientRect(hwnd_, &rect);

    // Fill background
    FillRect(hdc, &rect, backgroundBrush_);

    // Draw title bar
    RECT titleBarRect = rect;
    titleBarRect.bottom = WindowSize::TITLE_BAR_HEIGHT;
    FillRect(hdc, &titleBarRect, secondaryBrush_);

    // Draw title text
    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, Colors::TEXT_PRIMARY);
    SelectObject(hdc, normalFont_);

    RECT titleTextRect = titleBarRect;
    titleTextRect.left += 15;
    DrawText(hdc, AppInfo::NAME.c_str(), -1, &titleTextRect, DT_LEFT | DT_VCENTER | DT_SINGLELINE);

    // Draw footer
    RECT footerRect = rect;
    footerRect.top = rect.bottom - 30;
    FillRect(hdc, &footerRect, secondaryBrush_);

    // Draw footer text
    SetTextColor(hdc, Colors::TEXT_SECONDARY);
    SelectObject(hdc, footerFont_);

    std::wstring footerText = L"v1.0.0 Designed by Fabio d'Agostino";
    DrawText(hdc, footerText.c_str(), -1, &footerRect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
}

} // namespace InstAnalyticsInstaller
