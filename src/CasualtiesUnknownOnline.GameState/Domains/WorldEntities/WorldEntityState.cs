using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// The authoritative world-entity fact table for traps and building entities.
/// It is an immutable snapshot; domain reducers produce new snapshots so the
/// kernel can swap atomically after invariant validation.
/// </summary>
public sealed record WorldEntityState(
	IReadOnlyList<TrapConsumptionFact> Consumptions,
	IReadOnlyList<BuildingEntityHealthFact> BuildingHealth,
	IReadOnlyList<OpenedEntityFact> OpenedEntities)
{
	public static readonly WorldEntityState Empty = new([], [], []);

	public WorldEntityState WithConsumption(TrapConsumptionFact fact) =>
		this with
		{
			Consumptions = [.. Consumptions.Where(c => c.Position != fact.Position), fact],
		};

	public WorldEntityState WithBuildingHealth(BuildingEntityHealthFact fact) =>
		this with
		{
			BuildingHealth = [.. BuildingHealth.Where(h => h.Position != fact.Position), fact],
		};

	public WorldEntityState WithOpened(OpenedEntityFact fact) =>
		OpenedEntities.Any(o => o.Position == fact.Position)
			? this
			: this with { OpenedEntities = [.. OpenedEntities, fact] };
}
