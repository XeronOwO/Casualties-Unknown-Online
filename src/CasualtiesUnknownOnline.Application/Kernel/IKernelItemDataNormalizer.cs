using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The one conversion of the replication path this layer cannot own: a wire
/// item's data must reach the kernel through the same normalization the host's
/// own spawn commands take, and that normalization runs through the legacy
/// character-snapshot form the Runtime owns. Every other conversion the
/// replication surface needs is a pure kernel <-> wire mapping the layer now
/// performs itself (<see cref="KernelWireMapper"/>), so this port carries one
/// member instead of standing in for the whole mapper.
/// </summary>
public interface IKernelItemDataNormalizer
{
	/// <summary>
	/// The kernel data for an item state, normalized the way a spawn command's
	/// data is.
	/// </summary>
	ItemData ToKernelItemData(ItemState state);
}
