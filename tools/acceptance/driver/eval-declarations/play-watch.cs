// eval declaration: play-watch
// declares: AcceptancePlayWatch
// serves: lockpick-pain-reaches-no-peer and bark-vocalization-reaches-no-peer (is the report path reached at all?)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptancePlayWatch
{
	public static string Calls = "";
	public static int Count;

	public static string Start()
	{
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		System.Type sync = null;
		for (var i = 0; i < assemblies.Length; i++)
		{
			try { sync = assemblies[i].GetType("UnityEngine.Sound", false); } catch { sync = null; }
			if (sync != null) { break; }
		}

		if (sync == null) { return "no-sync-type"; }

		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var report = sync.GetMethod("OnReceived", flags);
		if (report == null) { return "no-receive-method"; }

		// A prefix on the adapter's own Report: this answers the one question reading could not — whether the capture reaches the report at all inside the
		// per-frame scopes (measured 2026-11-01: the acting client plays the clip and no peer does, with every other candidate ruled out by reading).
		var prefix = new HarmonyLib.HarmonyMethod(typeof(AcceptancePlayWatch).GetMethod("Before", BindingFlags.Static | BindingFlags.Public));
		var harmony = new HarmonyLib.Harmony("cuo.acceptance.play-watch2");
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

		// The clip and the kind, read off the message the handler received: a count alone cannot tell a footstep from the event under test (measured
		// 2026-11-01: forty messages arrived while the clip under test was never one of them).
		var described = text;
		if (__args != null && __args.Length > 1 && __args[1] != null) {
			var message = __args[1];
			var flags = BindingFlags.Instance | BindingFlags.Public;
			var kindProperty = message.GetType().GetProperty("Kind", flags);
			var clipProperty = message.GetType().GetProperty("Clip", flags);
			var ownerProperty = message.GetType().GetProperty("OwnerSteamId", flags);
			described = (kindProperty == null ? "?" : kindProperty.GetValue(message, null).ToString()) + ":" + (clipProperty == null ? "?" : clipProperty.GetValue(message, null).ToString()) + "@" + (ownerProperty == null ? "?" : ownerProperty.GetValue(message, null).ToString());
		}
		if (Calls.Length < 4000) { Calls += "[" + described + "]"; }
	}
}
