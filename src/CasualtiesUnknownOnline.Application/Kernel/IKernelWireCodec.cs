using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The wire codec for the kernel model: the conversions the replication surface
/// needs between the deterministic kernel types and their wire DTOs.
///
/// <para>
/// The conversions themselves are pure, but their implementation still lives
/// beside the legacy item-message bridge (the same mapper also carries the
/// legacy enemy-combat and character-snapshot branches, whose DTOs a version
/// adapter and the Game Adapter still speak), so this layer declares the
/// capability instead of reaching for that mapper directly.
/// </para>
/// </summary>
public interface IKernelWireCodec
{
	WireCommittedBatch ToWireBatch(CommittedBatch batch);

	CommittedBatch FromWireBatch(WireCommittedBatch batch, RunEpoch fallbackEpoch);

	GameCommand FromWireCommand(WireCommand command, EnvelopeHeader header);

	WireItem ToWireItem(ItemState state);

	ItemState FromWireItem(WireItem item);

	ItemIdentity FromWireIdentity(WireItemIdentity identity);

	ItemData FromWireData(WireItemData data);

	WireRandomStream ToWireRandomStream(RandomStreamState state);

	RandomStreamState FromWireRandomStream(WireRandomStream stream);

	/// <summary>
	/// The kernel data for an item state. The bridge normalizes the state through
	/// the legacy character-snapshot form (the same path the host's spawn commands
	/// take), so the result is the data a spawn command would carry.
	/// </summary>
	ItemData ToKernelItemData(ItemState state);
}
