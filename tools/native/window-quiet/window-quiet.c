/*
 * WindowQuiet - a native helper that keeps one process's windows from taking the foreground.
 *
 * Why it exists: Unity creates and activates its window before the managed runtime (and so before
 * any BepInEx plugin) runs, so nothing managed can influence that first activation. The system does
 * call a CBT hook before a window is activated, and a nonzero answer cancels the activation; a global
 * CBT hook needs a native DLL, which is what this file is (ticket:
 * docs/backlog/todo/1.0-the-game-window-never-takes-the-front.md).
 *
 * Contract with the caller: QuietInstall(pid) after CreateProcess, QuietRemove() when the client is
 * gone. Only windows owned by that pid are touched; the hook answers HCBT_ACTIVATE with 1 and lets
 * every other CBT event through.
 */

#include <windows.h>

#define QUIET_MAX 8

static HHOOK s_hook = NULL;
static DWORD s_pids[QUIET_MAX];
static int s_count = 0;
static HMODULE s_self = NULL;

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

static LRESULT CALLBACK QuietCbtProc(int code, WPARAM wParam, LPARAM lParam)
{
    if (code == HCBT_ACTIVATE) {
        DWORD owner = 0;
        GetWindowThreadProcessId((HWND)wParam, &owner);
        if (QuietOwns(owner)) {
            return 1;
        }
    }
    return CallNextHookEx(s_hook, code, wParam, lParam);
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
    return s_hook != NULL;
}

__declspec(dllexport) void __stdcall QuietRemove(void)
{
    if (s_hook != NULL) {
        UnhookWindowsHookEx(s_hook);
        s_hook = NULL;
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
