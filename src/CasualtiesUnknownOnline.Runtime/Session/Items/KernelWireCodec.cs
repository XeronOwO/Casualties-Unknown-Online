using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The Runtime's answer to the Application layer's kernel wire codec: a thin
/// adapter over the static <see cref="KernelWireMapper"/> and the legacy
/// item-message bridge it still owns. Every conversion the replication surface
/// needs is a direct call — the mapper keeps its legacy branches (the enemy
/// combat messages and the character-snapshot form a version adapter and the
/// Game Adapter still speak), and those branches are exactly why the mapper has
/// not moved with the replication surface.
/// </summary>
internal sealed class KernelWireCodec : IKernelWireCodec
{
	public WireCommittedBatch ToWireBatch(CommittedBatch batch) => KernelWireMapper.ToWireBatch(batch);

	public CommittedBatch FromWireBatch(WireCommittedBatch batch, RunEpoch fallbackEpoch) => KernelWireMapper.FromWireBatch(batch, fallbackEpoch);

	public GameCommand FromWireCommand(WireCommand command, EnvelopeHeader header) => KernelWireMapper.FromWireCommand(command, header);

	public WireItem ToWireItem(ItemState state) => KernelWireMapper.ToWireItem(state);

	public ItemState FromWireItem(WireItem item) => KernelWireMapper.FromWireItem(item);

	public ItemIdentity FromWireIdentity(WireItemIdentity identity) => KernelWireMapper.FromWireIdentity(identity);

	public ItemData FromWireData(WireItemData data) => KernelWireMapper.FromWireData(data);

	public WireRandomStream ToWireRandomStream(RandomStreamState state) => KernelWireMapper.ToWireRandomStream(state);

	public RandomStreamState FromWireRandomStream(WireRandomStream stream) => KernelWireMapper.FromWireRandomStream(stream);

	public ItemData ToKernelItemData(ItemState state) =>
		ItemKernelAuthority.ToKernelData(ItemKernelAuthority.ToCharacterItem(state));
}
