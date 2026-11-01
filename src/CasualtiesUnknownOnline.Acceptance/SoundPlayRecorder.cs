using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// Records the game's own one-shot sounds, so a batch can see a sound that has already finished.
///
/// <para>
/// A one-shot is invisible to a sampler: <c>audio-recent</c> can only answer a clip that happens to be mid-play when it is
/// asked, and an impact that lands between two driver round trips is missed no matter how long the fall is (measured
/// 2026-11-02: a 20-unit fall sampled eight times answered nothing on either client). This records the CALL instead —
/// <c>Sound.Play(string, …)</c> is the entry every native one-shot goes through, and its clip name is the same name the
/// sound tickets judge by.
/// </para>
///
/// <para>
/// It lives in the batch-only assembly and records nothing unless a batch installed it: the ring buffer is bounded, the
/// prefix does no work while <see cref="Enabled"/> is false, and the plugin is not deployed to a production install.
/// </para>
/// </summary>
internal static class SoundPlayRecorder
{
	private const int Capacity = 256;

	/// <summary>Newest last. A plain list with a cap: 256 entries is small and the oldest fall off the front.</summary>
	private static readonly List<Entry> Entries = [];

	private static readonly object Gate = new();

	/// <summary>Turns recording on. A batch installs it explicitly, like the staged input channel.</summary>
	internal static bool Enabled { get; private set; }

	internal readonly struct Entry(string clip, float time, Vector2 position)
	{
		internal string Clip { get; } = clip;

		internal float Time { get; } = time;

		internal Vector2 Position { get; } = position;
	}

	internal static void Start()
	{
		lock (Gate)
		{
			Entries.Clear();
			Enabled = true;
		}
	}

	internal static void Stop()
	{
		lock (Gate)
		{
			Enabled = false;
		}
	}

	/// <summary>The recorded entries whose clip contains <paramref name="match"/> (ordinal-ignore-case), newest last.</summary>
	internal static List<Entry> Since(string match)
	{
		lock (Gate)
		{
			var matches = new List<Entry>(Entries.Count);
			foreach (var entry in Entries)
			{
				if (match.Length == 0 || entry.Clip.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					matches.Add(entry);
				}
			}

			return matches;
		}
	}

	internal static void Clear()
	{
		lock (Gate)
		{
			Entries.Clear();
		}
	}

	/// <summary>
	/// Applies both overload patches explicitly. Nested attribute classes are NOT reached by
	/// <c>Harmony.PatchAll(typeof(...))</c> — it scans the type it is given, and a nested type is a different type — which is
	/// why the first version logged "the recorder is armed" and recorded nothing (measured 2026-11-02: two clients answered
	/// count=0 for a menu click that certainly plays a sound).
	/// </summary>
	internal static void Install(Harmony harmony)
	{
		var byName = AccessTools.Method(typeof(Sound), nameof(Sound.Play),
			[typeof(string), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform), typeof(float), typeof(float), typeof(bool), typeof(bool)]);
		var byClip = AccessTools.Method(typeof(Sound), nameof(Sound.Play),
			[typeof(AudioClip), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform), typeof(float), typeof(float), typeof(bool), typeof(bool)]);
		if (byName == null || byClip == null)
		{
			throw new InvalidOperationException("Sound.Play's overloads are not on this build: name=" + (byName != null) + " clip=" + (byClip != null));
		}

		harmony.Patch(byName, prefix: new HarmonyMethod(typeof(SoundPlayRecorder), nameof(PrefixClipName)));
		harmony.Patch(byClip, prefix: new HarmonyMethod(typeof(SoundPlayRecorder), nameof(PrefixClip)));
	}

	private static void PrefixClipName(string clip, Vector2 pos)
	{
		RawCalls++;
		Record(clip, pos);
	}

	private static void PrefixClip(AudioClip clip, Vector2 pos)
	{
		RawCalls++;
		Record(clip == null ? "" : clip.name, pos); // Unity object — ==
	}

	/// <summary>Plays one probe sound through the game's own entry and answers how many calls the prefixes have seen since the process started. A managed call compiled in this assembly is a fresh call site, so a zero answer means the patch does not reach the method at all.</summary>
	internal static long ProbePlay()
	{
		Sound.Play("probe", Vector2.zero, false, true, null, 1f, 1f, false, false);
		return RawCalls;
	}

	/// <summary>How many calls the prefixes have seen, armed or not. A zero here means the patch never ran at all, which is a different fault from "the buffer was empty".</summary>
	internal static long RawCalls { get; private set; }

	/// <summary>
	/// One plain-text answer for a batch: <c>raw=&lt;n&gt;|clip@time;clip@time;…</c>, newest last, filtered by
	/// <paramref name="match"/>. The formatting lives here rather than in an eval expression, so a reading cannot fail on
	/// reflection over the entry struct (measured 2026-11-02: the same read threw a NullReferenceException from the eval).
	/// </summary>
	internal static string Read(string match)
	{
		var builder = new System.Text.StringBuilder("raw=");
		builder.Append(RawCalls.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|');
		var first = true;
		foreach (var entry in Since(match))
		{
			if (!first)
			{
				builder.Append(';');
			}

			builder.Append(entry.Clip).Append('@').Append(entry.Time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
			first = false;
			if (builder.Length > 900)
			{
				break;
			}
		}

		return builder.ToString();
	}

	private static void Record(string clip, Vector2 position)
	{
		if (!Enabled || clip.Length == 0)
		{
			return;
		}

		lock (Gate)
		{
			Entries.Add(new Entry(clip, Time.time, position));
			if (Entries.Count > Capacity)
			{
				Entries.RemoveRange(0, Entries.Count - Capacity);
			}
		}
	}
}
