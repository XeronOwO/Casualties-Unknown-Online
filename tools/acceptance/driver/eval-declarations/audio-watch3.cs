// eval declaration: audio-watch3
// declares: AcceptanceAudioWatch3
// serves: suppressed-native-call-sounds-stay-unheard (its "every peer hears X ONCE" rows)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class AcceptanceAudioWatch3
{
	// A PLAY is one SOURCE, because Sound.Play creates a fresh GameObject with its own AudioSource for every call
	// (reversing/Assembly-CSharp/Assembly-CSharp/Sound.cs). Counting distinct source instances that ever played the matched clip
	// therefore counts playbacks, which is what "once" needs - a distinct-names list cannot tell one play from two.
	public static int Plays;
	public static string Ids = "";
	public static string Names = "";
	public static int Frames;
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

		// Re-scanned every few frames: `Sound.Play` creates a NEW AudioSource per call, so a single scan at the start sees none of them.
		var sources = new AudioSource[0];
		for (var frame = 0; frame < frames; frame++)
		{
			if (frame % 3 == 0) { sources = UnityEngine.Object.FindObjectsOfType<AudioSource>(); }

			Frames++;
			for (var i = 0; i < sources.Length; i++)
			{
				var source = sources[i];
				if (source == null || !source.isPlaying || source.clip == null) { continue; }
				var name = source.clip.name;
				if (Match.Length > 0 && name.IndexOf(Match, System.StringComparison.OrdinalIgnoreCase) < 0) { continue; }

				var id = source.GetInstanceID().ToString();
				if (Ids.IndexOf("[" + id + "]", System.StringComparison.Ordinal) >= 0) { continue; }

				Ids = Ids + "[" + id + "]";
				Plays++;
				if (Names.IndexOf("[" + name + "]", System.StringComparison.Ordinal) < 0) { Names = Names + "[" + name + "]"; }
			}

			yield return null;
		}

		Running = false;
	}
}
