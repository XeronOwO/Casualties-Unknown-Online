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

	private static void Save() => _genStates.Push(Random.state);

	private static void Restore() => Random.state = _genStates.Pop();

	/// <summary>
	/// Drive a generation sub-coroutine with the generation stream isolated
	/// from the public stream. Nested coroutines suspend through Drive (no
	/// save/restore — their consumption is the outer segment's continuation);
	/// plain yields (null, WaitForSeconds, WaitUntil) suspend through Unity
	/// and the stream is restored before the sub-coroutine resumes.
	/// </summary>
	private static IEnumerator Drive(IEnumerator sub)
	{
		while (sub.MoveNext())
		{
			var current = sub.Current;
			if (current is IEnumerator nested)
			{
				yield return Drive(nested);
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
	/// guest's world params are applied (the host sent WorldJoin at its run-start
	/// entry, but the params are captured at ITS GenerateWorld boundary — a fast
	/// guest transition can reach its own boundary first; starting the stream
	/// without the restore would generate a different world). Host/solo apply
	/// synchronously in the prefix, so the hold never engages.
	/// </summary>
	public static IEnumerator Wrap(IEnumerator generateWorld, IPatchBridge adapter)
	{
		while (!adapter.EnsureGuestWorldParams())
		{
			yield return null;
		}

		yield return Drive(generateWorld);
	}
}
