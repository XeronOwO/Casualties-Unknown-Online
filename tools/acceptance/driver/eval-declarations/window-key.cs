// eval declaration: window-key
// declares: AcceptanceWindowKey
// serves: container-move-snapshot-only-sync row A1g - the game's own container-expansion branch is
//         gated by a HELD key (PlayerCamera.TryPerformInventoryAction reads
//         Input.GetKey(KeyBinds.GetBind("expanddesc"))), and this is how an acceptance run holds one.
//
// The ONE file under tools/acceptance/ that may name a window-message or key-state API, and the ban in
// AcceptanceDriverGateTests.TheAcceptanceToolsNeverUseOsLevelInput is what keeps it the only one: a run
// drives a client from inside its own process, never the machine's keyboard, mouse, cursor or focus.
// What this file adds is the narrowest exception that reaches the game's own gate - a message queued on
// the client's OWN window (Process.GetCurrentProcess().MainWindowHandle), which that client's own
// message pump turns into the input state the game reads:
//   - the OS input queue is never touched (the OS key state reads 0 while the key reads held),
//   - no other window is addressed and no window is activated (the foreground window is read, never set),
//   - only a key the game itself has bound is queued, and the recipe reads the state back per call.
// The gate pins that: the four raw APIs below are PRIVATE to this class and are the only names of their
// kind anywhere under tools/acceptance/ - the surface the recipes call is this class's own vocabulary
// (QueueKeyDown, QueueKeyUp, ScanCodeOf, OsKeyState, ForegroundWindow). Of those four, the window message
// and the key-state read are the two the OS-input ban itself lists, and they are excused in this one
// directory only; the scan-code lookup and the foreground read are queries the ban never covered, because
// neither can drive anything. A future need for an actuation, focus-setting, hook, clipboard or cursor
// API is a rule question, not a new line in this file.
//
// The evaluator compiles one input at a time and refuses a declaration carrying a trailing expression,
// so a declaration is a file of its own: `drive-in-process.ps1 -Action declare -Declare window-key`
// sends this one once per client, and tools/acceptance/recipes/key-hold.cs then uses the type by name.
using System;
using System.Runtime.InteropServices;

public static class AcceptanceWindowKey
{
	/// <summary>WM_KEYDOWN - the message a real press delivers to this window class.</summary>
	private const uint KeyDownMessage = 0x0100;

	/// <summary>WM_KEYUP - the message a real release delivers to this window class.</summary>
	private const uint KeyUpMessage = 0x0101;

	/// <summary>
	/// Queues one key-down on <paramref name="window"/>'s own message queue and returns whether the queue
	/// accepted it. Asynchronous on purpose: the client's message pump handles it exactly like the message
	/// a physical key would deliver, and this process never blocks waiting for that pump. The `lParam`
	/// carries the scan code with a repeat count of one, which is the shape a first press has.
	/// </summary>
	public static bool QueueKeyDown(IntPtr window, int virtualKey, int scanCode)
	{
		return PostMessage(window, KeyDownMessage, new IntPtr((long)virtualKey), new IntPtr(1L | ((long)scanCode << 16)));
	}

	/// <summary>
	/// Queues one key-up on <paramref name="window"/>'s own message queue. Its `lParam` carries the scan
	/// code plus the previous-state and transition bits (`0xC0000000`), which is what makes the message a
	/// release rather than a repeated press - a release without them left the key held in the client that
	/// batch `20261006-a` measured.
	/// </summary>
	public static bool QueueKeyUp(IntPtr window, int virtualKey, int scanCode)
	{
		return PostMessage(window, KeyUpMessage, new IntPtr((long)virtualKey), new IntPtr(0xC0000000L | ((long)scanCode << 16) | 1L));
	}

	/// <summary>
	/// The scan code the client's own keyboard layout gives a virtual key (`MAPVK_VK_TO_VSC`), which is
	/// what the messages above carry. The layout answers for the left/right-specific codes as well as the
	/// generic ones, and those answers are not interchangeable (`VK_LSHIFT` is 0x2A and `VK_RSHIFT` is
	/// 0x36), so the caller passes the key the bind resolved to.
	/// </summary>
	public static int ScanCodeOf(int virtualKey)
	{
		return (int)MapVirtualKey((uint)virtualKey, 0u);
	}

	/// <summary>
	/// The OS-level key state - read only, and always 0 for a key this class queued. It is the evidence
	/// that the hold lives inside the client rather than in the machine's input queue.
	/// </summary>
	public static int OsKeyState(int virtualKey)
	{
		return (int)GetAsyncKeyState(virtualKey);
	}

	/// <summary>
	/// The window the OS currently has in the foreground - read only. No call in this file ever sets it,
	/// so a run can show that the client it drove was never brought forward, and therefore that nothing
	/// it did could reach another application.
	/// </summary>
	public static IntPtr ForegroundWindow()
	{
		return GetForegroundWindow();
	}

	[DllImport("user32.dll")]
	private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern uint MapVirtualKey(uint virtualKey, uint mapType);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int virtualKey);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();
}
