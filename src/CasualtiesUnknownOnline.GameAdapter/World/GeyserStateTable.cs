using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The geyser liquid types as one table. A geyser rolls its type in
/// <c>GeyserScript.Start</c> from the PUBLIC random stream
/// (<c>GeyserScript.cs:12</c>) — not the isolated generation stream, whose
/// consumption only the generator's own body seals — so every side's copy can
/// roll a different type. The type is an initial condition of a world entity, not
/// an event payload: it is captured once the generation completed, carried
/// position-keyed (both sides create the same geyser at the same place) and
/// written back absolutely.
///
/// Both the live broadcast (<see cref="GeyserStateSync"/>) and the save restore
/// (<see cref="NativeWorldFacts"/>) go through this one implementation.
/// </summary>
internal static class GeyserStateTable
{
	/// <summary>Position-key radius — the rumble/presentation jitter of a regenerated geyser.</summary>
	private const float PositionTolerance = 3f;

	/// <summary>Every live geyser's decided liquid type (empty while the world is still generating — the entities do not exist yet).</summary>
	internal static List<GeyserStateEntryMsg> Capture()
	{
		var geysers = new List<GeyserStateEntryMsg>();
		foreach (var geyser in Object.FindObjectsOfType<GeyserScript>())
		{
			var pos = geyser.transform.position;
			geysers.Add(new GeyserStateEntryMsg
			{
				Position = new NetVector2Msg(pos.x, pos.y),
				LiquidType = Traverse.Create(geyser).Field("liquidType").GetValue<byte>(), // byte — exact type (a GetValue<int> cast throws InvalidCastException)
			});
		}

		return geysers;
	}

	/// <summary>
	/// Write a decided set onto the live geysers, position-keyed. Absolute and
	/// idempotent: writing the same type again is a no-op, and a type the set does
	/// not name is left as the local roll produced it (the caller reports the
	/// count it matched). A geyser created after the set was captured is simply
	/// not matched — the periodic resend and the creation message carry it.
	/// </summary>
	internal static int Apply(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		var applied = 0;
		foreach (var geyser in Object.FindObjectsOfType<GeyserScript>())
		{
			var pos = geyser.transform.position;
			var match = geysers.FirstOrDefault(g =>
				Vector2.Distance(new Vector2(g.Position.X, g.Position.Y), new Vector2(pos.x, pos.y)) < PositionTolerance);
			if (match is null)
			{
				continue;
			}

			// MATCHED: the live geyser exists — counted here, not at the write, so an
			// entry whose rolled liquid type already equals the restored one is still
			// a row the restored replay can call applied (the game rolls only two
			// types, so an equal value is common, not an anomaly).
			applied++;
			var typeField = Traverse.Create(geyser).Field("liquidType");
			if (typeField.GetValue<byte>() == match.LiquidType) // byte — exact type (a SetValue(int) cast throws ArgumentException)
			{
				continue;
			}

			typeField.SetValue(match.LiquidType);
		}

		return applied;
	}
}
