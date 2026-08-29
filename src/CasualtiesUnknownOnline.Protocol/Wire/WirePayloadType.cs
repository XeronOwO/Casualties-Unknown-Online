namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Numeric payload identity for the Phase C protocol. Critical kernel payloads
/// use low, stable IDs; non-critical presentation effects are reserved in the
/// upper range. Receivers reject unknown critical payloads and ignore unknown
/// non-critical effects.
/// </summary>
public enum WirePayloadType
{
	// Commands (critical)
	ItemSpawnCommand = 1,
	ItemPickupCommand = 2,
	ItemDropCommand = 3,
	ItemDestroyCommand = 4,
	ItemUpdateStateCommand = 5,
	ItemTransferCommand = 6,
	ItemContainerSyncCommand = 7,
	RunStartCommand = 8,
	AdvanceLayerCommand = 9,
	RecordTrapConsumedCommand = 10,
	RecordBuildingEntityHealthCommand = 11,
	RecordOpenedEntityCommand = 12,
	ResetWorldEntitiesCommand = 13,
	UpdatePlayerStatusCommand = 14,
	ResetPlayersCommand = 15,
	UpsertEnemyCommand = 16,
	RemoveEnemyCommand = 17,
	ResetEnemiesCommand = 18,
	UpdateFluidRegionCommand = 19,
	ResetFluidsCommand = 20,
	SetPlayerCarryCommand = 21,
	ClearPlayerCarryCommand = 22,

	// Kernel events (critical)
	ItemSpawnedEvent = 101,
	ItemRelocatedEvent = 102,
	ItemDestroyedEvent = 103,
	ItemDataUpdatedEvent = 104,
	RunStartedEvent = 105,
	RunAdvancedEvent = 106,
	TrapConsumedEvent = 107,
	BuildingEntityHealthUpdatedEvent = 108,
	OpenedEntityEvent = 109,
	WorldEntitiesResetEvent = 110,
	PlayerStatusUpdatedEvent = 111,
	PlayersResetEvent = 112,
	EnemyUpsertedEvent = 113,
	EnemyRemovedEvent = 114,
	EnemiesResetEvent = 115,
	FluidRegionUpdatedEvent = 116,
	FluidsResetEvent = 117,
	PlayerCarrySetEvent = 118,
	PlayerCarryClearedEvent = 119,

	// Envelope families
	CommittedBatch = 100,

	// Checkpoint / streams
	CheckpointChunk = 201,
	RangeRequestCommand = 202,
	CommandRejected = 203,
	StateStream = 301,
	ItemSnapshotStream = 302,
	WorldItemsSnapshotStream = 303,
	PlayerStateStream = 304,
	EnemyStateStream = 305,

	// Non-critical presentation effects (reserved)
	PresentationEffect = 1001,
}
