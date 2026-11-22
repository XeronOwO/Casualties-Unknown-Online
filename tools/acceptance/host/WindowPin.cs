using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Keeps the game's window at the bottom of the desktop and unable to take the foreground, from the moment the
/// window is created. A Unity window asks for the foreground as it is shown, and asking again on every frame beats a
/// bottom pin alone: the window sat at the top of the z-order for the whole startup until the extended style was
/// cleared (measured 2026-11-22).
///
/// This lives in its own process because the launcher exits once the endpoints answer — the startup is exactly the
/// window a person sees pop, so the pin has to outlive the thing that started the game.
/// </summary>
internal static class WindowPin
{
	public static void Run(TimeSpan duration)
	{
		var deadline = DateTime.UtcNow + duration;
		var started = DateTime.UtcNow;
		var seen = new Dictionary<IntPtr, DateTime>();
		while (DateTime.UtcNow < deadline)
		{
			foreach (var handle in GameWindows())
			{
				if (!seen.TryGetValue(handle, out var first))
				{
					first = DateTime.UtcNow;
					seen[handle] = first;
					var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
					SetWindowLongPtr(handle, GwlExStyle, (IntPtr)(style | WsExNoActivate));
				}

				if (DateTime.UtcNow - first < TimeSpan.FromSeconds(12))
				{
					SetWindowPos(handle, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
				}
			}

			// The window is created and raised by the game itself; a 100 ms loop loses that race during the first
			// seconds and the splash sits just under the top (measured 2026-11-22: index 1 for the first 2.5 s), so
			// the first seconds are chased at 10 ms and the steady state afterwards at 100 ms.
			Thread.Sleep(DateTime.UtcNow - started < TimeSpan.FromSeconds(10) ? 10 : 100);
		}
	}

	public static Process? StartDetached(TimeSpan duration)
	{
		var exe = Environment.ProcessPath;
		if (string.IsNullOrEmpty(exe) || !exe.EndsWith("CasualtiesUnknownOnline.AcceptanceHost.exe", StringComparison.OrdinalIgnoreCase))
		{
			// Started through `dotnet run`: the command line is the host dll, which takes the same arguments.
			exe = Environment.ProcessPath;
		}

		if (string.IsNullOrEmpty(exe))
		{
			return null;
		}

		return Process.Start(new ProcessStartInfo(exe)
		{
			Arguments = $"pin --seconds {(int)duration.TotalSeconds}",
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		});
	}

	private static IEnumerable<IntPtr> GameWindows()
	{
		var found = new List<IntPtr>();
		var gameProcesses = new HashSet<uint>();
		foreach (var process in Process.GetProcessesByName("CasualtiesUnknown"))
		{
			using (process)
			{
				gameProcesses.Add((uint)process.Id);
			}
		}

		EnumWindows((handle, _) =>
		{
			if (!IsWindowVisible(handle))
			{
				return true;
			}

			GetWindowThreadProcessId(handle, out var owner);
			var title = new StringBuilder(256);
			GetWindowText(handle, title, title.Capacity);
			var named = title.Length > 0 && title.ToString().Contains("CasualtiesUnknown", StringComparison.OrdinalIgnoreCase);
			// The class name is not used: every Unity game names its window `UnityWndClass`, and pinning another
			// game's window would be worse than the pop this exists to prevent (measured 2026-11-22: the first
			// version matched by class and would have put an unrelated Unity game behind the desktop).
			if (named || gameProcesses.Contains(owner))
			{
				found.Add(handle);
			}

			return true;
		}, IntPtr.Zero);
		return found;
	}

	public static Task StartBackground(TimeSpan duration) => Task.Run(() => Run(duration));

	private const IntPtr HwndBottom = 1;
	private const uint SwpNoSize = 0x0001;
	private const uint SwpNoMove = 0x0002;
	private const uint SwpNoActivate = 0x0010;
	private const int GwlExStyle = -20;
	private const long WsExNoActivate = 0x08000000;

	private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr handle);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
	private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
	private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);
}
