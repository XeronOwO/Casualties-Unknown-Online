namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Discriminator for the typed item command payloads carried by
/// <see cref="CommandEnvelope"/>.
/// </summary>
public enum WireCommandKind
{
	ItemSpawn = 1,
	ItemPickup = 2,
	ItemDrop = 3,
	ItemDestroy = 4,
	ItemUpdateState = 5,
	ItemTransfer = 6,
	ItemContainerSync = 7,
	RunStart = 8,
	AdvanceLayer = 9,
	RecordTrapConsumed = 10,
	RecordBuildingEntityHealth = 11,
	RecordOpenedEntity = 12,
	ResetWorldEntities = 13,
	UpdatePlayerStatus = 14,
	ResetPlayers = 15,
	UpsertEnemy = 16,
	RemoveEnemy = 17,
	ResetEnemies = 18,
	UpdateFluidRegion = 19,
	ResetFluids = 20,
	SetPlayerCarry = 21,
	ClearPlayerCarry = 22,

	// Protocol control (not a gameplay command)
	RangeRequest = 100,
	CommandRejected = 101,
}
