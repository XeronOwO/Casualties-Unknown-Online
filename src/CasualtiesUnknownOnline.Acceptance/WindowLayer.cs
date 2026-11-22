using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// Keeps this client's own window behind the window the person is using, from the moment the game creates it, and does
/// nothing else to it: it stays visible, focusable and clickable.
///
/// <para>
/// A batch starts two clients and the one that comes up last raises itself over whatever is on screen. The fix belongs
/// in the client, not in the launcher: a case restarts a client minutes into a run, long after anything the launcher
/// started has finished, and that restart pops exactly the same way (measured 2026-11-22). The window is only pushed to
/// the bottom of the z-order — the earlier launcher-side version also cleared the window's activation style, which made
/// it feel hidden when it was clicked, and that was not what was asked for.
/// </para>
/// </summary>
internal static class WindowLayer
{
	public static void SendBehindOnStartup(TimeSpan duration)
	{
		_ = Task.Run(() =>
		{
			var deadline = DateTime.UtcNow + duration;
			var pid = Process.GetCurrentProcess().Id;
			while (DateTime.UtcNow < deadline)
			{
				var handle = OwnWindow(pid);
				if (handle != IntPtr.Zero)
				{
					SetWindowPos(handle, HwndBottom, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
				}

				Thread.Sleep(50);
			}
		});
	}

	private static IntPtr OwnWindow(int pid)
	{
		var found = IntPtr.Zero;
		EnumWindows((handle, _) =>
		{
			GetWindowThreadProcessId(handle, out var owner);
			if (owner != (uint)pid || !IsWindowVisible(handle))
			{
				return true;
			}

			found = handle;
			return false;
		}, IntPtr.Zero);
		return found;
	}

	private static readonly IntPtr HwndBottom = new(1);
	private const uint SwpNoSize = 0x0001;
	private const uint SwpNoMove = 0x0002;
	private const uint SwpNoActivate = 0x0010;

	private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr handle);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
}
