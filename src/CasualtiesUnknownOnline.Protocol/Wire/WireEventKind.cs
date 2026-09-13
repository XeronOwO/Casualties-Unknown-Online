namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Discriminator for the typed kernel item facts carried by
/// <see cref="CommittedBatchEnvelope"/>. These are domain facts, not Harmony
/// hook names.
///
/// A layer-boundary reset has a wire form here even though its COMMAND does not (see
/// <see cref="WireCommandKind"/>): only the host may trigger one, but the guests must
/// still learn that the host's layer-scoped tables started empty — their kernels are
/// the host's replay. Without these the guest would keep applying foreign-layer rows
/// until its next checkpoint.
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
	WorldEntitiesReset = 10,
	PlayerStatusUpdated = 11,
	EnemyUpserted = 13,
	EnemyRemoved = 14,
	EnemiesReset = 15,
	FluidRegionUpdated = 16,
	FluidsReset = 17,
	PlayerCarrySet = 18,
	PlayerCarryCleared = 19,
	PlayerInventoryTransfer = 20,
	PlayerHealResult = 21,
	PlayerItemUseResult = 22,
	EnemyBiteResult = 23,
	EnemyLungeResult = 24,
	EnemyEffectResult = 25,
	TrapStateChanged = 26,
	WorldItemsReset = 27,
}
