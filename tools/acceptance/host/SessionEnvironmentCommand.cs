namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Says whether the game install is free for an acceptance run, and swaps its BepInEx trees only when it
/// is (a port of <c>tools/acceptance/session-environment.ps1</c>).
/// </summary>
internal static class SessionEnvironmentCommand
{
	private const string ActiveName = "BepInEx";
	private const string GameProcessName = "CasualtiesUnknown";
	private const string CuoMarkerName = "CasualtiesUnknownOnline.dll";

	public static int Run(string[] args)
	{
		var options = new Arguments(
			args,
			"env --game <install> [--mode status|ensure-cuo] [--play-marker <dll>] [--parked-tree <name>] [--process <name>]");
		var gameDir = options.Required("game");
		var mode = (options.Optional("mode") ?? "status").ToLowerInvariant();
		if (mode is not ("status" or "ensure-cuo"))
		{
			throw new CommandFailure(64, $"--mode must be status or ensure-cuo; usage: {options.Usage}");
		}

		var playMarker = options.Optional("play-marker") ?? string.Empty;
		var parkedTree = options.Optional("parked-tree") ?? "BepInEx-play";
		var processName = options.Optional("process") ?? GameProcessName;

		if (!Directory.Exists(gameDir))
		{
			Console.WriteLine($"usage: --game does not resolve: {gameDir}");
			return 64;
		}

		var active = Path.Combine(gameDir, ActiveName);
		var trees = Directory.Exists(gameDir)
			? Directory.GetDirectories(gameDir, "BepInEx*").Select(Path.GetFileName).OfType<string>().ToList()
			: [];
		var running = System.Diagnostics.Process.GetProcessesByName(processName).Length > 0;

		if (!Directory.Exists(active))
		{
			Console.WriteLine("active=none");
			Console.WriteLine($"detail=no {ActiveName} tree under {gameDir}");
			Console.WriteLine($"game-running={Lower(running)}");
			Console.WriteLine("launch=blocked");
			return 2;
		}

		var activeKind = TreeKind(active, playMarker);
		var cuoTrees = trees.Where(tree => !Same(Path.Combine(gameDir, tree), active) && TreeKind(Path.Combine(gameDir, tree), playMarker) == "cuo").ToList();
		var playTrees = trees.Where(tree => !Same(Path.Combine(gameDir, tree), active) && TreeKind(Path.Combine(gameDir, tree), playMarker) == "play").ToList();
		var playTree = activeKind == "play" ? ActiveName : playTrees.Count >= 1 ? playTrees[0] : "-";
		var cuoTree = activeKind == "cuo" ? ActiveName : cuoTrees.Count >= 1 ? cuoTrees[0] : "-";
		var swapNeeded = activeKind != "cuo";
		var parkedFree = !Directory.Exists(Path.Combine(gameDir, parkedTree));
		var swapPossible = swapNeeded && cuoTrees.Count == 1 && parkedFree;
		var launch = running || (swapNeeded && !swapPossible) ? "blocked" : "ok";

		Console.WriteLine($"active={activeKind}");
		Console.WriteLine($"active-path={active}");
		Console.WriteLine($"play-tree={playTree}");
		Console.WriteLine($"cuo-tree={cuoTree}");
		Console.WriteLine($"game-running={Lower(running)}");
		Console.WriteLine($"swap-needed={Lower(swapNeeded)}");
		Console.WriteLine($"launch={launch}");

		if (mode == "status")
		{
			if (running)
			{
				Console.WriteLine("reason=game-running");
			}
			else if (swapNeeded && cuoTrees.Count != 1)
			{
				Console.WriteLine($"reason=cuo-tree-count={cuoTrees.Count}");
			}
			else if (swapNeeded && !parkedFree)
			{
				Console.WriteLine($"reason=parked-name-taken={parkedTree}");
			}

			return 0;
		}

		if (running)
		{
			Console.WriteLine("action=refused");
			Console.WriteLine("reason=game-running");
			Console.WriteLine("detail=a running game means its owner is playing; no process is killed and no folder is moved");
			return 2;
		}

		if (activeKind == "cuo")
		{
			Console.WriteLine("action=already-cuo");
			return 0;
		}

		if (cuoTrees.Count != 1)
		{
			Console.WriteLine("action=refused");
			Console.WriteLine($"reason=cuo-tree-count={cuoTrees.Count}");
			Console.WriteLine("detail=exactly one sibling tree must carry the CUO marker");
			return 2;
		}

		var parked = Path.Combine(gameDir, parkedTree);
		if (Directory.Exists(parked))
		{
			Console.WriteLine("action=refused");
			Console.WriteLine($"reason=parked-name-taken={parkedTree}");
			Console.WriteLine($"detail={parked} already exists; move it aside first");
			return 2;
		}

		Directory.Move(active, parked);
		Directory.Move(Path.Combine(gameDir, cuoTrees[0]), active);
		Console.WriteLine("action=swapped");
		Console.WriteLine($"parked-tree={parkedTree}");
		Console.WriteLine($"cuo-tree={ActiveName}");
		Console.WriteLine("launch=ok");
		return 0;
	}

	private static string TreeKind(string tree, string playMarker)
	{
		if (Marker(tree, CuoMarkerName) is not null)
		{
			return "cuo";
		}

		return playMarker.Length > 0 && Marker(tree, playMarker) is not null ? "play" : "other";
	}

	private static string? Marker(string tree, string marker)
	{
		if (marker.Length == 0 || !Directory.Exists(tree))
		{
			return null;
		}

		try
		{
			return Directory.EnumerateFiles(tree, marker, new EnumerationOptions
			{
				RecurseSubdirectories = true,
				MaxRecursionDepth = 6,
				IgnoreInaccessible = true,
			}).FirstOrDefault();
		}
		catch (IOException)
		{
			return null;
		}
	}

	private static bool Same(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

	private static string Lower(bool value) => value ? "true" : "false";
}
