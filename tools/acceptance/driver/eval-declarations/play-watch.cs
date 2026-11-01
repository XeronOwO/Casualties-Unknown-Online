// eval declaration: play-watch
// declares: AcceptancePlayWatch
// serves: lockpick-pain-reaches-no-peer (does the peer's replay reach Sound.Play at all?)
using System;
using System.Reflection;
using HarmonyLib;

public static class AcceptancePlayWatch
{
	public static string Calls = "";
	public static int Count;
	private static Harmony _harmony;

	public static string Start()
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		Type sound = null;
		for (var i = 0; i < assemblies.Length; i++)
		{
			try { sound = assemblies[i].GetType("Sound", false); } catch { sound = null; }
			if (sound != null) { break; }
		}

		if (sound == null) { return "no-sound-type"; }

		// The AudioClip overload `Sound.Play(AudioClip, Vector2, bool, bool, Transform, float, float, bool, bool)` — the one every replay uses once a clip has
		// been resolved, and the one whose refusal (or absence) a listener-side reading cannot tell apart from silence.
		var play = sound.GetMethod(
			"Play",
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
			null,
			new[] { typeof(AudioClip), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform), typeof(float), typeof(float), typeof(bool), typeof(bool) },
			null);
		if (play == null) { return "no-play-method"; }

		_harmony = new Harmony("cuo.acceptance.play-watch");
		_harmony.Patch(play, prefix: new HarmonyMethod(typeof(AcceptancePlayWatch).GetMethod("Before", BindingFlags.Static | BindingFlags.Public)));
		return "patched";
	}

	public static void Before(object[] __args)
	{
		Count++;
		var clip = __args != null && __args.Length > 0 ? __args[0] as AudioClip : null;
		var name = clip == null ? "null" : clip.name;
		if (Calls.Length < 4000) { Calls += "[" + name + "]"; }
	}
}
