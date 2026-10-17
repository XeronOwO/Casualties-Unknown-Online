using CasualtiesUnknownOnline.Application.Kernel;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The game's own item data behind the native-use bound: the answer the Admission
/// seam's <see cref="IKernelItemUseFacts"/> port asks, read from the registry the
/// game builds at startup (<c>Item.GlobalItems</c>, keyed by the definition id the
/// item names).
///
/// <para>
/// The one flag is <c>ItemInfo.usable</c>, because it is the guard both native use
/// call sites run in front of their delegate (<c>Body.UseItemInHand</c> and
/// <c>Body.UseItem</c>). A definition whose flag is clear cannot have produced a
/// use report on any client of this game — the call would have taken another
/// branch — so that is the one answer the host refuses on.
/// </para>
///
/// <para>
/// An id this registry does not carry answers <see cref="ItemUsePermission.Unknown"/>,
/// not a refusal: the host's registry is its own build's, a mod item the host does
/// not have still exists on the member that reported it, and refusing on a
/// definition this side cannot read would refuse a legitimate report. The case is
/// logged at Debug because it is expected for exactly that mod-content shape and
/// uninteresting for the judgement.
/// </para>
/// </summary>
internal sealed class GameItemUseFacts(ILogger<GameItemUseFacts> log) : IKernelItemUseFacts
{
	private readonly ILogger<GameItemUseFacts> _log = log;

	public ItemUsePermission NativeUse(string definitionId)
	{
		if (string.IsNullOrEmpty(definitionId) || !Item.GlobalItems.TryGetValue(definitionId, out var info) || info is null)
		{
			_log.LogDebug("[ItemUseFacts] definition '{Definition}' is not in this game's item registry — the use report cannot be judged here and is admitted.", definitionId);
			return ItemUsePermission.Unknown;
		}

		return info.usable ? ItemUsePermission.Permitted : ItemUsePermission.Forbidden;
	}
}
