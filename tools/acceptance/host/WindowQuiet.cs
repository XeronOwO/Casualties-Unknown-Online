using System.Runtime.InteropServices;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Keeps a started client's windows from taking the foreground, including Unity's first white frame.
/// </summary>
/// <remarks>
/// The game activates its own window a moment after `CreateProcess` returns, and it does so before any managed code
/// inside it runs - so neither the startup information we hand to `CreateProcess` nor a plugin can stop that first
/// activation. The system does ask a CBT hook before activating a window, and a nonzero answer cancels it; a global CBT
/// hook needs a native library, which is `tools/native/window-quiet` (see the ticket
/// `docs/backlog/todo/1.0-the-game-window-never-takes-the-front.md`). Everything here degrades to a single log line: a
/// missing library must not stop a run.
/// </remarks>
internal static class WindowQuiet
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int QuietInstallDelegate(uint processId);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate void QuietRemoveDelegate();

	private static QuietInstallDelegate? _install;
	private static QuietRemoveDelegate? _remove;
	private static bool _looked;

	private static void Load()
	{
		if (_looked)
		{
			return;
		}

		_looked = true;
		var path = FindLibrary();
		if (path is null)
		{
			Console.Error.WriteLine("[window-quiet] the native helper is not built; the clients may take the foreground.");
			return;
		}

		try
		{
			var library = NativeLibrary.Load(path);
			_install = Marshal.GetDelegateForFunctionPointer<QuietInstallDelegate>(NativeLibrary.GetExport(library, "QuietInstall"));
			_remove = Marshal.GetDelegateForFunctionPointer<QuietRemoveDelegate>(NativeLibrary.GetExport(library, "QuietRemove"));
		}
		catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
		{
			Console.Error.WriteLine($"[window-quiet] {Path.GetFileName(path)} could not be loaded: {error.Message}");
		}
	}

	private static string? FindLibrary()
	{
		var beside = Path.Combine(AppContext.BaseDirectory, "window-quiet.dll");
		if (File.Exists(beside))
		{
			return beside;
		}

		// `dotnet run` keeps the build output under `tools/acceptance/host/bin/...`; walk up to the repository root and
		// take the script's output, so a run from the working tree still finds a freshly built helper.
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName, "artifacts", "native", "window-quiet.dll");
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	/// <summary>Asks the helper to cancel activation of every window the given process creates.</summary>
	internal static void Install(int processId)
	{
		Load();
		if (_install is null)
		{
			return;
		}

		if (_install((uint)processId) == 0)
		{
			Console.Error.WriteLine($"[window-quiet] the hook could not be installed for process {processId}.");
		}
	}

	/// <summary>Drops the hook; the launcher calls this on shutdown.</summary>
	internal static void RemoveAll() => _remove?.Invoke();
}
