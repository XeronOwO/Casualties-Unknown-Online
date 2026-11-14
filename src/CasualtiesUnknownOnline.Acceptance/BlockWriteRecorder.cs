using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using UDebug = UnityEngine.Debug;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// Records every live block write the game makes in the deep band (y at or below 900), once per cell, with the
/// method that made it. It exists to answer one question on a real session: which code writes the cells whose
/// tables differ between two clients (the divergence family's first probe names the band, this names the writer).
/// Installed by the acceptance plug-in only, so a production install never carries it.
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

		var stamper = AccessTools.Method(typeof(WorldGeneration), "GenerateObjectAtPos");
		if (stamper != null) { harmony.Patch(stamper, postfix: new HarmonyMethod(typeof(BlockWriteRecorder), nameof(StamperPostfix))); }
	}

	private static void Postfix(Vector2Int pos, ushort block)
	{
		if (pos.y < DeepBand || !Seen.Add(pos))
		{
			return;
		}

		var frame = new StackTrace(2, false).GetFrame(0)?.GetMethod();
		var caller = frame == null ? "unknown" : frame.DeclaringType?.Name + "." + frame.Name;
		UDebug.LogWarning($"[SetBlock] {pos.x},{pos.y} -> {block} from {caller}.");
	}

	// The structure stamper: what ran it, on which client, for which structure, and where the player stood.
	private static void StamperPostfix(Vector2Int pos, object tilemap, float rotation, bool genMode)
	{
		if (pos.y < DeepBand)
		{
			return;
		}

		var frame = new StackTrace(2, false).GetFrame(0)?.GetMethod();
		var caller = frame == null ? "unknown" : frame.DeclaringType?.Name + "." + frame.Name;
		var body = PlayerCamera.main == null ? null : PlayerCamera.main.body;
		var where = body == null ? "no-body" : body.transform.position.x.ToString(CultureInfo.InvariantCulture) + "," + body.transform.position.y.ToString(CultureInfo.InvariantCulture);
		var name = tilemap is Object o ? o.name : "no-tilemap";
		UDebug.LogWarning($"[Stamper] {name} at {pos.x},{pos.y} rot={rotation.ToString(CultureInfo.InvariantCulture)} gen={genMode} from {caller} player={where}.");
	}
}
