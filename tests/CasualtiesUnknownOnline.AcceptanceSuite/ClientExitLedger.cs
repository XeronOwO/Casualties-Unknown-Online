using System.Runtime.InteropServices;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The client processes the launcher started, held open so their exit code survives them.
///
/// <para>
/// A client that ends mid-row must fail the row with the code, not with "the driver did not answer":
/// measured 2026-11-20 (A3) the sandboxed member ended about five seconds after its own session
/// teardown, the row reported a refused socket, and nothing on disk said whether the process crashed,
/// exited on its own or was closed by someone else. The handle is opened while the client is alive
/// (an exit code cannot be read from a process that is already gone), and the launcher rewrites
/// <c>pids.json</c> on a restart, so the ledger re-reads it before every answer.
/// </para>
/// </summary>
internal sealed class ClientExitLedger(string? pidsPath)
{
	private const uint QueryLimitedInformation = 0x1000;
	private const uint StillActive = 259;
	private const uint InfiniteWait = 0xFFFFFFFF;
	private const uint WaitObjectZero = 0x00000000;

	private readonly object _gate = new();
	private readonly Dictionary<string, int> _pidsByUrl = new(StringComparer.Ordinal);
	private readonly Dictionary<int, nint> _handles = [];
	private readonly Dictionary<int, uint> _codes = [];
	private readonly HashSet<int> _expected = [];

	/// <summary>
	/// The case is about to stop this process on purpose. Its code is then part of the case's own
	/// action, not a cause: the host-crash row kills the host and restarts it, and a later read of the
	/// killed client must not blame the kill for the row's outcome.
	/// </summary>
	public void Expect(int pid)
	{
		lock (_gate)
		{
			_expected.Add(pid);
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetExitCodeProcess(nint handle, out uint code);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint WaitForSingleObject(nint handle, uint milliseconds);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);

	/// <summary>
	/// Reads the launcher's snapshot and opens a handle on every client that is still alive. A handle
	/// that already shows an exit code is kept: the answer is the code, the process is gone.
	/// </summary>
	public void Refresh()
	{
		if (string.IsNullOrWhiteSpace(pidsPath) || !File.Exists(pidsPath))
		{
			return;
		}

		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(File.ReadAllText(pidsPath));
		}
		catch (Exception)
		{
			return;
		}

		using (document)
		{
			var root = document.RootElement;
			Remember(root, "host");
			Remember(root, "guest");
			Remember(root, "alt");
		}

		lock (_gate)
		{
			foreach (var pid in _pidsByUrl.Values)
			{
				if (_handles.ContainsKey(pid))
				{
					continue;
				}

				var handle = OpenProcess(QueryLimitedInformation, false, pid);
				if (handle != nint.Zero)
				{
					_handles[pid] = handle;
					Watch(pid, handle);
				}
			}
		}
	}

	/// <summary>
	/// Waits on the process handle so the end time is the moment Windows signalled it, not the moment a
	/// caller next polled. Measured 2026-11-20 (A3): the first-poll time was read as the death time,
	/// which left open whether the member died at its own session teardown or at the host relaunch —
	/// a question the row must answer, because one is a product defect and the other a fixture one.
	/// </summary>
	private void Watch(int pid, nint handle) => _ = Task.Run(() =>
	{
		if (WaitForSingleObject(handle, InfiniteWait) != WaitObjectZero)
		{
			return;
		}

		lock (_gate)
		{
			_exitedAt[pid] = DateTime.Now;
			if (GetExitCodeProcess(handle, out var code))
			{
				_codes[pid] = code;
			}
		}
	});

	private void Remember(JsonElement root, string property)
	{
		if (!root.TryGetProperty(property, out var client) || client.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		var url = client.TryGetProperty("url", out var urlValue) && urlValue.ValueKind == JsonValueKind.String
			? urlValue.GetString()
			: null;
		if (string.IsNullOrWhiteSpace(url))
		{
			return;
		}

		var pid = 0;
		if (client.TryGetProperty("pid", out var single) && single.ValueKind == JsonValueKind.Number)
		{
			pid = single.GetInt32();
		}
		else if (client.TryGetProperty("pids", out var many) && many.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in many.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.Number)
				{
					pid = item.GetInt32();
					break;
				}
			}
		}

		if (pid > 0)
		{
			lock (_gate)
			{
				_pidsByUrl[url!] = pid;
			}
		}
	}

	/// <summary>
	/// What the process behind this endpoint did, when it is no longer there: the pid, the exit code
	/// and the moment it was first seen gone. Null while the client is alive (or unknown to the file).
	/// </summary>
	public string? Describe(string url)
	{
		Refresh();
		int pid;
		lock (_gate)
		{
			if (!_pidsByUrl.TryGetValue(url, out pid) || !_handles.TryGetValue(pid, out var handle))
			{
				return null;
			}

			if (!GetExitCodeProcess(handle, out var code))
			{
				return null;
			}

			if (code == StillActive)
			{
				return null;
			}

			if (!_codes.ContainsKey(pid))
			{
				_codes[pid] = code;
				_exitedAt[pid] = DateTime.Now;
			}

			var stopped = _expected.Contains(pid) ? " (stopped by the case)" : string.Empty;
			return $"the client process {pid} ended with exit code 0x{_codes[pid]:X8} at {_exitedAt[pid]:HH:mm:ss.fff}{stopped}";
		}
	}

	private readonly Dictionary<int, DateTime> _exitedAt = [];

	/// <summary>Every client the ledger has seen end, for the report (empty when all stayed up).</summary>
	public IReadOnlyList<string> Ended()
	{
		List<string> ended = [];
		lock (_gate)
		{
			foreach (var (pid, code) in _codes)
			{
				ended.Add($"process {pid} exit 0x{code:X8} at {_exitedAt[pid]:HH:mm:ss.fff}");
			}
		}

		return ended;
	}
}

/// <summary>
/// A driver call failed because the process behind the endpoint had already ended. It derives from
/// <see cref="DriverException"/> so every handler of an unanswered call keeps working, and a caller that
/// asks "does this client answer?" reads it as "no" — never as the refusal path for a live client that
/// answers but cannot join (<c>RelaunchAsync</c>, 2026-11-20: a plain wrapper made a dead host look like
/// a refusing one, the A3 row failed instead of restarting the host, and an uncaught subtype then ended
/// the whole suite with 0xE0434352).
/// </summary>
internal sealed class ClientEndedException(string message, Exception inner) : DriverException(message, inner);
