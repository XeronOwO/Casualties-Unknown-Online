// eval declaration: audio-watch2
// declares: AcceptanceAudioWatch2
// serves: suppressed-native-call-sounds-stay-unheard (its "every client hears X once" rows)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceAudioWatch2
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

		// Re-scanned every few frames: a one-shot `Sound.Play` can create its own AudioSource instead of reusing a pooled one, so a
		// single scan at the start sees none of the clips that matter (measured 2026-11-01: a cached scan recorded nothing across a
		// remote treatment the poll-based sampler caught by luck).
		var sources = new AudioSource[0];
		for (var frame = 0; frame < frames; frame++)
		{
			if (frame % 5 == 0) { sources = UnityEngine.Object.FindObjectsOfType<AudioSource>(); }

			Frames++;
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
