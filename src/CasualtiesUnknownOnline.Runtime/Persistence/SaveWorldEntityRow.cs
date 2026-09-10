using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One row of <c>world-entities.json</c> (§3.4). The kernel's world-entity
/// fact table holds four kinds of fact (trap consumption, building-entity
/// health, opened lockable entities, trap state machines), so its save file is
/// a typed row list rather than one opaque blob: §6's salvage is per entry, and
/// a row that cannot be materialized is skipped by itself.
/// </summary>
public sealed class SaveWorldEntityRow
{
	/// <summary>The row kind: <c>trap-consumption</c>, <c>building-health</c>, <c>opened-entity</c> or <c>trap-state</c>.</summary>
	public string Kind { get; init; } = string.Empty;

	public WireTrapConsumption? TrapConsumption { get; init; }

	public WireBuildingEntityHealth? BuildingHealth { get; init; }

	public WireOpenedEntity? OpenedEntity { get; init; }

	public WireTrapState? TrapState { get; init; }

	public static SaveWorldEntityRow OfConsumption(TrapConsumptionFact fact) =>
		new() { Kind = TrapConsumptionKind, TrapConsumption = KernelDomainWireMapper.ToWireTrapConsumption(fact) };

	public static SaveWorldEntityRow OfBuildingHealth(BuildingEntityHealthFact fact) =>
		new() { Kind = BuildingHealthKind, BuildingHealth = KernelDomainWireMapper.ToWireBuildingEntityHealth(fact) };

	public static SaveWorldEntityRow OfOpenedEntity(OpenedEntityFact fact) =>
		new() { Kind = OpenedEntityKind, OpenedEntity = KernelDomainWireMapper.ToWireOpenedEntity(fact) };

	public static SaveWorldEntityRow OfTrapState(TrapStateFact fact) =>
		new() { Kind = TrapStateKind, TrapState = KernelDomainWireMapper.ToWireTrapStateFact(fact) };

	/// <summary>The row's identity for the damage report: the fact's position, or its kind when it has no position.</summary>
	public string Describe() => Kind switch
	{
		TrapConsumptionKind => $"trap-consumption at {Position(TrapConsumption?.Position)}",
		BuildingHealthKind => $"building-health at {Position(BuildingHealth?.Position)}",
		OpenedEntityKind => $"opened-entity at {Position(OpenedEntity?.Position)}",
		TrapStateKind => $"trap-state at {Position(TrapState?.Position)}",
		_ => $"<{Kind}>",
	};

	private static string Position(WireEntityPosition? position) =>
		position is null ? "<no position>" : $"({position.X},{position.Y})";

	public const string TrapConsumptionKind = "trap-consumption";
	public const string BuildingHealthKind = "building-health";
	public const string OpenedEntityKind = "opened-entity";
	public const string TrapStateKind = "trap-state";
}
