using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The Runtime's answer to the Application layer's item-data normalizer: the one
/// conversion of the replication path that runs through the legacy
/// character-snapshot form, so it cannot live beside the pure kernel <-> wire
/// mapper the layer now owns.
/// </summary>
internal sealed class KernelItemDataNormalizer : IKernelItemDataNormalizer
{
	public ItemData ToKernelItemData(ItemState state) =>
		ItemKernelAuthority.ToKernelData(ItemKernelAuthority.ToCharacterItem(state));
}
