using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter;

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
/// The fix is isolation, not a seed: Drive() runs a generation sub-coroutine
/// and snapshots Random.state around every suspension, so the generation
/// stream advances purely from generation code. Nested coroutines (e.g.
/// WorldGenerateTerrain → WorldGenerateStructures, WorldGeneration.cs:2819)
/// are driven recursively and need no wrapping of their own — their random
/// consumption is a natural continuation of the outer segment.
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
	/// The GenerateWorld replacement: the same sub-steps in the same order as
	/// WorldGeneration.GenerateWorld (WorldGeneration.cs:1534-1548), each
	/// wrapped in Drive. The yield before UpdateWorld preserves the original
	/// frame rhythm (1543). UpdateWorld itself is synchronous and consumes no
	/// randomness (RandomizeTileTransforms is dead code).
	/// </summary>
	public static IEnumerator CreateIsolatedGenerateWorld(WorldGeneration world)
	{
		yield return Drive(Invoke(world, "WorldPreprocess"));
		yield return Drive(Invoke(world, "WorldCreateBackground"));
		yield return Drive(Invoke(world, "WorldGenerateTerrain"));
		yield return Drive(Invoke(world, "WorldGenerateWorldBorders"));
		yield return null;
		world.UpdateWorld();
		yield return Drive(Invoke(world, "WorldPlacePlayer"));
		yield return Drive(Invoke(world, "WorldPlaceEntities"));
		yield return Drive(Invoke(world, "FinishWorldGeneration"));
	}

	private static IEnumerator Invoke(WorldGeneration world, string method)
	{
		var mi = typeof(WorldGeneration).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
		return (IEnumerator)mi!.Invoke(world, null)!;
	}
}
