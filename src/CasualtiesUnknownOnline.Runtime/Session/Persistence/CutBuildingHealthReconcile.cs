using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The cut's building-health rows against the LIVE world at the cut instant.
///
/// The kernel's fact for a building is the health the blast reported when it landed.
/// The game can destroy that building afterwards — a blast that removes its support
/// kills it through the game's own support loss, which reports nothing — so the
/// kernel keeps a positive health for an entity the world no longer has, and the cut
/// then names an entity that will not be there when the restore looks it up. Measured
/// 2026-11-17: a cut carried health 1437.9 at cell (5,492) whose building the blast
/// had already felled; the continue reported the row as refused and the world as
/// incomplete, so the save was not trustworthy even though the world it described was
/// correct.
///
/// The cut therefore states what the world shows: the live health when the entity
/// stands, and zero (destroyed) when it is gone. A cut taken with no live world cannot
/// answer the question at all, and keeps the kernel's facts untouched rather than
/// recording every named building as destroyed — <see cref="INativeWorldFacts.CanReadLiveBuildings"/>
/// is that distinction.
/// </summary>
internal static class CutBuildingHealthReconcile
{
	/// <summary>Replace the checkpoint's building-health rows with the live world's reading; a checkpoint that names no building is returned unchanged.</summary>
	internal static GameCheckpoint Apply(GameCheckpoint checkpoint, INativeWorldFacts? native, WorldCutReason reason, ILogger log)
	{
		if (native is not { CanReadLiveBuildings: true }
			|| checkpoint.WorldEntities is not { BuildingHealth.Count: > 0 } entities)
		{
			return checkpoint;
		}

		List<BuildingEntityHealthFact> rows = [];
		var destroyed = 0;
		var moved = 0;
		foreach (var fact in entities.BuildingHealth)
		{
			var live = native.LiveBuildingHealthAt(fact.Position.CenterX, fact.Position.CenterY);
			if (live is null)
			{
				rows.Add(fact with { Health = 0 });
				destroyed++;
				continue;
			}

			if (live.Value != fact.Health)
			{
				moved++;
			}

			rows.Add(live.Value == fact.Health ? fact : fact with { Health = live.Value });
		}

		if (destroyed > 0)
		{
			log.LogInformation(
				"Cut {Reason} reconciled the building-health rows against the live world: {Destroyed} named building(s) no longer stand there and are recorded destroyed (health 0).",
				reason,
				destroyed);
		}

		if (moved > 0)
		{
			log.LogInformation(
				"Cut {Reason} reconciled the building-health rows against the live world: {Moved} row(s) carry the health the world shows now, not the last reported value.",
				reason,
				moved);
		}

		return checkpoint with { WorldEntities = entities with { BuildingHealth = rows } };
	}
}
