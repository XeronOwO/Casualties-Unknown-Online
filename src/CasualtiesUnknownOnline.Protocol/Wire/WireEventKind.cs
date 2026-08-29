namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Discriminator for the typed kernel item facts carried by
/// <see cref="CommittedBatchEnvelope"/>. These are domain facts, not Harmony
/// hook names.
/// </summary>
public enum WireEventKind
{
	ItemSpawned = 1,
	ItemRelocated = 2,
	ItemDestroyed = 3,
	ItemDataUpdated = 4,
	RunStarted = 5,
	RunAdvanced = 6,
	TrapConsumed = 7,
	BuildingEntityHealthUpdated = 8,
	OpenedEntity = 9,
}
