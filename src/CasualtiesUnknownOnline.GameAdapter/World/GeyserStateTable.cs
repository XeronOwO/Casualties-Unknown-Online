using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
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
	/// <summary>
	/// Position-key radius — a regenerated geyser can stand a few metres from the row the
	/// previous world recorded. Measured 2026-11-17: one row at (97.8,167.2) answered a live
	/// geyser at (97.8,163.6), 3.62 m away, while the next live geyser stood 12.3 m away, so
	/// the one-to-one assignment below still pairs them with no rival.
	/// </summary>
	private const float PositionTolerance = 6f;

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
	/// Write a decided set onto the live geysers, one saved row to one live geyser.
	/// Absolute and idempotent: writing the same type again is a no-op, and a type the set
	/// does not name is left as the local roll produced it (the caller reports the count it
	/// matched).
	///
	/// The match is ONE-TO-ONE inside <see cref="PositionTolerance"/>: every (row, live
	/// geyser) pair inside the radius is a candidate, the candidates are taken
	/// shortest-first, and a taken row or geyser cannot be claimed twice. Two geysers 1.35 m
	/// apart (measured 2026-11-17) would otherwise let one live object take the row its
	/// neighbour owns and leave that row refused. A row no claim takes is refused and named at warning
	/// level with its position, its nearest live geyser, that distance and the live/row counts — the
	/// reading that separates a shifted position from a lost live object, so the restore's refused
	/// count — rows minus applied — carries its own evidence.
	///
	/// Each write runs through <c>ContainedRowLoop.Run</c>: a throwing write costs ITSELF,
	/// is not counted as applied, and the remembered type is kept only after the write
	/// completed (the Start postfix replays a type that was written).
	/// </summary>
	internal static int Apply(IReadOnlyList<GeyserStateEntryMsg> geysers, ILogger log)
	{
		var live = Object.FindObjectsOfType<GeyserScript>(true);
		var candidates = new List<(float Distance, int Row, int Live)>();
		// The nearest live geyser per saved row, kept even when it is outside the tolerance: it is the
		// evidence a refusal needs (see the report below), because "no live geyser within 6 m" does not
		// say whether the geyser stands 6.4 m away or the live set holds different objects entirely.
		var nearestDistance = new float[geysers.Count];
		var nearestPoint = new Vector2[geysers.Count];
		for (var row = 0; row < geysers.Count; row++)
		{
			var saved = geysers[row];
			var position = new Vector2(saved.Position.X, saved.Position.Y);
			nearestDistance[row] = float.PositiveInfinity;
			for (var index = 0; index < live.Length; index++)
			{
				var geyser = live[index];
				if (geyser == null)
				{
					continue;
				}

				var point = geyser.transform.position;
				var distance = Vector2.Distance(position, new Vector2(point.x, point.y));
				if (distance < nearestDistance[row])
				{
					nearestDistance[row] = distance;
					nearestPoint[row] = new Vector2(point.x, point.y);
				}

				if (distance <= PositionTolerance)
				{
					candidates.Add((distance, row, index));
				}
			}
		}

		candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));
		var claimedRows = new bool[geysers.Count];
		var claimedLive = new bool[live.Length];
		var assignments = new List<KeyValuePair<GeyserScript, int>>();
		foreach (var candidate in candidates)
		{
			if (claimedRows[candidate.Row] || claimedLive[candidate.Live])
			{
				continue;
			}

			claimedRows[candidate.Row] = true;
			claimedLive[candidate.Live] = true;
			assignments.Add(new KeyValuePair<GeyserScript, int>(live[candidate.Live], candidate.Row));
		}

		for (var row = 0; row < geysers.Count; row++)
		{
			if (claimedRows[row])
			{
				continue;
			}

			var savedRow = geysers[row];
			if (live.Length == 0)
			{
				log.LogWarning(
					"Restored geyser type row at ({X:F1},{Y:F1}) was not written: the live world holds no geyser at all ({Rows} saved row(s)); the row is refused.",
					savedRow.Position.X,
					savedRow.Position.Y,
					geysers.Count);
				continue;
			}

			// Two shapes, told apart by the nearest distance: a live geyser sits inside the tolerance
			// and another row's claim took it (a pairing that is not one-to-one), or the nearest live
			// geyser is simply too far away (a shifted position or a live set that is not the cut's).
			if (nearestDistance[row] <= PositionTolerance)
			{
				log.LogWarning(
					"Restored geyser type row at ({X:F1},{Y:F1}) was not written: its nearest live geyser at ({X2:F1},{Y2:F1}) stands {Distance:F1} m away, inside the {Tolerance} m tolerance, and another row's claim took it; {Live} live geyser(s) against {Rows} saved row(s); the row is refused.",
					savedRow.Position.X,
					savedRow.Position.Y,
					nearestPoint[row].x,
					nearestPoint[row].y,
					nearestDistance[row],
					PositionTolerance,
					live.Length,
					geysers.Count);
			}
			else
			{
				log.LogWarning(
					"Restored geyser type row at ({X:F1},{Y:F1}) was not written: the nearest live geyser at ({X2:F1},{Y2:F1}) stands {Distance:F1} m away, outside the {Tolerance} m tolerance; {Live} live geyser(s) against {Rows} saved row(s); the row is refused.",
					savedRow.Position.X,
					savedRow.Position.Y,
					nearestPoint[row].x,
					nearestPoint[row].y,
					nearestDistance[row],
					PositionTolerance,
					live.Length,
					geysers.Count);
			}
		}

		var appliedRows = new List<int>();
		ContainedRowLoop.Run(
			assignments,
			assignment =>
			{
				var liquidType = geysers[assignment.Value].LiquidType;
				WriteLiquidType(assignment.Key, liquidType);
				var restored = RestoredTypes.GetValue(assignment.Key, _ => new RestoredType());
				restored.LiquidType = liquidType;
				restored.Restored = true;
				appliedRows.Add(assignment.Value);
				return true;
			},
			assignment => $"({assignment.Key.transform.position.x:F1},{assignment.Key.transform.position.y:F1})",
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
