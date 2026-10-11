using CasualtiesUnknownOnline.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.ModEffects;

/// <summary>
/// What a registered item action landed on: the item the native call handed over,
/// the body the action was performed on, and — on the limb path — the limb it
/// named. It is the item-shaped counterpart of <see cref="ModLiquidEffectContext"/>,
/// and it reuses the very same body accessor, because the game's own
/// <c>useAction</c> / <c>useLimbAction</c> receive the same body a liquid's own
/// delegate does.
/// </summary>
internal sealed class ModItemActionContext : IModItemActionContext
{
	private readonly ModEffectBody _body;
	private readonly ModEffectItem _item;
	private readonly IModEffectLimb? _limb;

	internal ModItemActionContext(
		string modId,
		string itemId,
		Item item,
		Body body,
		Limb? limb,
		ulong ownerSteamId,
		ILogger log)
	{
		ItemId = itemId;
		OwnerSteamId = ownerSteamId;
		_body = new ModEffectBody(modId, body, log);
		_item = new ModEffectItem(item);

		// Through the body's own wrapper table, so a limb reached here is the same
		// accessor as the one reached from IModEffectBody.Limbs.
		_limb = limb == null ? null : _body.Wrap(limb); // Unity object — == (destroyed-object check)
	}

	/// <inheritdoc />
	public string ItemId { get; }

	/// <inheritdoc />
	public ulong OwnerSteamId { get; }

	/// <inheritdoc />
	public IModEffectItem Item => _item;

	/// <inheritdoc />
	public IModEffectBody Body => _body;

	/// <inheritdoc />
	public IModEffectLimb? Limb => _limb;

	/// <inheritdoc />
	public GameObject? NativeObject => _item.NativeObject;
}
