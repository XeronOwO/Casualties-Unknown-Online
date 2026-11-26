using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Starts, reuses and stops the acceptance clients. Every machine value is passed in or read from the
/// facts file; a value found in neither place is a named error, never a guess.
///
/// One client that already answers is REUSED, never started again — the game refuses a second
/// instance, but the run must not depend on that. A box keeps its own copy of the plug-in, so the
/// desktop build is synced into the box before its process starts; a box running an older build makes
/// every reading false.
/// </summary>
internal sealed class ClientLauncher(ClientLauncher.LauncherOptions options)
{
	private static readonly string[] GameProcessName = ["CasualtiesUnknown"];

	private readonly LauncherOptions _options = options;
	private readonly List<(string Name, string Url, bool Boxed, string Root, string? Box)> _clients = [];
	private readonly List<Process> _started = [];
	private readonly StreamWriter? _logFile = OpenLog(options.OutDir);

	public IReadOnlyList<(string Name, string Url, bool Boxed, string Root, string? Box)> Clients => _clients;

	/// <summary>Everything the launcher needs, already resolved from parameters or the facts file.</summary>
	internal sealed record LauncherOptions(
		string GameDir,
		string SteamExe,
		string SandboxieExe,
		string GuestBox,
		string GuestRoot,
		string? HostBox,
		string AltBox,
		string AltRoot,
		string HostUrl,
		string GuestUrl,
		string AltUrl,
		string OutDir,
		bool All,
		bool Restart,
		string? Only,
		string? SaveRoot,
		string? PidsPath,
		int SteamGraceSeconds,
		int ReadyTimeoutSeconds);

	public bool Launch()
	{
		// Arm the window hooks before any client process exists: a global hook is injected into the processes that start
		// after it is installed, and only that gets the very first window (measured 2026-11-22: armed after
		// `CreateProcess`, the white startup window was already created and raised).
		WindowQuiet.InstallForImage("CasualtiesUnknown.exe");
		BuildClientList();
		var needsSandbox = _clients.Any(candidate => candidate.Boxed);
		if (needsSandbox && !EnsureSandboxService())
		{
			Log("FAIL: the sandbox cannot start, so no boxed client can come up");
			return false;
		}

		if (_options.Restart)
		{
			RestartNamed();
		}

		ApplySaveRoot();

		foreach (var client in _clients)
		{
			StartClient(client);
		}

		var ready = Endpoints.WaitAll([.. _clients.Select(c => (c.Name, c.Url))], _options.ReadyTimeoutSeconds, Log);
		Log($"endpoints ready={ready}");

		if (!ready)
		{
			var restarted = 0;
			foreach (var client in _clients.Where(c => c.Boxed && !Endpoints.IsOpen(c.Url)))
			{
				if (BoxGameProcesses(client.Box!).Count > 0)
				{
					continue;
				}

				RestartBoxGame(client);
				restarted++;
			}

			if (restarted == 0)
			{
				Log("no boxed game died; not waiting a second round");
			}
			else
			{
				ready = Endpoints.WaitAll([.. _clients.Select(c => (c.Name, c.Url))], _options.ReadyTimeoutSeconds, Log);
				Log($"endpoints after the box restart: ready={ready}");
			}
		}

		var missing = _clients.Where(c => !Endpoints.IsOpen(c.Url)).ToList();
		if (missing.Count > 0)
		{
			foreach (var client in missing)
			{
				Log($"FAIL: {client.Name} endpoint never came up");
			}

			return false;
		}

		Log(string.Join(", ", _clients.Select(c => c.Name)) + " up (endpoints answered)");
		return true;
	}

	private void BuildClientList()
	{
		// The host's root is the game install even when it runs inside a box: Sandboxie is given the real path and
		// redirects the writes into the box. Passing the box *name* here built `Steam2\CasualtiesUnknown.exe` and
		// started the wrong instance (measured 2026-11-26 23:00).
		var host = ("host", _options.HostUrl, !string.IsNullOrWhiteSpace(_options.HostBox), _options.GameDir, _options.HostBox);
		var guest = ("guest", _options.GuestUrl, true, _options.GuestRoot, _options.GuestBox);
		var alt = ("alt", _options.AltUrl, true, _options.AltRoot, _options.AltBox);

		if (!string.IsNullOrWhiteSpace(_options.Only))
		{
			_clients.Add(_options.Only switch
			{
				"host" => host,
				"guest" => guest,
				_ => alt
			});
			return;
		}

		if (_options.All)
		{
			_clients.AddRange([host, guest, alt]);
			return;
		}

		_clients.AddRange([host, guest]);
	}

	private void StartClient((string Name, string Url, bool Boxed, string Root, string? Box) client)
	{
		var noReuse = _options.Restart;
		if (!noReuse && Endpoints.IsOpen(client.Url))
		{
			Log($"{client.Name} already running (reused)");
			return;
		}

		if (client.Boxed)
		{
			StartBoxGame(client);
			return;
		}

		var hostExe = Path.Combine(_options.GameDir, "CasualtiesUnknown.exe");
		var existingHost = HostCandidates(hostExe).FirstOrDefault();
		if (!noReuse && existingHost is not null)
		{
			// A process that has existed longer than a launch can take but still has no endpoint is
			// a leftover, not a client coming up (measured 2026-11-20: the A3 relaunch left one and
			// every later run sat on the 300 s endpoint budget). The endpoint is closed here, so the
			// "never restart a client that answers" rule does not cover this process.
			if (HostAge(existingHost) < TimeSpan.FromSeconds(30))
			{
				Log("host process exists; letting it come up");
				return;
			}

			Log($"host process {existingHost.Id} is stale ({HostAge(existingHost).TotalSeconds:F0} s old, no endpoint); stopping it");
			StopQuietly(existingHost.Id);
			WaitUntil(() => !Endpoints.IsOpen(client.Url), TimeSpan.FromSeconds(10));
		}

		StartDetached(new ProcessStartInfo(hostExe) { WorkingDirectory = _options.GameDir }, "host");
		Log("host started");
	}

	private void StartBoxGame((string Name, string Url, bool Boxed, string Root, string? Box) client)
	{
		if (BoxGameProcesses(client.Box!).Count > 0)
		{
			Log($"{client.Name} process exists; letting it come up");
			return;
		}

		SyncPlugins(client);
		EnsureBoxSteam(client.Box!);
		StartDetached(
			new ProcessStartInfo(_options.SandboxieExe)
			{
				Arguments = $"/box:{client.Box} \"{Path.Combine(client.Root, "CasualtiesUnknown.exe")}\""
			},
			client.Name);
		Log($"{client.Name} started");
	}

	/// <summary>
	/// Starts a client detached from the caller's console: the child gets private pipes this launcher
	/// drains, so a game that outlives the launcher cannot hold the caller's stdout open.
	///
	/// Measured 2026-11-20 (A3): the suite starts this launcher with <c>dotnet run … launch</c> and reads
	/// its stdout to the end (<c>SessionDriver.cs:775</c>). The launcher exited, the relaunched host
	/// answered on its port, and the suite still sat in that read: the game, started here with no
	/// redirect, had inherited the launcher's stdout pipe and kept the write end open after the
	/// launcher was gone. Handing the child its own drained pipes removes the inherited handle, so the
	/// read ends when the launcher's own process ends.
	/// </summary>
	private void StartDetached(ProcessStartInfo start, string name)
	{
		DetachStandardHandles();
		// The window must never rise over the person's work, and the flash before the game's own code runs is created
		// by this call, not by anything the game does later. The documented lever is the startup information a parent
		// hands to `CreateProcess`: `STARTF_USESHOWWINDOW` with `SW_SHOWNOACTIVATE` shows the window without activating
		// it, from the first frame. Nothing a later process does can undo a window that was created activated.
		var commandLine = "\"" + start.FileName + "\"" + (string.IsNullOrWhiteSpace(start.Arguments) ? string.Empty : " " + start.Arguments);
		// An empty current directory is not "leave it alone": `CreateProcess` refuses the whole call with
		// ERROR_INVALID_NAME (123). The boxed client passes no working directory, which is how its start failed.
		var workingDirectory = string.IsNullOrWhiteSpace(start.WorkingDirectory) ? null : start.WorkingDirectory;
		var pid = CreateWithoutActivation(start.FileName, commandLine, workingDirectory, out var thread, out var error);
		if (pid == 0)
		{
			throw new InvalidOperationException($"could not start the {name}: {error}");
		}

		// The process is created suspended, so the hook is in place before it runs one instruction: created and then
		// resumed, the first activation slipped through the gap between "the process exists" and "the hook is
		// installed", and the white startup screen still came forward (measured 2026-11-22).
		WindowQuiet.Install(pid);
		ResumeThread(thread);
		CloseHandle(thread);
		var process = Process.GetProcessById(pid);
		_started.Add(process);
		// Sandboxie starts the boxed game itself, so the pid above can be the box's starter: the window the person sees
		// belongs to a different process. Cover the game processes the boxes report as soon as they appear.
		CoverBoxedGames();
	}

	private static int CreateWithoutActivation(string exe, string commandLine, string? workingDirectory, out nint thread, out string error)
	{
		var info = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), dwFlags = StartfUseShowWindow, wShowWindow = SwShowNoActivate };
		// Created suspended: the hook has to be installed before the process can create and activate its window.
		var ok = CreateProcess(exe, commandLine, nint.Zero, nint.Zero, false, CreateSuspended, nint.Zero, workingDirectory, ref info, out var created);
		if (!ok)
		{
			thread = nint.Zero;
			error = "CreateProcess failed with " + Marshal.GetLastWin32Error();
			return 0;
		}

		thread = created.hThread;
		// The process handle is not needed: `GetProcessById` opens its own after the resume below.
		CloseHandle(created.hProcess);
		error = string.Empty;
		return created.dwProcessId;
	}

	/// <summary>Covers the game processes a box started, not just the starter this class created.</summary>
	private void CoverBoxedGames()
	{
		foreach (var box in new[] { _options.GuestBox, _options.AltBox })
		{
			if (string.IsNullOrWhiteSpace(box))
			{
				continue;
			}

			foreach (var process in BoxGameProcesses(box))
			{
				WindowQuiet.Install(process.Id);
			}
		}
	}

	private const int CreateSuspended = 0x4;
	private const int StartfUseShowWindow = 0x1;
	private const short SwShowNoActivate = 4;

	[StructLayout(LayoutKind.Sequential)]
	private struct StartupInfo
	{
		public int cb;
		public string reserved;
		public string desktop;
		public string title;
		public int x;
		public int y;
		public int xSize;
		public int ySize;
		public int xCountChars;
		public int yCountChars;
		public int fillAttribute;
		public int dwFlags;
		public short wShowWindow;
		public short cbReserved2;
		public nint lpReserved2;
		public nint stdInput;
		public nint stdOutput;
		public nint stdError;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessInformation
	{
		public nint hProcess;
		public nint hThread;
		public int dwProcessId;
		public int dwThreadId;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CreateProcess(string? applicationName, string commandLine, nint processAttributes, nint threadAttributes, bool inheritHandles, int creationFlags, nint environment, string? currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint handle);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint ResumeThread(nint thread);

	/// <summary>
	/// Removes this process's own standard handles from what a child inherits. A redirected child still
	/// gets the private pipes created for it; clearing the flag keeps the pipe this launcher's stdout is
	/// connected to out of the child. Without it the game holds the caller's read end open after the
	/// launcher is gone and the caller never reads its end of the stream (measured 2026-11-20, A3: the
	/// suite wrote its report at 23:04:50 and the caller's job only finished at 23:06:26, when the
	/// restarted host died). Clearing is per handle and idempotent, so every start repeats it.
	/// </summary>
	private static void DetachStandardHandles()
	{
		foreach (var which in new[] { StdInput, StdOutput, StdError })
		{
			var handle = GetStdHandle(which);
			if (handle != nint.Zero && handle != InvalidHandle)
			{
				SetHandleInformation(handle, HandleFlagInherit, 0);
			}
		}
	}

	private const int StdInput = -10;
	private const int StdOutput = -11;
	private const int StdError = -12;
	private const uint HandleFlagInherit = 0x1;
	private static readonly nint InvalidHandle = -1;

	/// <summary>
	/// Pins every game window that appears to the bottom of the desktop, from the moment it is created,
	/// without ever activating it. The game builds its own window a second or two after the process starts
	/// and asks to be shown; pushing it down afterwards is a visible pop, which the user rejected
	/// (2026-11-22: "放到桌面最下层" — it must never rise in the first place). Each handle is pinned for a
	/// few seconds after it first shows, which covers the window's own "show" call and its first frames,
	/// and then left alone so a person who raises it on purpose is not fought.
	private const int GwlExStyle = -20;
	private const long WsExNoActivate = 0x08000000;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern long GetWindowLong(nint handle, int index);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern long SetWindowLong(nint handle, int index, long value);

	[DllImport("kernel32.dll")]
	private static extern nint GetStdHandle(int which);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);

	/// <summary>
	/// Drains one pipe of a client the launcher started, keeping the text in <c>%TEMP%</c> instead of
	/// throwing it away. A game that ends on its own writes why on these pipes — Unity's fatal-error
	/// text and Mono's unhandled-exception banner go to stderr, never to <c>Player.log</c> — and a
	/// client that dies with only a last log line is a failure this harness has to be able to explain
	/// (measured 2026-11-20, A3: the sandboxed guest ended 0.4 s after its own session teardown with
	/// no crash report, no WER entry, and nothing else on disk).
	/// </summary>
	private static void Drain(StreamReader reader, string name, string channel) => _ = Task.Run(async () =>
	{
		try
		{
			using var sink = new FileStream(
				Path.Combine(Path.GetTempPath(), $"cuo-client-{name}.{channel}.log"),
				FileMode.Append,
				FileAccess.Write,
				FileShare.ReadWrite);
			await reader.BaseStream.CopyToAsync(sink).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// The client closed its side, or this launcher is shutting down; nothing left to drain.
		}
	});

	/// <summary>The box keeps its own plug-in copy; the desktop deploy never reaches inside the sandbox.</summary>
	private void SyncPlugins((string Name, string Url, bool Boxed, string Root, string? Box) client)
	{
		var desktop = Path.Combine(_options.GameDir, "BepInEx", "plugins", "CasualtiesUnknownOnline");
		var boxed = Path.Combine(client.Root, "BepInEx", "plugins", "CasualtiesUnknownOnline");
		if (!Directory.Exists(desktop) || string.Equals(Path.GetFullPath(client.Root).TrimEnd('\\'), Path.GetFullPath(_options.GameDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		Directory.CreateDirectory(boxed);
		foreach (var file in Directory.EnumerateFiles(desktop, "*", SearchOption.AllDirectories))
		{
			var relative = Path.GetRelativePath(desktop, file);
			var destination = Path.Combine(boxed, relative);
			Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
			File.Copy(file, destination, overwrite: true);
		}

		Log($"{client.Name} plugins synced from the desktop build");
	}

	private void EnsureBoxSteam(string box)
	{
		if (BoxProcesses(box, "steam").Count > 0)
		{
			Log($"{box} has a Steam process (left alone; IP direct does not need it)");
		}
	}

	private void RestartNamed()
	{
		foreach (var client in _clients.Where(c => c.Boxed))
		{
			StopBoxGame(client.Box!);
		}

		if (_clients.Any(c => c.Name == "host"))
		{
			var hostExe = Path.GetFullPath(Path.Combine(_options.GameDir, "CasualtiesUnknown.exe"));
			var stopped = false;
			foreach (var process in HostCandidates(hostExe))
			{
				StopQuietly(process.Id);
				stopped = true;
			}

			if (!stopped)
			{
				var owner = PortOwner(new Uri(_options.HostUrl).Port);
				if (owner is not null)
				{
					StopQuietly(owner.Value);
				}
			}

			WaitUntil(() => !Endpoints.IsOpen(_options.HostUrl), TimeSpan.FromSeconds(10));
		}

		Thread.Sleep(3000);
		Log("restarted: " + string.Join(", ", _clients.Select(c => c.Name)));
	}

	/// <summary>
	/// A report can name what it ran, but the launcher is the only side that knows the pids of the
	/// processes it started. A crash case kills the host by this file, never by guessing a name.
	/// </summary>
	public void WritePids()
	{
		if (string.IsNullOrWhiteSpace(_options.PidsPath))
		{
			return;
		}

		var hostExe = Path.GetFullPath(Path.Combine(_options.GameDir, "CasualtiesUnknown.exe"));
		var hostPid = HostCandidates(hostExe)
			.Select(process => (int?)process.Id)
			.FirstOrDefault();
		var payload = new
		{
			host = new { pid = hostPid, url = _options.HostUrl, gameDir = _options.GameDir },
			guest = new
			{
				pids = BoxGameProcesses(_options.GuestBox).Select(process => process.Id).ToArray(),
				url = _options.GuestUrl,
				root = _options.GuestRoot,
				box = _options.GuestBox,
			},
			alt = new
			{
				pids = BoxGameProcesses(_options.AltBox).Select(process => process.Id).ToArray(),
				url = _options.AltUrl,
				root = _options.AltRoot,
				box = _options.AltBox,
			},
		};
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_options.PidsPath))!);
		File.WriteAllText(_options.PidsPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
		Log($"pids written (host pid {hostPid?.ToString() ?? "missing"})");
	}

	private void ApplySaveRoot()
	{
		if (string.IsNullOrWhiteSpace(_options.SaveRoot))
		{
			return;
		}

		var root = Path.GetFullPath(_options.SaveRoot);
		Environment.SetEnvironmentVariable("CUO_SAVE_ROOT", root);
		Environment.SetEnvironmentVariable("CUO_SAVE_TIMING", "1");
		Directory.CreateDirectory(root);
		Log("acceptance save root: " + root);
	}

	/// <summary>A box can only start when Sandboxie's service is up; an unelevated Start.exe cannot start it.</summary>
	private bool EnsureSandboxService()
	{
		if (ServiceState("SbieSvc") == "RUNNING")
		{
			return true;
		}

		Log("SbieSvc is not running; starting it elevated (a UAC prompt is expected)");
		try
		{
			Process.Start(new ProcessStartInfo("sc.exe", "start SbieSvc")
			{
				UseShellExecute = true,
				Verb = "runas",
				WindowStyle = ProcessWindowStyle.Hidden
			});
		}
		catch (Exception exception)
		{
			Log("FAIL: elevation was refused or failed, so no box can start: " + exception.Message);
			return false;
		}

		if (WaitUntil(() => ServiceState("SbieSvc") == "RUNNING", TimeSpan.FromSeconds(60)))
		{
			Log("SbieSvc is up");
			return true;
		}

		Log("FAIL: SbieSvc did not reach Running within 60 s");
		return false;
	}

	private static string ServiceState(string name)
	{
		try
		{
			using var process = Process.Start(new ProcessStartInfo("sc.exe", $"query {name}")
			{
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (process is null)
			{
				return string.Empty;
			}

			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit(5000);
			var match = Regex.Match(output, "STATE\\s*:\\s*\\d+\\s+([A-Z]+)", RegexOptions.IgnoreCase);
			return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private void RestartBoxGame((string Name, string Url, bool Boxed, string Root, string? Box) client)
	{
		StopBoxGame(client.Box!);
		EnsureBoxSteam(client.Box!);
		Process.Start(new ProcessStartInfo(_options.SandboxieExe)
		{
			Arguments = $"/box:{client.Box} \"{Path.Combine(client.Root, "CasualtiesUnknown.exe")}\"",
			UseShellExecute = false,
			CreateNoWindow = true
		});
		Log($"{client.Name} restarted once (its earlier process exited before answering)");
	}

	private void StopBoxGame(string box)
	{
		foreach (var process in BoxGameProcesses(box))
		{
			StopQuietly(process.Id);
		}

		WaitUntil(() => BoxGameProcesses(box).Count == 0, TimeSpan.FromSeconds(10));
	}

	private List<Process> BoxGameProcesses(string box)
	{
		var result = new List<Process>();
		foreach (var pid in BoxProcessIds(box))
		{
			try
			{
				var process = Process.GetProcessById(pid);
				if (GameProcessName.Any(name => string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase)))
				{
					result.Add(process);
				}
			}
			catch (ArgumentException)
			{
			}
		}

		return result;
	}

	private List<Process> BoxProcesses(string box, string name)
	{
		var result = new List<Process>();
		foreach (var pid in BoxProcessIds(box))
		{
			try
			{
				var process = Process.GetProcessById(pid);
				if (string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase))
				{
					result.Add(process);
				}
			}
			catch (ArgumentException)
			{
			}
		}

		return result;
	}

	private List<int> BoxProcessIds(string box)
	{
		var ids = new List<int>();
		try
		{
			using var process = Process.Start(new ProcessStartInfo(_options.SandboxieExe, $"/box:{box} /listpids")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (process is null)
			{
				return ids;
			}

			var output = process.StandardOutput.ReadToEnd();
			process.StandardError.ReadToEnd();
			process.WaitForExit(10000);
			foreach (var line in output.Split('\n'))
			{
				var trimmed = line.Trim();
				if (int.TryParse(trimmed, out var id) && id != 0)
				{
					ids.Add(id);
				}
			}
		}
		catch (Exception exception)
		{
			Log($"could not list pids in {box}: {exception.Message}");
		}

		return ids;
	}

	private static IEnumerable<Process> GameProcesses()
	{
		var processes = new List<Process>();
		foreach (var name in GameProcessName)
		{
			try
			{
				processes.AddRange(Process.GetProcessesByName(name));
			}
			catch (Exception)
			{
			}
		}

		return processes;
	}

	/// <summary>
	/// The pid of every game process the configured boxes hold. A boxed game reports the host install's
	/// image path — measured 2026-11-21: the member's <c>Path</c> was the host's own
	/// <c>CasualtiesUnknown.exe</c>, so an image-path match cannot tell the member from the host.
	/// </summary>
	private HashSet<int> BoxedGameProcessIds()
	{
		var pids = new HashSet<int>();
		// Every box on this machine, not only this group's: a boxed host reports the same real image path as the desktop
		// one, so a second group's host looked like this group's own and got adopted, then the run died with
		// -532462766 (measured 2026-11-26 23:00, two groups started side by side).
		foreach (var box in _clients.Where(candidate => candidate.Boxed).Select(candidate => candidate.Box!).Concat([_options.GuestBox, _options.AltBox]).Concat(EveryBoxOnThisMachine()))
		{
			if (string.IsNullOrWhiteSpace(box))
			{
				continue;
			}

			foreach (var process in BoxGameProcesses(box))
			{
				pids.Add(process.Id);
			}
		}

		return pids;
	}

	/// <summary>
	/// Every sandbox box this machine has, read from the directory the configured roots live under: the roots a group
	/// is told about are only its own, and the other group's box is exactly the one a host-candidate match must not see.
	/// </summary>
	private IEnumerable<string> EveryBoxOnThisMachine()
	{
		var names = new List<string>();
		foreach (var root in new[] { _options.GuestRoot, _options.AltRoot, _options.GuestBox, _options.AltBox, _options.HostBox })
		{
			if (string.IsNullOrWhiteSpace(root))
			{
				continue;
			}

			var parts = root.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
			var at = Array.FindIndex(parts, part => string.Equals(part, "Sandbox", StringComparison.OrdinalIgnoreCase));
			if (at < 0 || at + 1 >= parts.Length)
			{
				continue;
			}

			var parent = string.Join(Path.DirectorySeparatorChar, parts.Take(at + 1));
			if (!Directory.Exists(parent))
			{
				continue;
			}

			foreach (var directory in Directory.GetDirectories(parent))
			{
				names.Add(Path.GetFileName(directory));
			}
		}

		return names.Distinct(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>The game processes that can be the host: the host's image path, never a boxed member.</summary>
	private List<Process> HostCandidates(string hostExe)
	{
		var boxed = BoxedGameProcessIds();
		return [.. GameProcesses().Where(process => !boxed.Contains(process.Id) && SamePath(process, hostExe))];
	}

	private static bool SamePath(Process process, string path)
	{
		try
		{
			return string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty), path, StringComparison.OrdinalIgnoreCase);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>How long the process has existed; an unreadable start time is treated as fresh, so it is never killed blindly.</summary>
	private static TimeSpan HostAge(Process process)
	{
		try
		{
			return DateTime.Now - process.StartTime;
		}
		catch (Exception)
		{
			return TimeSpan.Zero;
		}
	}

	private static int? PortOwner(int port)
	{
		try
		{
			using var process = Process.Start(new ProcessStartInfo("netstat", "-ano -p tcp")
			{
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			});
			if (process is null)
			{
				return null;
			}

			var output = process.StandardOutput.ReadToEnd();
			process.WaitForExit(10000);
			foreach (var line in output.Split('\n'))
			{
				var trimmed = line.Trim();
				if (!trimmed.Contains($"LISTENING", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (parts.Length < 5 || !parts[1].EndsWith($":{port}", StringComparison.Ordinal))
				{
					continue;
				}

				if (int.TryParse(parts[^1], out var pid))
				{
					return pid;
				}
			}
		}
		catch (Exception)
		{
		}

		return null;
	}

	private static void StopQuietly(int pid)
	{
		try
		{
			Process.GetProcessById(pid).Kill(entireProcessTree: true);
		}
		catch (Exception)
		{
		}
	}

	private static bool WaitUntil(Func<bool> condition, TimeSpan budget)
	{
		var deadline = DateTime.UtcNow + budget;
		while (DateTime.UtcNow < deadline)
		{
			if (condition())
			{
				return true;
			}

			Thread.Sleep(250);
		}

		return condition();
	}

	private void Log(string line)
	{
		var text = $"{DateTimeOffset.Now:HH:mm:ss} {line}";
		Console.WriteLine(text);
		_logFile?.WriteLine(text);
		_logFile?.Flush();
	}

	private static StreamWriter? OpenLog(string outDir)
	{
		try
		{
			Directory.CreateDirectory(outDir);
			return new StreamWriter(Path.Combine(outDir, "launch.log"), append: false);
		}
		catch (Exception)
		{
			return null;
		}
	}
}
