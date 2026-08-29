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

	// Envelope families
	CommittedBatch = 100,

	// Checkpoint / streams
	CheckpointChunk = 201,
	RangeRequestCommand = 202,
	CommandRejected = 203,
	StateStream = 301,
	ItemSnapshotStream = 302,
	WorldItemsSnapshotStream = 303,

	// Non-critical presentation effects (reserved)
	PresentationEffect = 1001,
}
