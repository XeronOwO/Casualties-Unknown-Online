namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored item action registration face: the function behind an item's
/// own <c>ItemInfo.useAction</c> / <c>ItemInfo.useLimbAction</c> delegate. It is
/// the item half of the surface <see cref="IModLiquidEffects"/> opened for
/// liquids, on the same reasoning — a function is not data and cannot ride a
/// content payload, so it is registered in CODE, keyed by the id of an item THIS
/// mod registered through <see cref="IModContent"/>. What travels between peers
/// stays the item's data; the behaviour is code each peer already has, looked up
/// by the id it was registered under.
///
/// <para>
/// Registration requires <see cref="ModPermission.AuthorEffects"/>, the same bit
/// the liquid half uses — it is the "this mod authors behaviour" permission, not a
/// per-content-kind one. The item's content MUST be registered first, and only its
/// OWNER may author its action: an id this mod has not registered would silently
/// author behaviour for content another mod (or the game) materialized, and
/// authoring one for a vanilla item is a content override, outside this surface.
/// </para>
///
/// <para>
/// The two halves are separate registrations because they are separate native call
/// sites with different arguments: <see cref="TryRegisterUse"/> stands where
/// <c>ItemInfo.useAction</c> stands (the using body and the item) and
/// <see cref="TryRegisterLimbUse"/> where <c>ItemInfo.useLimbAction</c> stands
/// (the treated limb and the item). Registering one and not the other is a
/// supported shape.
/// </para>
///
/// <para>
/// Two rails are the item side's own, because both native calls sit behind a flag
/// on the definition and a registration the game's gate can never reach is dead
/// code: an action is refused unless the definition declares the flag that half is
/// gated on (<see cref="IModItemDefinition.Usable"/> /
/// <see cref="IModItemDefinition.UsableWithLmb"/> for the use half,
/// <see cref="IModItemDefinition.UsableOnLimb"/> for the limb half), and the use
/// half is refused for an item whose definition already carries a use action of its
/// own (<see cref="IModItemDefinition.Tool"/> / <see cref="IModItemDefinition.Gun"/>)
/// — one item, one action, decided by the definition's own data rather than by
/// which registration happened to run.
/// </para>
///
/// <para>
/// One more property of this surface: it is a LOCAL registration on each peer's own
/// copy of the mod. What makes two peers agree is the handshake — id, version and
/// permission parity for a mod both sides list — and the affected side judges on
/// its own picture and timeline, never the sender's.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModItemActions
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.AuthorEffects"/>.
	/// Every registration call also checks and logs this before acting.
	/// </summary>
	bool CanRegister { get; }

	/// <summary>
	/// Try to register the use action of one item this mod registered as content —
	/// the delegate <c>Body.UseItem</c> / <c>Body.UseItemInHand</c> run. Returns
	/// false (with a framework log) when the mod lacks
	/// <see cref="ModPermission.AuthorEffects"/>, the id is not a valid content id,
	/// the mod has not registered an ITEM definition with that id, the definition
	/// does not declare <see cref="IModItemDefinition.Usable"/> — which is the flag
	/// BOTH of the game's use call sites require;
	/// <see cref="IModItemDefinition.UsableWithLmb"/> only adds the left-mouse entry
	/// point and is not a substitute — the definition carries a
	/// <see cref="IModItemDefinition.Tool"/> or <see cref="IModItemDefinition.Gun"/>
	/// use action of its own, the action is null, or this mod already registered a
	/// use action for that id — a re-registration is refused rather than replaced,
	/// so two call sites can never disagree about which function runs.
	/// </summary>
	/// <param name="itemId">The content id of the item this action belongs to.</param>
	/// <param name="action">The function the game's own use call sites run.</param>
	bool TryRegisterUse(string itemId, ModItemAction action);

	/// <summary>
	/// Try to register the limb action of one item this mod registered as content —
	/// the delegate the game's own wound view runs when the item is released onto a
	/// limb, and the one CUO's cross-player limb-tool chain runs on the treated
	/// player's own client. Refused on the same rails as
	/// <see cref="TryRegisterUse"/>, independently of it, with the limb half's own
	/// flag: the definition must declare
	/// <see cref="IModItemDefinition.UsableOnLimb"/>, which is also what makes the
	/// game offer the item on a limb at all.
	/// </summary>
	/// <param name="itemId">The content id of the item this action belongs to.</param>
	/// <param name="action">The function the game's own limb-use call sites run.</param>
	bool TryRegisterLimbUse(string itemId, ModItemAction action);
}
