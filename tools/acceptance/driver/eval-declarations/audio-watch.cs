// eval declaration: audio-watch
// declares: AcceptanceAudioWatch
// serves: suppressed-native-call-sounds-stay-unheard (its "every client hears X once" rows)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceAudioWatch
{
	public static string Hits = "";
	public static int Frames;
	public static int Seen;
	public static bool Running;
	public static string Match = "";

	public static string Start(string match, int frames)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		Match = match == null ? "" : match;
		camera.StartCoroutine(Watch(frames));
		return "started";
	}

	public static IEnumerator Watch(int frames)
	{
		Running = true;
		// The sources are re-scanned every few frames ON PURPOSE: a one-shot clip can be played by a source the game CREATES for it, and
		// a list captured once at the start cannot see it (measured 2026-11-02: a dismembered limb reported 11 ambient clips and no
		// medical one, while the same event's clip may never have existed at scan time).
		var sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
		for (var frame = 0; frame < frames; frame++)
		{
			Frames++;
			if (frame % 5 == 0)
			{
				sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
			}

			for (var i = 0; i < sources.Length; i++)
			{
				var source = sources[i];
				if (source == null || !source.isPlaying || source.clip == null) { continue; }
				var name = source.clip.name;
				if (Match.Length > 0 && name.IndexOf(Match, System.StringComparison.OrdinalIgnoreCase) < 0) { continue; }
				if (Hits.IndexOf(name + "|", System.StringComparison.Ordinal) >= 0) { continue; }
				Hits = Hits + name + "|";
				Seen++;
			}
			yield return null;
		}
		Running = false;
	}
}
