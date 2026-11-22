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

	[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	private delegate int QuietInstallImageDelegate(string image);

	private static QuietInstallDelegate? _install;
	private static QuietInstallImageDelegate? _installImage;
	private static QuietRemoveDelegate? _remove;
	private static bool _looked;

	/// <summary>
	/// Arms the hooks before any client exists, matching the game's image name.
	/// </summary>
	/// <remarks>
	/// A global hook is injected into the processes that start after it is installed, so arming it first is what makes
	/// the client's very first window visible to it. Installed after `CreateProcess`, the same hook arrived a moment too
	/// late and the white startup window had already been created and raised (measured 2026-11-22).
	/// </remarks>
	internal static void InstallForImage(string image)
	{
		Load();
		if (_installImage is null)
		{
			return;
		}

		if (_installImage(image) == 0)
		{
			Console.Error.WriteLine($"[window-quiet] the hooks could not be armed for '{image}'.");
		}
	}

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
			_installImage = Marshal.GetDelegateForFunctionPointer<QuietInstallImageDelegate>(NativeLibrary.GetExport(library, "QuietInstallImage"));
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
