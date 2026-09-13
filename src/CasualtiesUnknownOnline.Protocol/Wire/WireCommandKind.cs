namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Discriminator for the typed item command payloads carried by
/// <see cref="CommandEnvelope"/>.
///
/// The LAYER-SCOPED kernel resets are deliberately absent: the world-item, the
/// world-entity, the enemy and the fluid resets all run host-locally at the
/// generation boundary (<c>WorldService.ResetWorldLayerTables</c>, the last two
/// through <c>LayerScopedTableReset</c>), because a remotely triggerable "wipe the
/// layer's table" is a destructive trigger no peer may have. The received-command
/// path carries no role check — <c>KernelProtocolCommandHandler</c> executes what
/// the envelope names and the kernel's <c>CommandContext</c> has no role — so the
/// wire identity IS the gate: an unmapped kind cannot be reconstructed by
/// <c>KernelWireMapper.FromWireCommand</c> at all. Guests converge from the host's
/// committed batches and checkpoints instead.
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
	UpdatePlayerStatus = 14,
	UpsertEnemy = 16,
	RemoveEnemy = 17,
	UpdateFluidRegion = 19,
	SetPlayerCarry = 21,
	ClearPlayerCarry = 22,
	RecordEnemyBite = 23,
	RecordEnemyLunge = 24,
	RecordEnemyEffect = 25,
	RecordTrapState = 26,

	// Protocol control (not a gameplay command)
	RangeRequest = 100,
	CommandRejected = 101,
}
