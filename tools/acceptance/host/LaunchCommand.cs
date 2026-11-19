namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// The client lifecycle the launcher script carried: resolve every machine value from the parameters or
/// the facts file, reuse a client that already answers, start the rest, and wait once for all endpoints.
/// Exit codes follow the script's convention (0 up, 6 host, 7 guest, 8 alt, 64 usage).
/// </summary>
internal static class LaunchCommand
{
	private const string Usage =
		"launch [--facts <md>] [--game-dir <install>] [--out-dir <dir>] [--steam-exe <exe>] [--sandboxie <start.exe>] " +
		"[--guest-box <box>] [--guest-root <install>] [--alt-box <box>] [--alt-root <install>] " +
		"[--host-url <ws>] [--guest-url <ws>] [--alt-url <ws>] [--only host|guest|alt] [--all] [--restart] " +
		"[--save-root <dir>] [--steam-grace <s>] [--ready-timeout <s>]";

	public static int Run(string[] args)
	{
		var arguments = new Arguments(args, Usage);
		var repoRoot = RepoPaths.RepositoryRoot();
		var factsPath = arguments.Optional("facts") ?? Path.Combine(repoRoot, "docs", "acceptance", "AGENTS.local.md");
		var facts = AcceptanceFacts.Load(factsPath);

		var options = new ClientLauncher.LauncherOptions(
			GameDir: facts.Require(arguments.Optional("game-dir") ?? string.Empty, "game-dir", "--game-dir"),
			SteamExe: facts.Require(arguments.Optional("steam-exe") ?? string.Empty, "steam-exe", "--steam-exe"),
			SandboxieExe: facts.Require(arguments.Optional("sandboxie") ?? string.Empty, "sandboxie-exe", "--sandboxie"),
			GuestBox: facts.Require(arguments.Optional("guest-box") ?? string.Empty, "sandbox-guest-box", "--guest-box"),
			GuestRoot: facts.Require(arguments.Optional("guest-root") ?? string.Empty, "sandbox-guest-root", "--guest-root"),
			AltBox: facts.Require(arguments.Optional("alt-box") ?? string.Empty, "sandbox-alt-box", "--alt-box"),
			AltRoot: facts.Require(arguments.Optional("alt-root") ?? string.Empty, "sandbox-alt-root", "--alt-root"),
			HostUrl: facts.Require(arguments.Optional("host-url") ?? string.Empty, "hotrepl-host-url", "--host-url"),
			GuestUrl: facts.Require(arguments.Optional("guest-url") ?? string.Empty, "hotrepl-guest-url", "--guest-url"),
			AltUrl: facts.Require(arguments.Optional("alt-url") ?? string.Empty, "hotrepl-alt-url", "--alt-url"),
			OutDir: arguments.Optional("out-dir") ?? Path.Combine(repoRoot, ".acceptance", "launcher"),
			All: arguments.Optional("all") is not null,
			Restart: arguments.Optional("restart") is not null,
			Only: arguments.Optional("only"),
			SaveRoot: arguments.Optional("save-root"),
			SteamGraceSeconds: arguments.OptionalNumber("steam-grace", 60),
			ReadyTimeoutSeconds: arguments.OptionalNumber("ready-timeout", 300));

		var launcher = new ClientLauncher(options);
		if (!launcher.Launch())
		{
			var hostMissing = launcher.Clients.Count > 0 && !Endpoints.IsOpen(options.HostUrl);
			return hostMissing ? 6 : 7;
		}

		Console.WriteLine("offline IP direct: no lobby to create; the endpoints are the deliverable");
		return 0;
	}
}
