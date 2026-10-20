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
	ItemApplyLimb = 27,
	ItemRemoveLimb = 28,

	/// <summary>
	/// A CONSOLE command wrote one item's state (<c>/data set</c>). The payload is
	/// the same save-shaped state an <see cref="ItemUpdateState"/> report carries —
	/// the row it writes is the row the game would have written — and the kind
	/// exists so the host can tell a console write from a native use before it
	/// judges the report: a native use is a gesture the player performed, a console
	/// write is an operator verb the host's own rule admits or refuses.
	/// </summary>
	ItemDataWrite = 29,

	/// <summary>
	/// A NATIVE USE of one item ran on the reporting member's own client, through one of
	/// the game's two guarded use call sites, and this is the state it produced. Separate
	/// from <see cref="ItemUpdateState"/> for the same reason the console write is: the
	/// host judges a USE by the item's own data (<c>ItemInfo.usable</c>, the flag both of
	/// those call sites require), and a slot move, a state drift or a gun's rack/safety
	/// transition through that kind claims no use at all, so one kind could not carry both
	/// answers.
	/// </summary>
	ItemUse = 30,

	/// <summary>
	/// A member took ONE item OUT OF A CONTAINER and into its own carried inventory. Its
	/// kernel fact is the same relocation <see cref="ItemPickup"/> states — an item that is
	/// now carried by the sender — and the kind exists because the two reports settle
	/// differently when the host refuses: a refused pickup is a world item the sender may not
	/// have (its local copy goes back to the world), while a refused take-out is an item the
	/// sender is HOLDING (its way back is the carried-set statement, and unparenting it into
	/// the world would be the opposite of a settlement).
	/// <para>
	/// The report exists because a container's children are the kernel's rows for what is
	/// inside it: an item taken out of a container is still recorded as contained there, and
	/// the container's own report cannot state the departure (it names what the container
	/// holds, and the kernel destroys the children a report leaves out). The arrival is the
	/// fact the sender can state.
	/// </para>
	/// </summary>
	ItemContainerTakeOut = 31,

	// Protocol control (not a gameplay command)
	RangeRequest = 100,
	CommandRejected = 101,
}
