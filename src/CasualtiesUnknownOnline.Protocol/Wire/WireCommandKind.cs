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

	// Protocol control (not a gameplay command)
	RangeRequest = 100,
	CommandRejected = 101,
}
