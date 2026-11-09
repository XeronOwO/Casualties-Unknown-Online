// eval declaration: run-ending-a
// declares: AcceptanceRunEndingA
// serves: 1.0-endings-authority (a run's ending is the host's branch; every client plays it).
//
// Reflection only: the tracker and the game's ending methods are found by name, so the evaluator never
// needs a typed reference to our own runtime or to Assembly-CSharp.
using System;
using System.Reflection;

public static class AcceptanceRunEndingA
{
	const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	public static string Run(string mode, string kind)
	{
		var runtime = FindAssembly("CasualtiesUnknownOnline.Runtime");
		if (runtime == null) { return "error=no-runtime-assembly"; }
		var tracker = runtime.GetType("CasualtiesUnknownOnline.Runtime.Session.World.RunEndingTracker", false);
		if (tracker == null) { return "error=no-tracker"; }

		if (mode == "call")
		{
			var game = FindAssembly("Assembly-CSharp");
			if (game == null) { return "error=no-game-assembly"; }
			var worldType = game.GetType("WorldGeneration", false);
			if (worldType == null) { return "error=no-worldgeneration"; }
			var worldField = worldType.GetField("world", Any);
			var world = worldField == null ? null : worldField.GetValue(null);
			if (world == null) { return "error=no-world"; }
			var methodName = kind == "breach" ? "SurfaceBreachCutscene" : "MercEnding";
			var method = worldType.GetMethod(methodName, Any, null, Type.EmptyTypes, null);
			if (method == null) { return "error=no-" + methodName; }
			method.Invoke(world, null);
		}
		else if (mode == "reset")
		{
			var clear = tracker.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
			if (clear != null) { clear.Invoke(null, null); }
		}

		var current = tracker.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
		var fromAuthority = tracker.GetProperty("FromAuthority", BindingFlags.Public | BindingFlags.Static);
		return "ending=" + (current == null ? "?" : current.GetValue(null, null))
			+ "|authority=" + (fromAuthority == null ? "?" : fromAuthority.GetValue(null, null))
			+ "|mode=" + mode;
	}

	private static Assembly FindAssembly(string name)
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			if (assemblies[i].GetName().Name == name) { return assemblies[i]; }
		}

		return null;
	}
}
