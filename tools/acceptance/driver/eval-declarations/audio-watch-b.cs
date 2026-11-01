// eval declaration: audio-watch-b
// declares: AcceptanceAudioWatchB
// serves: suppressed-native-call-sounds-stay-unheard (the peer's one-shot clip at a dismember)
//
// The second version, with the one thing the first got wrong: it re-scans the scene's AudioSources every frame. A one-shot clip is
// normally played by a source the game CREATES for it, and a list captured once at Start cannot see a source that did not exist yet
// (measured 2026-11-02: a direct Limb.Dismember on a patient at the listener's feet reported eleven ambient clips and no medical one,
// and the same reading came back identically after the sources were captured once - the instrument, not the game).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AcceptanceAudioWatchB
{
	public static string Hits = "";
	public static int Frames;
	public static int Seen;
	public static int Sources;
	public static bool Running;
	public static string Match = "";

	public static string Start(string match, int frames)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		Match = match == null ? "" : match;
		Hits = "";
		Frames = 0;
		Seen = 0;
		camera.StartCoroutine(Watch(frames));
		return "started";
	}

	public static IEnumerator Watch(int frames)
	{
		Running = true;
		for (var frame = 0; frame < frames; frame++)
		{
			Frames++;
			var sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
			Sources = sources.Length;
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

	public static string Report()
	{
		return "{\"ok\":true,\"running\":" + (Running ? "true" : "false")
			+ ",\"frames\":" + Frames
			+ ",\"seen\":" + Seen
			+ ",\"sources\":" + Sources
			+ ",\"hits\":\"" + Hits + "\"}";
	}
}