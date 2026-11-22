/*
 * WindowQuiet - a native helper that keeps one process's windows from taking the foreground.
 *
 * Why it exists: Unity creates and activates its window before the managed runtime (and so before
 * any BepInEx plugin) runs, so nothing managed can influence that first activation. The system does
 * call a CBT hook before a window is created and before it is activated; cancelling the activation is
 * not enough on its own (measured 2026-11-22: the window stopped taking the *foreground* but still
 * covered the screen, because a window can be raised without being activated). A global CBT hook needs
 * a native DLL, which is what this file is (ticket:
 * docs/backlog/todo/1.0-the-game-window-never-takes-the-front.md).
 *
 * Contract with the caller: QuietInstall(pid) after CreateProcess, QuietRemove() when the client is
 * gone. Only windows owned by one of those pids are touched; the hook lets every other CBT event
 * through untouched.
 */

#include <windows.h>

#define QUIET_MAX 8

static HHOOK s_hook = NULL;
static HHOOK s_callWndHook = NULL;
static DWORD s_pids[QUIET_MAX];
static int s_count = 0;
static HMODULE s_self = NULL;
static WCHAR s_image[MAX_PATH];
static int s_byImage = 0;

static int QuietOwns(DWORD owner)
{
    int i;
    if (owner == 0) {
        return 0;
    }
    for (i = 0; i < s_count; i++) {
        if (s_pids[i] == owner) {
            return 1;
        }
    }
    return 0;
}

/* A global hook runs inside the process whose event fired, and every injected copy has its own copy of these
   statics: configuration written by the launcher is not visible here. The image name is therefore compiled in,
   which also keeps the hook from touching any other process it is injected into. */
static int QuietHere(void)
{
    static const WCHAR quietImage[] = L"CasualtiesUnknown.exe";
    WCHAR path[MAX_PATH];
    DWORD length = GetModuleFileNameW(NULL, path, MAX_PATH);
    size_t imageLength = wcslen(quietImage);

    if (length > 0 && length < MAX_PATH && (size_t)length >= imageLength
        && _wcsicmp(path + ((size_t)length - imageLength), quietImage) == 0) {
        return 1;
    }

    return QuietOwns(GetCurrentProcessId());
}

/* Raising a window is not an activation: `ShowWindow(SW_SHOW)` and `SetWindowPos` move a window to the top
   without any CBT event, which is why the window still covered the screen while it never took the foreground
   (measured 2026-11-22: the window was visible, never activated, and still the top application window). Every
   z-order change arrives as `WM_WINDOWPOSCHANGING` first; rewriting that message to "go to the bottom" is the
   one place that catches all of them, before the move happens. */
static LRESULT CALLBACK QuietCallWndProc(int code, WPARAM wParam, LPARAM lParam)
{
    if (code == HC_ACTION) {
        CWPSTRUCT* message = (CWPSTRUCT*)lParam;
        if (message != NULL && message->message == WM_WINDOWPOSCHANGING && QuietHere()) {
            WINDOWPOS* position = (WINDOWPOS*)message->lParam;
            if (position != NULL) {
                position->hwndInsertAfter = HWND_BOTTOM;
                position->flags &= ~SWP_NOZORDER;
            }
        }
    }

    return CallNextHookEx(s_callWndHook, code, wParam, lParam);
}

static LRESULT CALLBACK QuietCbtProc(int code, WPARAM wParam, LPARAM lParam)
{
    if (code == HCBT_CREATEWND) {
        CREATESTRUCTW* create = (CREATESTRUCTW*)lParam;
        if (create != NULL && QuietHere()) {
            /* A new window is shown at the top of the z-order the moment it exists, which is the flash the
               person sees even when it is never activated. Creating it hidden and showing it later, below
               everything, is the only way to keep that first frame from covering the screen. */
            create->dwExStyle |= WS_EX_NOACTIVATE;
            create->style &= ~WS_VISIBLE;
        }
    } else if (code == HCBT_ACTIVATE) {
        if (QuietHere()) {
            /* The game wants to come forward: show it (in case it was created hidden) but place it below every
               other window and refuse the activation. Both happen before the activation completes, so the window
               appears without covering the person's work. */
            ShowWindow((HWND)wParam, SW_SHOWNOACTIVATE);
            SetWindowPos((HWND)wParam, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            return 1;
        }
    }

    return CallNextHookEx(s_hook, code, wParam, lParam);
}

/* Arm the hooks before any client exists: a global hook is injected into the processes that start after it is
   installed, so the client's very first window is seen. Matching the image name is what makes that possible. */
__declspec(dllexport) BOOL __stdcall QuietInstallImage(const WCHAR* image)
{
    if (image == NULL) {
        return FALSE;
    }

    wcsncpy_s(s_image, MAX_PATH, image, _TRUNCATE);
    s_byImage = 1;
    if (s_hook == NULL) {
        s_hook = SetWindowsHookExW(WH_CBT, QuietCbtProc, s_self, 0);
    }
    if (s_callWndHook == NULL) {
        s_callWndHook = SetWindowsHookExW(WH_CALLWNDPROC, QuietCallWndProc, s_self, 0);
    }
    return s_hook != NULL && s_callWndHook != NULL;
}

__declspec(dllexport) BOOL __stdcall QuietInstall(unsigned long pid)
{
    if (s_count >= QUIET_MAX) {
        return FALSE;
    }
    s_pids[s_count++] = (DWORD)pid;
    if (s_hook == NULL) {
        s_hook = SetWindowsHookExW(WH_CBT, QuietCbtProc, s_self, 0);
    }
    if (s_callWndHook == NULL) {
        s_callWndHook = SetWindowsHookExW(WH_CALLWNDPROC, QuietCallWndProc, s_self, 0);
    }
    return s_hook != NULL && s_callWndHook != NULL;
}

__declspec(dllexport) void __stdcall QuietRemove(void)
{
    if (s_hook != NULL) {
        UnhookWindowsHookEx(s_hook);
        s_hook = NULL;
    }
    if (s_callWndHook != NULL) {
        UnhookWindowsHookEx(s_callWndHook);
        s_callWndHook = NULL;
    }
    s_count = 0;
}

__declspec(dllexport) unsigned long __stdcall QuietLastError(void)
{
    return (unsigned long)GetLastError();
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        s_self = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}
