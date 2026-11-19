using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// The read-only dependency preflight (the successor of the deleted
/// <c>tools/acceptance/preflight.ps1</c>): one row per
/// dependency, machine values from the acceptance-environment facts, exit 0 when a full two-client run
/// is possible, 2 when a required row is not present, 1 when the preflight itself failed. The third
/// client is an advisory row and never decides the exit code. Nothing on the machine is changed.
/// </summary>
internal static class PreflightCommand
{
	private const string Usage = "preflight [--facts <md>] [--json <path>] [--timeout-ms <ms>]";

	private static readonly string[] RequiredIds = ["steam", "game", "sandboxie", "dotnet", "capture", "logs", "artifacts"];
	private static readonly string[] RequiredFacts = ["game-dir", "game-app-id", "steam-exe", "sandboxie-exe", "sandbox-guest-root", "acceptance-artifacts-dir"];

	public static int Run(string[] args)
	{
		var arguments = new Arguments(args, Usage);
		var repoRoot = RepoPaths.RepositoryRoot();
		var factsPath = arguments.Optional("facts") ?? Path.Combine(repoRoot, "docs", "acceptance", "AGENTS.local.md");
		var timeout = arguments.OptionalNumber("timeout-ms", 750);

		try
		{
			var report = Invoke(repoRoot, factsPath, timeout);
			var required = report.Where(row => RequiredIds.Contains(row.Id, StringComparer.Ordinal)).ToList();
			var blocking = required.Where(row => !string.Equals(row.State, "present", StringComparison.Ordinal)).ToList();

			foreach (var row in report)
			{
				Console.WriteLine($"[{row.State}]".PadRight(10) + row.Id.PadRight(12) + row.Detail);
			}

			var summary = string.Join(", ", report
				.GroupBy(row => row.State)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.Select(group => $"{group.Count()} {group.Key}"));
			Console.WriteLine($"preflight: {summary}");

			var jsonPath = arguments.Optional("json");
			if (!string.IsNullOrWhiteSpace(jsonPath))
			{
				try
				{
					var payload = "[" + string.Join(",\n", report.Select(row =>
						$"{{\"id\":\"{row.Id}\",\"state\":\"{row.State}\",\"detail\":\"{Escape(row.Detail)}\"}}")) + "]";
					File.WriteAllText(jsonPath, payload, new System.Text.UTF8Encoding(false));
					Console.WriteLine($"json: {jsonPath}");
				}
				catch (Exception exception)
				{
					Console.WriteLine($"json: FAILED - {exception.Message} (the report above stands)");
				}
			}

			if (blocking.Count == 0)
			{
				var third = report.Any(row => row.Id == "sandbox-alt" && row.State == "present");
				Console.WriteLine("RESULT: OK - a full two-client run is possible" + (third ? "; the alternate third client is configured" : string.Empty));
				return 0;
			}

			Console.WriteLine($"RESULT: MISSING - {string.Join(", ", blocking.Select(row => row.Id))}; ask the user per docs/acceptance/dependencies.md (Asking the user)");
			return 2;
		}
		catch (Exception exception)
		{
			Console.WriteLine($"RESULT: ERROR - {exception.Message}");
			return 1;
		}
	}

	private static List<Row> Invoke(string repoRoot, string factsPath, int timeout)
	{
		var results = new List<Row>();
		var facts = AcceptanceFacts.Load(factsPath);
		if (facts.IsEmpty)
		{
			results.Add(new Row("facts", "missing", $"no 'acceptance environment' section resolved from {factsPath}; every key below is a question for the user"));
		}

		var gameDir = facts.Optional(string.Empty, "game-dir");
		var sandboxRoot = facts.Optional(string.Empty, "sandbox-guest-root");
		var artifactDir = facts.Optional(string.Empty, "acceptance-artifacts-dir");

		foreach (var key in RequiredFacts)
		{
			if (string.IsNullOrWhiteSpace(facts.Optional(string.Empty, key)))
			{
				results.Add(new Row("fact", "missing", $"FACT-MISSING: {key} is not set in the acceptance environment section"));
			}
		}

		// dotnet
		var dotnet = RunProcess("dotnet", ["--version"], repoRoot, out var dotnetOutput) ? dotnetOutput.Trim() : null;
		results.Add(string.IsNullOrWhiteSpace(dotnet)
			? new Row("dotnet", "missing", "dotnet is not on PATH; the commit under acceptance cannot be built")
			: new Row("dotnet", "present", $"dotnet {dotnet.Split('\n')[0].Trim()}"));

		// game install
		if (string.IsNullOrWhiteSpace(gameDir))
		{
			results.Add(new Row("game", "missing", "game-dir is not set"));
		}
		else if (!Directory.Exists(gameDir))
		{
			results.Add(new Row("game", "missing", "game-dir does not resolve"));
		}
		else
		{
			var missing = new List<string>();
			if (!File.Exists(Path.Combine(gameDir, "CasualtiesUnknown.exe")))
			{
				missing.Add("the game executable");
			}

			if (!File.Exists(Path.Combine(gameDir, "winhttp.dll")))
			{
				missing.Add("the doorstop DLL");
			}

			if (!Directory.Exists(Path.Combine(gameDir, "BepInEx")))
			{
				missing.Add("BepInEx");
			}

			results.Add(missing.Count > 0
				? new Row("game", "missing", "game-dir is missing " + string.Join(", ", missing))
				: new Row("game", "present", "game install, doorstop and BepInEx resolve"));
		}

		// deploy (advisory: the workflow's build/deploy step fixes it)
		if (!string.IsNullOrWhiteSpace(gameDir))
		{
			var deployed = Path.Combine(gameDir, "BepInEx", "plugins", "CasualtiesUnknownOnline", "CasualtiesUnknownOnline.dll");
			if (File.Exists(deployed))
			{
				var version = FileVersionInfo.GetVersionInfo(deployed).ProductVersion;
				var head = HeadSha(repoRoot) ?? "unknown";
				var written = File.GetLastWriteTime(deployed).ToString("u");
				results.Add(new Row("deploy", "present", $"deployed plugin ProductVersion {version}, written {written}; repository HEAD is {head}"));
			}
			else
			{
				results.Add(new Row("deploy", "missing", "no deployed plugin DLL under the game install; run the workflow build/deploy step"));
			}
		}
		else
		{
			results.Add(new Row("deploy", "missing", "game-dir is unresolved, so the deployed artifact cannot be checked"));
		}

		// steam
		var steamExe = facts.Optional(string.Empty, "steam-exe");
		var steamAppId = facts.Optional(string.Empty, "game-app-id");
		var steamRunning = Process.GetProcessesByName("steam").Length > 0;
		if (string.IsNullOrWhiteSpace(steamExe) || !File.Exists(steamExe))
		{
			results.Add(new Row("steam", "missing", "steam-exe does not resolve"));
		}
		else
		{
			var mapping = AppIdMapping(gameDir, steamAppId);
			var detail = $"steam.exe resolves; {(steamRunning ? "Steam is running" : "Steam is not running yet; the run starts it")}; {mapping.Detail}";
			if (mapping.State == "present")
			{
				detail += "; the account state is settled at launch";
			}

			results.Add(new Row("steam", mapping.State, detail));
		}

		// sandboxie
		var sandboxExe = facts.Optional(string.Empty, "sandboxie-exe");
		var sandboxSource = "sandboxie-exe";
		if (string.IsNullOrWhiteSpace(sandboxExe) || !File.Exists(sandboxExe))
		{
			var derived = SandboxieFromService();
			if (!string.IsNullOrWhiteSpace(derived))
			{
				sandboxExe = derived;
				sandboxSource = "derived from the SbieSvc image path";
			}
		}

		var sandboxProblems = new List<string>();
		if (string.IsNullOrWhiteSpace(sandboxExe) || !File.Exists(sandboxExe))
		{
			sandboxProblems.Add("sandboxie-exe does not resolve and cannot be derived from SbieSvc");
		}

		if (string.IsNullOrWhiteSpace(sandboxRoot) || !Directory.Exists(sandboxRoot))
		{
			sandboxProblems.Add("sandbox-guest-root does not resolve");
		}

		var services = new[] { "SbieSvc", "SbieDrv" }.Count(name => ServiceExists(name));
		if (services < 2)
		{
			sandboxProblems.Add($"SbieSvc and SbieDrv: found {services} of the 2 services");
		}

		results.Add(sandboxProblems.Count > 0
			? new Row("sandboxie", "missing", string.Join("; ", sandboxProblems))
			: new Row("sandboxie", "present", $"program ({sandboxSource}), guest sandbox root, SbieSvc and SbieDrv resolve"));

		// sandbox-alt (advisory)
		var altRoot = facts.Optional(string.Empty, "sandbox-alt-root");
		var altBox = facts.Optional(string.Empty, "sandbox-alt-box");
		var altUrl = facts.Optional(string.Empty, "hotrepl-alt-url");
		var altProblems = new List<string>();
		if (!string.IsNullOrWhiteSpace(altRoot) && !Directory.Exists(altRoot))
		{
			altProblems.Add("sandbox-alt-root does not resolve");
		}

		if (!string.IsNullOrWhiteSpace(altBox) && string.IsNullOrWhiteSpace(altRoot))
		{
			altProblems.Add("sandbox-alt-box is set but sandbox-alt-root is not");
		}

		if (!string.IsNullOrWhiteSpace(altUrl) && string.IsNullOrWhiteSpace(altRoot))
		{
			altProblems.Add("hotrepl-alt-url is set but sandbox-alt-root is not");
		}

		if (string.IsNullOrWhiteSpace(altRoot) && string.IsNullOrWhiteSpace(altBox) && string.IsNullOrWhiteSpace(altUrl))
		{
			results.Add(new Row("sandbox-alt", "missing", "no alternate-sandbox facts; every third-peer row is blocked (the two-client rows still run)"));
		}
		else
		{
			if (string.IsNullOrWhiteSpace(altBox))
			{
				altProblems.Add("sandbox-alt-box is not set");
			}

			if (string.IsNullOrWhiteSpace(altUrl))
			{
				altProblems.Add("hotrepl-alt-url is not set");
			}

			var altPort = 0;
			if (!string.IsNullOrWhiteSpace(altUrl))
			{
				if (Uri.TryCreate(altUrl, UriKind.Absolute, out var altUri))
				{
					altPort = altUri.Port;
				}
				else
				{
					altProblems.Add("hotrepl-alt-url is not an absolute URI");
				}
			}

			if (altProblems.Count == 0)
			{
				var altConfig = Path.Combine(altRoot!, "BepInEx", "config", "hotrepl.bepinex.cfg");
				var configPort = ReadHotReplPort(altConfig);
				if (configPort is null)
				{
					altProblems.Add($"the alternate client has no own HotRepl config carrying a Port ({altConfig}); it would read the physical install's config and collide with the host");
				}
				else if (configPort != altPort)
				{
					altProblems.Add($"the alternate client's HotRepl config carries port {configPort}, not the {altPort} in hotrepl-alt-url");
				}
				else
				{
					foreach (var otherKey in new[] { "hotrepl-host-url", "hotrepl-guest-url" })
					{
						var otherUrl = facts.Optional(string.Empty, otherKey);
						if (!string.IsNullOrWhiteSpace(otherUrl) && Uri.TryCreate(otherUrl, UriKind.Absolute, out var otherUri) && otherUri.Port == altPort)
						{
							altProblems.Add($"the alternate client's port {altPort} collides with {otherKey}");
						}
					}
				}
			}

			if (altProblems.Count > 0)
			{
				results.Add(new Row("sandbox-alt", "missing", string.Join("; ", altProblems)));
			}
			else
			{
				var up = Endpoints.IsOpen(altUrl!, timeout);
				results.Add(new Row("sandbox-alt", "present",
					$"alternate sandbox root and box resolve; its own HotRepl config carries port {altPort}, distinct from the host and guest; the endpoint is {(up ? "up" : "idle (no client running)")}"));
			}
		}

		// ip-names: a shared name is allowed while each client carries its own identity (decision 272)
		var names = new Dictionary<string, string?>
		{
			["host"] = IpDirectSetting(gameDir, "DisplayName"),
			["guest"] = IpDirectSetting(sandboxRoot, "DisplayName"),
			["alt"] = IpDirectSetting(altRoot, "DisplayName"),
		};
		var identities = new Dictionary<string, string?>
		{
			["host"] = IpDirectSetting(gameDir, "ClientIdentity"),
			["guest"] = IpDirectSetting(sandboxRoot, "ClientIdentity"),
			["alt"] = IpDirectSetting(altRoot, "ClientIdentity"),
		};
		var named = names.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key).ToList();
		var collisions = new List<(string Left, string Right, string Name)>();
		foreach (var left in named)
		{
			foreach (var right in named)
			{
				if (string.CompareOrdinal(left, right) < 0 && string.Equals(names[left], names[right], StringComparison.OrdinalIgnoreCase))
				{
					collisions.Add((left, right, names[left]!));
				}
			}
		}

		var unidentified = collisions
			.SelectMany(collision => new[] { collision.Left, collision.Right })
			.Where(side => string.IsNullOrWhiteSpace(identities[side]))
			.Distinct()
			.ToList();

		if (named.Count == 0)
		{
			results.Add(new Row("ip-names", "missing", "no client config carries an [IpDirect] DisplayName, so the names cannot be compared"));
		}
		else if (unidentified.Count > 0)
		{
			var detail = string.Join(", ", named.Select(name => $"{name}={names[name]}"));
			results.Add(new Row("ip-names", "missing",
				$"shared display name without a client identity yet ({detail}): {string.Join(", ", unidentified)} has no [IpDirect] ClientIdentity — start that client over IP-direct once so the identity is generated and the shared name is kept apart (decision 272)"));
		}
		else if (collisions.Count > 0)
		{
			var detail = string.Join("; ", collisions.Select(collision => $"{collision.Left} and {collision.Right} present '{collision.Name}'"));
			results.Add(new Row("ip-names", "present",
				$"a shared IP-direct display name is allowed ({detail}); each client's ClientIdentity keeps the character keys apart (decision 272)"));
		}
		else
		{
			results.Add(new Row("ip-names", "present", "distinct IP-direct display names (" + string.Join(", ", named.Select(name => $"{name}={names[name]}")) + ")"));
		}

		// hotrepl
		if (!string.IsNullOrWhiteSpace(gameDir))
		{
			if (!Directory.Exists(Path.Combine(gameDir, "BepInEx", "plugins", "HotRepl")))
			{
				results.Add(new Row("hotrepl", "missing", "the HotRepl plugin directory is not deployed under the game install"));
			}
			else
			{
				var hostUp = Endpoints.IsOpen(facts.Optional(string.Empty, "hotrepl-host-url") ?? string.Empty, timeout);
				var guestUp = Endpoints.IsOpen(facts.Optional(string.Empty, "hotrepl-guest-url") ?? string.Empty, timeout);
				results.Add(new Row("hotrepl", "present",
					$"plugin deployed; host endpoint {(hostUp ? "up" : "idle (no client running)")}, guest endpoint {(guestUp ? "up" : "idle (no client running)")}"));
			}
		}
		else
		{
			results.Add(new Row("hotrepl", "missing", "game-dir is unresolved, so the evaluator plugin cannot be checked"));
		}

		// capture
		results.Add(DesktopAvailable()
			? new Row("capture", "present", "an interactive desktop with the drawing stack is available")
			: new Row("capture", "missing", "no interactive desktop or drawing stack; visual rows cannot be captured"));

		// input: the in-process driver (the evaluator channel, never OS-level keyboard or mouse)
		var driverTemplate = Path.Combine(repoRoot, "tools", "acceptance", "driver", "InProcessDriver.cs");
		var recipes = Path.Combine(repoRoot, "tools", "acceptance", "recipes");
		var declarations = Path.Combine(repoRoot, "tools", "acceptance", "driver", "eval-declarations");
		results.Add(File.Exists(driverTemplate) && Directory.Exists(recipes) && Directory.Exists(declarations)
			? new Row("input", "present", "the in-process driver template, the scenario recipes and the eval declarations they name resolve; setups run through the evaluator channel the hotrepl row reports")
			: new Row("input", "missing", "the in-process driver template, the scenario recipes or the eval declarations they name do not resolve; a scenario that needs a driven setup stays blocked, and OS-level input is never a substitute"));

		// logs
		if (!string.IsNullOrWhiteSpace(gameDir) && Directory.Exists(gameDir))
		{
			var roots = new List<string> { Path.Combine(gameDir, "BepInEx") };
			if (!string.IsNullOrWhiteSpace(sandboxRoot))
			{
				roots.Add(Path.Combine(sandboxRoot, "BepInEx"));
			}

			var missingRoots = roots.Count(root => !Directory.Exists(root));
			results.Add(missingRoots == 0
				? new Row("logs", "present", $"{roots.Count} BepInEx log root(s) resolve; per-run files appear after a client starts")
				: new Row("logs", "missing", $"{missingRoots} of {roots.Count} BepInEx log root(s) missing (the guest's root comes from the sandbox install)"));
		}
		else
		{
			results.Add(new Row("logs", "missing", "game-dir is unresolved, so the log root cannot be checked"));
		}

		// artifacts
		if (string.IsNullOrWhiteSpace(artifactDir))
		{
			results.Add(new Row("artifacts", "missing", "acceptance-artifacts-dir is not set"));
		}
		else
		{
			results.Add(Directory.Exists(artifactDir)
				? new Row("artifacts", "present", "the artifact directory resolves")
				: new Row("artifacts", "missing", "the artifact directory does not exist yet; create it or point acceptance-artifacts-dir elsewhere"));
		}

		return results;
	}

	private static string? HeadSha(string repoRoot) => RunProcess("git", ["-C", repoRoot, "rev-parse", "HEAD"], repoRoot, out var output) ? output.Trim() : null;

	private static string? InstallAppId(string gameDir)
	{
		var file = Path.Combine(gameDir, "steam_appid.txt");
		if (!File.Exists(file))
		{
			return null;
		}

		foreach (var line in File.ReadLines(file).Take(5))
		{
			if (!string.IsNullOrWhiteSpace(line))
			{
				return line.Trim();
			}
		}

		return null;
	}

	private static string? ManifestPath(string gameDir, string appId)
	{
		var common = Path.GetDirectoryName(gameDir.TrimEnd('\\', '/'));
		if (string.IsNullOrWhiteSpace(common) || !string.Equals(Path.GetFileName(common), "common", StringComparison.Ordinal))
		{
			return null;
		}

		var steamApps = Path.GetDirectoryName(common);
		if (string.IsNullOrWhiteSpace(steamApps))
		{
			return null;
		}

		var manifest = Path.Combine(steamApps, $"appmanifest_{appId}.acf");
		return File.Exists(manifest) ? manifest : null;
	}

	private static string? ManifestInstallDir(string path)
	{
		try
		{
			foreach (var line in File.ReadLines(path))
			{
				var match = Regex.Match(line, "^\\s*\"installdir\"\\s*\"(.*)\"\\s*$");
				if (match.Success)
				{
					return match.Groups[1].Value;
				}
			}
		}
		catch (Exception)
		{
		}

		return null;
	}

	private static (string State, string Detail) AppIdMapping(string? gameDir, string? appId)
	{
		if (string.IsNullOrWhiteSpace(appId))
		{
			return ("unknown", "game-app-id is not set, so the launch id cannot be cross-checked against the install");
		}

		var id = appId.Trim();
		if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
		{
			return ("unknown", $"game-dir is unresolved, so app id {id} cannot be cross-checked against the install");
		}

		var expected = Path.GetFileName(gameDir.TrimEnd('\\', '/'));
		var installId = InstallAppId(gameDir);
		var manifest = ManifestPath(gameDir, id);
		var manifestDir = manifest is null ? null : ManifestInstallDir(manifest);

		var conflicts = new List<string>();
		var confirmations = new List<string>();
		if (!string.IsNullOrWhiteSpace(installId))
		{
			if (string.Equals(installId, id, StringComparison.OrdinalIgnoreCase))
			{
				confirmations.Add("the install's steam_appid.txt");
			}
			else
			{
				conflicts.Add($"the install's steam_appid.txt reads {installId}");
			}
		}

		if (!string.IsNullOrWhiteSpace(manifestDir))
		{
			if (string.Equals(manifestDir, expected, StringComparison.OrdinalIgnoreCase))
			{
				confirmations.Add($"the app manifest (installdir {manifestDir})");
			}
			else
			{
				conflicts.Add($"{Path.GetFileName(manifest)} names installdir '{manifestDir}', not this install ('{expected}')");
			}
		}

		if (conflicts.Count > 0)
		{
			return ("missing", $"game-app-id {id} is not this install: {string.Join("; ", conflicts)}; fix the fact or the install before launching");
		}

		if (confirmations.Count > 0)
		{
			return ("present", $"app id {id} matches {string.Join(" and ", confirmations)}");
		}

		return ("unknown", $"app id {id} cannot be cross-checked against the install: no steam_appid.txt under game-dir and no appmanifest_{id}.acf with an installdir beside it; a manifest is only read when the install sits under a steamapps\\common folder; do not launch on an unproven id");
	}

	private static string? SandboxieFromService()
	{
		try
		{
			if (!RunProcess("sc", ["qc", "SbieSvc"], Directory.GetCurrentDirectory(), out var output))
			{
				return null;
			}

			var match = Regex.Match(output, "BINARY_PATH_NAME\\s*:\\s*(.+)", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return null;
			}

			var path = match.Groups[1].Value.Trim().Trim('"');
			var directory = Path.GetDirectoryName(path);
			if (string.IsNullOrWhiteSpace(directory))
			{
				return null;
			}

			foreach (var candidate in new[] { "Start.exe", "SandMan.exe", "SbieCtrl.exe" })
			{
				var full = Path.Combine(directory, candidate);
				if (File.Exists(full))
				{
					return full;
				}
			}
		}
		catch (Exception)
		{
		}

		return null;
	}

	private static bool ServiceExists(string name) => RunProcess("sc", ["query", name], Directory.GetCurrentDirectory(), out _);

	private static int? ReadHotReplPort(string configPath)
	{
		if (!File.Exists(configPath))
		{
			return null;
		}

		foreach (var line in File.ReadLines(configPath))
		{
			var match = Regex.Match(line, "^\\s*Port\\s*=\\s*(\\d+)\\s*$");
			if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
			{
				return port;
			}
		}

		return null;
	}

	private static string? IpDirectSetting(string? installRoot, string key)
	{
		if (string.IsNullOrWhiteSpace(installRoot))
		{
			return null;
		}

		var path = Path.Combine(installRoot, "BepInEx", "config", "CasualtiesUnknownOnline.cfg");
		if (!File.Exists(path))
		{
			return null;
		}

		var inSection = false;
		foreach (var line in File.ReadLines(path))
		{
			var heading = Regex.Match(line, "^\\s*\\[(.+)\\]\\s*$");
			if (heading.Success)
			{
				inSection = string.Equals(heading.Groups[1].Value.Trim(), "IpDirect", StringComparison.Ordinal);
				continue;
			}

			if (inSection)
			{
				var match = Regex.Match(line, "^\\s*" + Regex.Escape(key) + "\\s*=\\s*(.*)$");
				if (match.Success)
				{
					return match.Groups[1].Value.Trim();
				}
			}
		}

		return null;
	}

	private static bool DesktopAvailable()
	{
		var handle = GetDC(IntPtr.Zero);
		if (handle == IntPtr.Zero)
		{
			return false;
		}

		ReleaseDC(IntPtr.Zero, handle);
		return true;
	}

	[DllImport("user32.dll")]

	private static extern IntPtr GetDC(IntPtr window);

	[DllImport("user32.dll")]

	private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

	private static bool RunProcess(string fileName, string[] arguments, string workingDirectory, out string output)
	{
		output = string.Empty;
		try
		{
			var startInfo = new ProcessStartInfo(fileName)
			{
				WorkingDirectory = workingDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach (var argument in arguments)
			{
				startInfo.ArgumentList.Add(argument);
			}

			using var process = Process.Start(startInfo);
			if (process is null)
			{
				return false;
			}

			output = process.StandardOutput.ReadToEnd();
			process.StandardError.ReadToEnd();
			process.WaitForExit(30000);
			return process.ExitCode == 0;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

	internal sealed record Row(string Id, string State, string Detail);
}
