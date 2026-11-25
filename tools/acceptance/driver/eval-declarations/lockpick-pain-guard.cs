// eval declaration: lockpick-pain-guard
// declares: AcceptanceLockpickPainGuard
// serves: lockpick row (the hand's first pain ends the picking).
//
// A stuck pick costs the hand: `LockpingMinigame.cs:144-157` adds 20 pain and takes 15 off `clawHealth` every half
// second the hand is stuck, and `Body.cs:2776` makes the person unconscious at `averagePain > 99`. A player lets go at
// the first cost. Reading the pain from the suite is too slow to do that: one eval round trip is several hundred
// milliseconds, so the row looked after the hand had already gone from 0 to 104.8 (measured 2026-11-26). The guard
// presses the attack key itself and watches the body on every frame, releasing the key on the frame the pain rises.
using System.Collections;
using System.Reflection;
using UnityEngine;

public static class AcceptanceLockpickPainGuard
{
	const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	/// <summary>Where the guard stopped; the row reads this instead of polling the body.</summary>
	public static string Trace = "idle";

	/// <summary>The last pain the guard read, and the baseline it was given.</summary>
	public static float Pain = -1f;
	public static float Baseline = -1f;

	/// <summary>True while the coroutine still watches; the row waits on this turning false.</summary>
	public static bool Running;

	/// <summary>The screen point the guard keeps the pointer on, every frame, so the hand never falls back to the real mouse.</summary>
	public static float TargetX;
	public static float TargetY;

	static MethodInfo s_moveTo;
	static MethodInfo s_releaseKey;

	static System.Type FindType(string simpleName)
	{
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	}

	static void Release()
	{
		var vi = FindType("VirtualInput");
		if (vi != null) { vi.GetMethod("ReleaseKey", Flags).Invoke(null, new object[] { KeyCode.Mouse0 }); }
	}
	static Body TheBody()
	{
		var game = MinigameBase.main;
		if (game != null && game.body != null) { return game.body; }
		var camera = PlayerCamera.main;
		return camera == null ? null : camera.body;
	}

	public static string Start(float x, float y, float baseline, float timeoutSeconds)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { Trace = "no-camera"; return "no-camera"; }
		var vi = FindType("VirtualInput");
		if (vi == null) { Trace = "no-VirtualInput"; return "no-VirtualInput"; }
		s_moveTo = vi.GetMethod("MoveTo", Flags);
		s_releaseKey = vi.GetMethod("ReleaseKey", Flags);
		vi.GetMethod("Install", Flags).Invoke(null, new object[] { false });
		TargetX = x;
		TargetY = y;
		s_moveTo.Invoke(null, new object[] { x, y });
		vi.GetMethod("PressKey", Flags).Invoke(null, new object[] { KeyCode.Mouse0 });
		Baseline = baseline;
		Pain = baseline;
		Running = true;
		Trace = "pressed";
		camera.StartCoroutine(Watch(timeoutSeconds));
		return "started";
	}

	/// <summary>The progress the lock reached while the guard held the key; read by the row after it stops.</summary>
	public static float Progress()
	{
		var game = MinigameBase.main;
		if (game == null || game.currentMinigame == null) { return 0f; }
		var field = game.currentMinigame.GetType().GetField(
			"lockProgress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		return field == null ? 0f : System.Convert.ToSingle(field.GetValue(game.currentMinigame));
	}

	static IEnumerator Watch(float timeoutSeconds)
	{
		var started = Time.realtimeSinceStartup;
		while (Time.realtimeSinceStartup - started < timeoutSeconds)
		{
			yield return null;
			// The staged pointer only exists on frames the harness runs; a frame without it hands the game the real
			// mouse, the hand leaves the band and the stuck clock runs. Keeping the pointer every frame is what a
			// player's own hand does.
			if (s_moveTo != null) { s_moveTo.Invoke(null, new object[] { TargetX, TargetY }); }
			var body = TheBody();
			if (body == null) { Trace = "released:no-body"; break; }
			Pain = body.averagePain;
			if (Pain > Baseline) { Trace = "released:pain:" + Pain.ToString("0.###"); break; }
			var game = MinigameBase.main;
			if (game == null || game.currentMinigame == null) { Trace = "closed:minigame"; break; }
			if (Progress() >= 0.99f) { Trace = "closed:turned"; break; }
		}

		if (s_releaseKey != null) { s_releaseKey.Invoke(null, new object[] { KeyCode.Mouse0 }); }
		Running = false;
		if (Trace == "pressed") { Trace = "released:timeout"; }
	}
}
