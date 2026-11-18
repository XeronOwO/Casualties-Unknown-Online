using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Captures one client window to a PNG for a row judged from a frame (a port of
/// <c>tools/acceptance/capture-window.ps1</c>): PrintWindow with PW_RENDERFULLCONTENT renders a D3D
/// window without activating it, so the client never has to be in front.
/// </summary>
internal static class WindowCaptureCommand
{
	private const uint RenderFullContent = 0x00000002;

	public static int Run(string[] args)
	{
		var options = new Arguments(args, "capture --out <png> [--process <name>] [--pid <id>] [--attempts <n>] [--delay-ms <n>]");
		var output = options.Required("out");
		var processName = options.Optional("process") ?? "CasualtiesUnknown";
		var processId = options.OptionalNumber("pid", 0);
		var attempts = options.OptionalNumber("attempts", 3);
		var delayMs = options.OptionalNumber("delay-ms", 400);

		NativeMethods.SetProcessDPIAware();

		var target = processId > 0
			? System.Diagnostics.Process.GetProcesses().FirstOrDefault(process => process.Id == processId)
			: System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault(process => process.MainWindowHandle != 0);
		if (target is null)
		{
			throw new CommandFailure(1, processId > 0 ? $"No process with id {processId}." : $"No window found for process '{processName}'.");
		}

		var handle = target.MainWindowHandle;
		if (handle == IntPtr.Zero)
		{
			throw new CommandFailure(1, $"The window handle of '{target.ProcessName}' (id {target.Id}) is zero; it has no window to capture.");
		}

		if (!NativeMethods.IsWindowVisible(handle))
		{
			throw new CommandFailure(1, $"The window of '{processName}' is not visible; a hidden client cannot be captured.");
		}

		var directory = Path.GetDirectoryName(output);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var detail = string.Empty;
		for (var attempt = 1; attempt <= attempts; attempt++)
		{
			if (Capture(handle, output, out detail))
			{
				var length = new FileInfo(output).Length;
				Console.WriteLine($"captured {output} ({detail}, {length} bytes) from pid {target.Id}");
				return 0;
			}

			if (attempt < attempts)
			{
				Thread.Sleep(delayMs);
			}
		}

		throw new CommandFailure(1, $"PrintWindow failed {attempts} time(s) for '{processName}': {detail} (is the client minimised?).");
	}

	private static bool Capture(IntPtr handle, string path, out string detail)
	{
		detail = string.Empty;
		if (!NativeMethods.GetWindowRect(handle, out var rect))
		{
			detail = "GetWindowRect failed";
			return false;
		}

		var width = rect.Right - rect.Left;
		var height = rect.Bottom - rect.Top;
		if (width <= 0 || height <= 0)
		{
			detail = $"zero-size window ({width}x{height})";
			return false;
		}

		using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		using (var graphics = Graphics.FromImage(bitmap))
		{
			var deviceContext = graphics.GetHdc();
			bool rendered;
			try
			{
				rendered = NativeMethods.PrintWindow(handle, deviceContext, RenderFullContent);
			}
			finally
			{
				graphics.ReleaseHdc(deviceContext);
			}

			if (!rendered)
			{
				detail = "PrintWindow returned false";
				return false;
			}
		}

		bitmap.Save(path, ImageFormat.Png);
		detail = $"{width}x{height}";
		return true;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	private static partial class NativeMethods
	{
		[DllImport("user32.dll")]
		public static extern bool SetProcessDPIAware();

		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr window, out Rect rect);

		[DllImport("user32.dll")]
		public static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);

		[DllImport("user32.dll")]
		public static extern bool IsWindowVisible(IntPtr window);
	}
}
