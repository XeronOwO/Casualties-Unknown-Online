// eval declaration: anim-watch
// declares: AcceptanceAnimWatch
// serves: lockpick-pain-reaches-no-peer and bark-vocalization-reaches-no-peer (is the report path reached at all?)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceAnimWatch
{
	public static string Calls = "";
	public static int Count;

	public static string Start()
	{
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		System.Type sync = null;
		for (var i = 0; i < assemblies.Length; i++)
		{
			try { sync = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.CharacterAttackAnimSync", false); } catch { sync = null; }
			if (sync != null) { break; }
		}

		if (sync == null) { return "no-sync-type"; }

		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var report = sync.GetMethod("OnReceived", flags);
		if (report == null) { return "no-receive-method"; }

		// A prefix on the adapter's own Report: this answers the one question reading could not — whether the capture reaches the report at all inside the
		// per-frame scopes (measured 2026-11-01: the acting client plays the clip and no peer does, with every other candidate ruled out by reading).
		var prefix = new HarmonyLib.HarmonyMethod(typeof(AcceptanceAnimWatch).GetMethod("Before", BindingFlags.Static | BindingFlags.Public));
		var harmony = new HarmonyLib.Harmony("cuo.acceptance.anim-watch");
		harmony.Patch(report, prefix: prefix);
		return "patched";
	}

	public static void Before(object[] __args)
	{
		Count++;
		var text = "";
		if (__args != null)
		{
			for (var i = 0; i < __args.Length; i++)
			{
				if (i > 0) { text += ","; }
				text += __args[i] == null ? "null" : __args[i].ToString();
			}
		}

		if (Calls.Length < 4000) { Calls += "[" + text + "]"; }
	}
}
