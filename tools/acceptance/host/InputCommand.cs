using System.Runtime.InteropServices;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Sends one real mouse/keyboard input to a window (a port of <c>tools/acceptance/send-input.ps1</c>).
/// The user authorised simulated input for the acceptance tooling on 2026-10-24; coordinates are CLIENT
/// coordinates of the target window, and the window is brought forward first because Unity reads input
/// from the focused window only.
/// </summary>
internal static class InputCommand
{
	private static readonly string[] Actions = ["click", "key", "drag", "rdrag", "move", "rpress", "rrelease"];

	public static int Run(string[] args)
	{
		var options = new Arguments(
			args,
			"input --action click|key|drag|rdrag|move|rpress|rrelease [--process <name>] [--pid <id>] [--x] [--y] [--x1] [--y1] [--x2] [--y2] [--vk] [--hold-ms] [--down] [--up]");
		var action = options.Required("action").ToLowerInvariant();
		if (!Actions.Contains(action, StringComparer.Ordinal))
		{
			throw new CommandFailure(64, $"--action must be one of {string.Join("|", Actions)}; usage: {options.Usage}");
		}

		var processName = options.Optional("process") ?? "CasualtiesUnknown";
		var processId = options.OptionalNumber("pid", 0);
		var x = options.OptionalNumber("x", 0);
		var y = options.OptionalNumber("y", 0);
		var x1 = options.OptionalNumber("x1", 0);
		var y1 = options.OptionalNumber("y1", 0);
		var x2 = options.OptionalNumber("x2", 0);
		var y2 = options.OptionalNumber("y2", 0);
		var key = options.OptionalNumber("vk", 0);
		var holdMs = options.OptionalNumber("hold-ms", 400);
		var down = options.Optional("down") is not null;
		var up = options.Optional("up") is not null;

		NativeInput.SetProcessDPIAware();

		var target = processId > 0
			? System.Diagnostics.Process.GetProcesses().FirstOrDefault(process => process.Id == processId)
			: System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault(process => process.MainWindowHandle != 0);
		if (target is null || target.MainWindowHandle == 0)
		{
			throw new CommandFailure(1, $"no window found for '{processName}'{(processId > 0 ? $" pid {processId}" : string.Empty)} (is the client running and past the launcher?)");
		}

		var handle = target.MainWindowHandle;
		if (!NativeInput.ForceForeground(handle))
		{
			throw new CommandFailure(1, $"could not bring '{processName}' to the foreground (the click/key would go to another window)");
		}

		Thread.Sleep(250);

		switch (action)
		{
			case "click":
				var clicked = ToScreen(handle, x, y);
				NativeInput.Click(clicked.X, clicked.Y);
				break;
			case "key":
				if (key <= 0)
				{
					throw new CommandFailure(64, "--vk is required for --action key");
				}

				if (down || up)
				{
					if (down)
					{
						NativeInput.KeyDown((ushort)key);
					}

					if (up)
					{
						NativeInput.KeyUp((ushort)key);
					}
				}
				else
				{
					NativeInput.Key((ushort)key);
				}

				break;
			case "drag":
				Drag(handle, x1, y1, x2, y2, holdMs, rightButton: false);
				break;
			case "rdrag":
				Drag(handle, x1, y1, x2, y2, holdMs, rightButton: true);
				break;
			case "move":
				var moved = ToScreen(handle, x, y);
				NativeInput.SetCursorPos(moved.X, moved.Y);
				break;
			case "rpress":
				if (x == 0 && y == 0)
				{
					throw new CommandFailure(64, "rpress needs --x and --y (the item it presses on)");
				}

				var press = ToScreen(handle, x, y);
				NativeInput.MouseDownRight(press.X, press.Y);
				break;
			case "rrelease":
				var release = ToScreen(handle, x, y);
				NativeInput.MouseUpRight(release.X, release.Y);
				break;
		}

		var sent = JsonSerializer.Serialize(new
		{
			ok = true,
			window = target.ProcessName,
			handle = handle.ToString(),
			action,
			at = DateTime.Now.ToString("HH:mm:ss"),
		});
		Console.WriteLine(sent);
		return 0;
	}

	private static void Drag(IntPtr handle, int fromX, int fromY, int toX, int toY, int holdMs, bool rightButton)
	{
		var from = ToScreen(handle, fromX, fromY);
		var to = ToScreen(handle, toX, toY);
		if (rightButton)
		{
			NativeInput.MouseDownRight(from.X, from.Y);
		}
		else
		{
			NativeInput.MouseDown(from.X, from.Y);
		}

		var steps = Math.Max(2, holdMs / 40);
		for (var step = 1; step <= steps; step++)
		{
			var screenX = from.X + (to.X - from.X) * step / steps;
			var screenY = from.Y + (to.Y - from.Y) * step / steps;
			NativeInput.SetCursorPos(screenX, screenY);
			Thread.Sleep(40);
		}

		if (rightButton)
		{
			NativeInput.MouseUpRight(to.X, to.Y);
		}
		else
		{
			NativeInput.MouseUp(to.X, to.Y);
		}
	}

	private static NativeInput.Point ToScreen(IntPtr handle, int clientX, int clientY)
	{
		var point = new NativeInput.Point { X = clientX, Y = clientY };
		if (!NativeInput.ClientToScreen(handle, ref point))
		{
			throw new CommandFailure(1, $"ClientToScreen failed for ({clientX},{clientY})");
		}

		return point;
	}

	/// <summary>The Win32 input surface the script embedded; names and comments follow it.</summary>
	private static class NativeInput
	{
		public const uint MouseEventLeftDown = 0x0002;
		public const uint MouseEventLeftUp = 0x0004;
		public const uint MouseEventRightDown = 0x0008;
		public const uint MouseEventRightUp = 0x0010;
		public const uint KeyEventKeyUp = 0x0002;
		private const uint InputMouse = 0;
		private const uint InputKeyboard = 1;

		[StructLayout(LayoutKind.Sequential)]
		public struct Point
		{
			public int X;
			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MouseInput
		{
			public int Dx;
			public int Dy;
			public uint MouseData;
			public uint Flags;
			public uint Time;
			public IntPtr ExtraInfo;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct KeyboardInput
		{
			public ushort VirtualKey;
			public ushort Scan;
			public uint Flags;
			public uint Time;
			public IntPtr ExtraInfo;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct InputUnion
		{
			[FieldOffset(0)]
			public MouseInput Mouse;

			[FieldOffset(0)]
			public KeyboardInput Keyboard;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct Input
		{
			public uint Type;
			public InputUnion Union;
		}

		[DllImport("user32.dll")]
		public static extern bool SetProcessDPIAware();

		[DllImport("user32.dll")]
		public static extern bool SetForegroundWindow(IntPtr window);

		[DllImport("user32.dll")]
		public static extern bool BringWindowToTop(IntPtr window);

		[DllImport("user32.dll")]
		public static extern bool ShowWindow(IntPtr window, int command);

		[DllImport("user32.dll")]
		public static extern void SwitchToThisWindow(IntPtr window, bool altTab);

		[DllImport("user32.dll")]
		public static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		public static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

		[DllImport("kernel32.dll")]
		public static extern uint GetCurrentThreadId();

		[DllImport("user32.dll")]
		public static extern bool AttachThreadInput(uint attach, uint attachTo, bool toggle);

		[DllImport("user32.dll")]
		public static extern bool ClientToScreen(IntPtr window, ref Point point);

		[DllImport("user32.dll")]
		public static extern bool SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		private static extern uint SendInput(uint count, Input[] inputs, int size);

		/// <summary>
		/// Bring a window forward for real: a background process may not call SetForegroundWindow alone, so
		/// this borrows the foreground thread's input state, sets the window, and lets go.
		/// </summary>
		public static bool ForceForeground(IntPtr window)
		{
			if (GetForegroundWindow() == window)
			{
				return true;
			}

			var foreground = GetForegroundWindow();
			var targetThread = GetWindowThreadProcessId(window, IntPtr.Zero);
			var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, IntPtr.Zero);
			var currentThread = GetCurrentThreadId();

			var attachedForeground = false;
			var attachedTarget = false;
			try
			{
				if (foregroundThread != 0 && foregroundThread != currentThread)
				{
					attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
				}

				if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
				{
					attachedTarget = AttachThreadInput(currentThread, targetThread, true);
				}

				BringWindowToTop(window);
				ShowWindow(window, 9);
				SetForegroundWindow(window);
			}
			finally
			{
				if (attachedTarget)
				{
					AttachThreadInput(currentThread, targetThread, false);
				}

				if (attachedForeground)
				{
					AttachThreadInput(currentThread, foregroundThread, false);
				}
			}

			if (GetForegroundWindow() == window)
			{
				return true;
			}

			SwitchToThisWindow(window, true);
			Thread.Sleep(150);
			return GetForegroundWindow() == window;
		}

		public static void Click(int screenX, int screenY) => SendMouse(screenX, screenY, MouseEventLeftDown | MouseEventLeftUp, twoInputs: true);

		public static void MouseDown(int screenX, int screenY) => SendMouse(screenX, screenY, MouseEventLeftDown, twoInputs: false);

		public static void MouseUp(int screenX, int screenY) => SendMouse(screenX, screenY, MouseEventLeftUp, twoInputs: false);

		public static void MouseDownRight(int screenX, int screenY) => SendMouse(screenX, screenY, MouseEventRightDown, twoInputs: false);

		public static void MouseUpRight(int screenX, int screenY) => SendMouse(screenX, screenY, MouseEventRightUp, twoInputs: false);

		public static void Key(ushort virtualKey)
		{
			var inputs = new Input[2];
			inputs[0].Type = InputKeyboard;
			inputs[0].Union.Keyboard.VirtualKey = virtualKey;
			inputs[1].Type = InputKeyboard;
			inputs[1].Union.Keyboard.VirtualKey = virtualKey;
			inputs[1].Union.Keyboard.Flags = KeyEventKeyUp;
			SendInput(2, inputs, Marshal.SizeOf(typeof(Input)));
		}

		public static void KeyDown(ushort virtualKey)
		{
			var inputs = new Input[1];
			inputs[0].Type = InputKeyboard;
			inputs[0].Union.Keyboard.VirtualKey = virtualKey;
			SendInput(1, inputs, Marshal.SizeOf(typeof(Input)));
		}

		public static void KeyUp(ushort virtualKey)
		{
			var inputs = new Input[1];
			inputs[0].Type = InputKeyboard;
			inputs[0].Union.Keyboard.VirtualKey = virtualKey;
			inputs[0].Union.Keyboard.Flags = KeyEventKeyUp;
			SendInput(1, inputs, Marshal.SizeOf(typeof(Input)));
		}

		private static void SendMouse(int screenX, int screenY, uint flags, bool twoInputs)
		{
			SetCursorPos(screenX, screenY);
			Thread.Sleep(80);
			if (twoInputs)
			{
				var pair = new Input[2];
				pair[0].Type = InputMouse;
				pair[0].Union.Mouse.Flags = MouseEventLeftDown;
				pair[1].Type = InputMouse;
				pair[1].Union.Mouse.Flags = MouseEventLeftUp;
				SendInput(2, pair, Marshal.SizeOf(typeof(Input)));
				return;
			}

			var single = new Input[1];
			single[0].Type = InputMouse;
			single[0].Union.Mouse.Flags = flags;
			SendInput(1, single, Marshal.SizeOf(typeof(Input)));
		}
	}
}
