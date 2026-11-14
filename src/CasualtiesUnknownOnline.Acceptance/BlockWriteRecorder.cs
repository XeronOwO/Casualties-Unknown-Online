using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// Records every live block write the game makes in the deep band (y at or below 900), once per cell, with the
/// call chain that made it. It exists to answer one question on a real session: which code writes the cells whose
/// tables differ between two clients (the divergence family's first probe names the band, this names the writer
/// and the trigger above it). Installed by the acceptance plug-in only, so a production install never carries it.
/// </summary>
internal static class BlockWriteRecorder
{
	private const int DeepBand = 900;
	private static readonly HashSet<Vector2Int> Seen = [];

	internal static void Install(Harmony harmony)
	{
		var target = AccessTools.Method(typeof(WorldGeneration), nameof(WorldGeneration.SetBlock), [typeof(Vector2Int), typeof(ushort)]);
		if (target == null)
		{
			UDebug.LogWarning("[SetBlock] the acceptance recorder found no SetBlock(Vector2Int, ushort) to patch; the band write stays unread.");
			return;
		}

		harmony.Patch(target, postfix: new HarmonyMethod(typeof(BlockWriteRecorder), nameof(Postfix)));
	}

	// Depth 4: the writer, the structure stamper, and the two frames above it (the coroutine that ran the stamp).
	private static void Postfix(Vector2Int pos, ushort block)
	{
		if (pos.y < DeepBand || !Seen.Add(pos))
		{
			return;
		}

		var trace = new StackTrace(2, false);
		var camera = PlayerCamera.main;
		var at = camera != null && camera.body != null ? camera.body.transform.position : Vector3.zero;
		UDebug.LogWarning(
			$"[SetBlock] {pos.x},{pos.y} -> {block} at {at.x:0.0},{at.y:0.0} from {Name(trace, 0)} < {Name(trace, 1)} < {Name(trace, 2)} < {Name(trace, 3)}.");
	}

	private static string Name(StackTrace trace, int index)
	{
		var method = trace.FrameCount > index ? trace.GetFrame(index)?.GetMethod() : null;
		return method == null ? "?" : method.DeclaringType?.Name + "." + method.Name;
	}
}
