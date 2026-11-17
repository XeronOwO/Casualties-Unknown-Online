using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
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

	/// <summary>
	/// The last decided type per geyser, kept after the write so the geyser's own
	/// <c>Start</c> — which rolls the PUBLIC random type — cannot overwrite it when
	/// the geyser activates after the restore or broadcast. The same entry also
	/// remembers that the geyser's own <c>Start</c> has run, which is what makes it
	/// capturable while its chunk is inactive.
	/// </summary>
	private static readonly ConditionalWeakTable<GeyserScript, RestoredType> RestoredTypes = new();

	/// <summary>Every geyser whose own Start has run, active or not — a geyser that has not run its Start has no decided type, and the started mark is set by the Start postfix. Empty while the world is still generating (the entities do not exist yet).</summary>
	internal static List<GeyserStateEntryMsg> Capture()
	{
		var geysers = new List<GeyserStateEntryMsg>();
		foreach (var geyser in Object.FindObjectsOfType<GeyserScript>(true))
		{
			if (!RestoredTypes.TryGetValue(geyser, out var state) || !state.Started)
			{
				continue;
			}

			var pos = geyser.transform.position;
			geysers.Add(new GeyserStateEntryMsg
			{
				Position = new NetVector2Msg(pos.x, pos.y),
				LiquidType = Traverse.Create(geyser).Field("liquidType").GetValue<byte>(), // byte — exact type (a GetValue<int> cast throws InvalidCastException)
			});
		}

		return geysers;
	}

	/// <summary>The geyser's own <c>Start</c> has run: capture it even after its chunk deactivates.</summary>
	internal static void MarkStarted(GeyserScript geyser) => RestoredTypes.GetValue(geyser, _ => new RestoredType()).Started = true;

	/// <summary>
	/// Write a decided set onto the live geysers, position-keyed. Absolute and
	/// idempotent: writing the same type again is a no-op, and a type the set does
	/// not name is left as the local roll produced it (the caller reports the
	/// count it matched). A geyser created after the set was captured is simply
	/// not matched — the periodic resend and the creation message carry it.
	///
	/// Each geyser in the world (active or not — the game deactivates far chunks) is
	/// attempted on its own (<see cref="ContainedRowLoop.RunLiveWorld"/>), so a type
	/// field this copy cannot serve costs ITSELF — named at error level with its
	/// position — instead of every geyser behind it. The count is per SAVED ROW,
	/// not per live object: two live geysers inside one row's 3 m radius mean one
	/// applied row, and a row no live geyser matches is refused. A matched row
	/// counts as applied only once its write completed, so a throwing object lands
	/// in the caller's refusal count (rows - applied).
	/// </summary>
	internal static int Apply(IReadOnlyList<GeyserStateEntryMsg> geysers, ILogger log)
	{
		var appliedRows = new HashSet<int>();
		ContainedRowLoop.RunLiveWorld(
			Object.FindObjectsOfType<GeyserScript>(true),
			geyser =>
			{
				var pos = geyser.transform.position;
				var position = new Vector2(pos.x, pos.y);
				var matched = -1;
				var nearest = PositionTolerance;
				for (var i = 0; i < geysers.Count; i++)
				{
					var saved = geysers[i];
					var distance = Vector2.Distance(new Vector2(saved.Position.X, saved.Position.Y), position);
					if (distance < nearest)
					{
						nearest = distance;
						matched = i;
					}
				}

				if (matched < 0)
				{
					return;
				}

				// The write lands first: a throwing write must not leave a remembered
				// type the audit called refused, and the postfix only replays a type
				// that was actually written.
				WriteLiquidType(geyser, geysers[matched].LiquidType);
				var restored = RestoredTypes.GetValue(geyser, _ => new RestoredType());
				restored.LiquidType = geysers[matched].LiquidType;
				restored.Restored = true;

				appliedRows.Add(matched);
			},
			geyser => $"({geyser.transform.position.x:F1},{geyser.transform.position.y:F1})",
			log,
			"restored geyser type");

		return appliedRows.Count;
	}

	/// <summary>
	/// The geyser's own <c>Start</c> has just rolled its type from the PUBLIC random
	/// stream: put the remembered decided type back so a restore or broadcast survives
	/// the activation. A geyser that never received a restored type (its entry exists
	/// only because Start ran) is left alone, never written with the default 0.
	/// </summary>
	internal static void ReapplyRestoredType(GeyserScript geyser)
	{
		if (RestoredTypes.TryGetValue(geyser, out var restored) && restored.Restored)
		{
			WriteLiquidType(geyser, restored.LiquidType);
		}
	}

	/// <summary>Write one decided type onto a geyser, if it differs (byte — exact type; a SetValue(int) cast throws ArgumentException).</summary>
	private static void WriteLiquidType(GeyserScript geyser, byte liquidType)
	{
		var typeField = Traverse.Create(geyser).Field("liquidType");
		if (typeField.GetValue<byte>() != liquidType)
		{
			typeField.SetValue(liquidType);
		}
	}

	private sealed class RestoredType
	{
		internal bool Started;
		internal bool Restored;
		internal byte LiquidType;
	}
}
