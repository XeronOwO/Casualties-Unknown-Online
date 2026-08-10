using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Makes the world-generation random stream deterministic across peers.
/// The game consumes UnityEngine.Random — one global stream — from many places
/// at once: generation coroutines, Body.Update sound/particle effects
/// (Body.cs:1169-1184 footstep pitch, 3437-3499 radiation), earthquake timers
/// (WorldGeneration.cs:857-901). Generation itself is a cross-frame coroutine
/// (WorldGeneration.cs:1534) that yields every ~100 columns, so without
/// isolation the stream diverges between peers at every suspension point:
/// host and guest start from the same captured Random.state
/// (WorldStartParams.RandomState) but the stream they continue from is
/// polluted by frame-rate-dependent consumers in between.
///
/// The fix is isolation, not a seed: Wrap() runs the game's own GenerateWorld
/// coroutine (body untouched — loading UI, generatingWorld flag and future
/// game updates inside it stay intact) and snapshots Random.state around every
/// suspension, so the generation stream advances purely from generation code.
/// Nested coroutines (e.g. WorldGenerateTerrain → WorldGenerateStructures,
/// WorldGeneration.cs:2819) are driven recursively and need no wrapping of
/// their own — their random consumption is a natural continuation of the
/// outer segment.
/// </summary>
internal static class WorldGenRandomIsolation
{
	private static readonly Stack<Random.State> _genStates = new();

	/// <summary>Diagnostics hook (bound by the adapter at construction) — generation-stream segment fingerprints for peer log comparison.</summary>
	internal static Action<string>? Log;

	private static int _segments;
	private static string? _lastSavedHex;

	/// <summary>The random stream state at the most recent segment start. The
	/// layer-modifier decision (ApplyLayerModifiers, WorldGeneration.cs:3729)
	/// runs AFTER the FINAL segment restore — no further save/restore pair
	/// exists there, so the world's frame-level draws between that restore and
	/// the decision leak into the public stream per-side (frame-rate dependent;
	/// observed 123-151 ms windows at 10-20 fps). Rewinding the decision to
	/// this state makes the roll a pure function of the segment start, which is
	/// fingerprint-identical on every side.</summary>
	internal static Random.State? LastSegmentStart { get; private set; }

	private static void Save()
	{
		_genStates.Push(Random.state);
		_segments++;
		_lastSavedHex = StateHex();
		LastSegmentStart = Random.state;
		// Fingerprint every early segment (the divergence point is what matters),
		// then sample the tail — a full 1024-column generation is ~40 segments.
		if (Log is not null && (_segments <= 24 || _segments % 8 == 0))
		{
			Log($"[GenStream] segment {_segments}: {_lastSavedHex}");
		}
	}

	private static void Restore()
	{
		// Diagnostics (layer-modifier hunt): the state BEFORE the restore is the
		// polluted public stream (real-time consumers ran during the yield), the
		// popped value is the saved segment state. If the restore does not land
		// on the saved state, the save/restore pairing is off.
		var before = StateHex();
		Random.state = _genStates.Pop();
		if (Log is not null)
		{
			Log($"[GenStream] restore before={before} after={StateHex()} expect={_lastSavedHex}");
		}
		_lastSavedHex = null;
	}

	private static string StateHex() => BitConverter.ToString(RandomStateSerializer.Serialize(Random.state)).Replace("-", "");

	/// <summary>
	/// Drive a generation sub-coroutine with the generation stream isolated
	/// from the public stream. Nested coroutines suspend through Drive (no
	/// save/restore — their consumption is the outer segment's continuation);
	/// plain yields (null, WaitForSeconds, WaitUntil) suspend through Unity
	/// and the stream is restored before the sub-coroutine resumes.
	/// </summary>
	private static IEnumerator Drive(IEnumerator sub, IPatchBridge adapter, bool resetFirst)
	{
		if (resetFirst)
		{
			// First MoveNext of the TOP-level drive: reset the stream
			// immediately before the first generation segment consumes any
			// Random. The reset and the first consumption share one coroutine
			// step — no frame boundary — so whatever the scene consumed in
			// between (a nested-coroutine launch is NOT same-stack in Unity 5.6)
			// is overwritten. Nested drives continue the stream and must not
			// reset (their consumption is the outer segment's continuation).
			adapter.ResetGenStreamToBaseline();
		}

		while (sub.MoveNext())
		{
			var current = sub.Current;
			if (current is IEnumerator nested)
			{
				yield return Drive(nested, adapter, resetFirst: false);
			}
			else
			{
				Save();
				yield return current;
				Restore();
			}
		}
	}

	/// <summary>
	/// Wrap the game's own GenerateWorld coroutine so the generation random
	/// stream is sealed across every suspension point. The original coroutine
	/// body is never replaced — only its stream is isolated.
	///
	/// Before the wrapped coroutine may consume any Random it holds until the
	/// guest's world params are applied (a layer switch generates before the
	/// host's new params arrive), then resets the stream to the captured
	/// baseline — BOTH sides generate from the state captured at the host's
	/// run-start entry (the click moment): everything consumed in between
	/// (transition, scene loading) is overwritten, so both generation streams
	/// start from the same state. The reset and the coroutine launch happen in
	/// the same coroutine step — no frame boundary, nothing can consume Random
	/// in between. Host/solo apply synchronously in the prefix, so the hold
	/// never engages.
	/// </summary>
	public static IEnumerator Wrap(IEnumerator generateWorld, IPatchBridge adapter)
	{
		while (!adapter.EnsureGuestWorldParams())
		{
			yield return null;
		}

		// The reset happens inside Drive's first step, not here — the launch of
		// a nested coroutine is not same-stack, and a frame boundary between the
		// reset and the first Random call would leak one frame of public-stream
		// consumption into the generation start (divergent details).
		_segments = 0;
		LastSegmentStart = null; // a new generation — the first Save records the new layer's segment start
		yield return Drive(generateWorld, adapter, resetFirst: true);
		Log?.Invoke($"[GenStream] done — {_segments} segments.");
	}
}
