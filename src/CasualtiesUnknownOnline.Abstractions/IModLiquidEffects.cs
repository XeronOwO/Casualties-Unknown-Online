namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored liquid effect registration face. A function is not data and
/// cannot ride a content payload, so it is registered in CODE, keyed by the id of
/// a liquid THIS mod registered through <see cref="IModContent"/>: what travels
/// between peers stays the liquid's data (id, colour, value, flags, qualities),
/// and the behaviour is code each peer already has, looked up by the id it was
/// registered under.
///
/// <para>
/// Registration requires <see cref="ModPermission.AuthorEffects"/>. The liquid's
/// content MUST be registered first — an effect whose liquid this mod has not
/// registered as content is refused, because the id is the only thing that binds
/// the two and an unowned id would silently author behaviour for content another
/// mod (or the game) materialized. Only the liquid's OWNER may author its effect;
/// authoring one for a vanilla liquid is outside this surface.
/// </para>
///
/// <para>
/// The two native delegates are registered separately, because they are separate
/// call sites with different arguments: <see cref="TryRegisterDrink"/> stands
/// where <c>LiquidType.onDrink</c> stands (a body and a volume) and
/// <see cref="TryRegisterLimbUse"/> where <c>LiquidType.onHealthUse</c> stands (a
/// limb and a volume), which is also the delegate both the topical apply and the
/// injection run. Registering one and not the other is a supported shape: the
/// unregistered half keeps the framework's reporting no-op, so the action is
/// well-defined rather than a null the game would throw on.
/// </para>
///
/// <para>
/// One more property of this surface: it is a LOCAL registration on each peer's
/// own copy of the mod. What makes two peers agree is the handshake — id, version
/// and permission parity for a mod both sides list — and the affected side judging
/// on its own picture and timeline, never the sender's.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModLiquidEffects
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.AuthorEffects"/>.
	/// Every registration call also checks and logs this before acting.
	/// </summary>
	bool CanRegister { get; }

	/// <summary>
	/// Try to register the drink effect of one liquid this mod registered as
	/// content. Returns false (with a framework log) when the mod lacks
	/// <see cref="ModPermission.AuthorEffects"/>, the id is not a valid content id,
	/// the mod has not registered a LIQUID definition with that id, the effect is
	/// null, or this mod already registered a drink effect for that id — a
	/// re-registration is refused rather than replaced, so two call sites can never
	/// disagree about which function runs.
	/// </summary>
	/// <param name="liquidId">The content id of the liquid this effect belongs to.</param>
	/// <param name="effect">The function the game's own drink call site runs.</param>
	bool TryRegisterDrink(string liquidId, ModLiquidEffect effect);

	/// <summary>
	/// Try to register the limb effect of one liquid this mod registered as
	/// content — the delegate behind the topical apply AND the injection, exactly
	/// as the game's own <c>onHealthUse</c> is. Refused on the same rails as
	/// <see cref="TryRegisterDrink"/>, independently of it, with the limb half's own
	/// reachability rule: the definition must declare
	/// <see cref="IModLiquidDefinition.HealthUsable"/> or
	/// <see cref="IModLiquidDefinition.Injectable"/>, because the game runs
	/// <c>onHealthUse</c> behind exactly those two flags and a liquid declaring
	/// neither reaches no call site at all. The drink half has no such rule — the
	/// game's own drink site is gated by no flag of the liquid's.
	/// </summary>
	/// <param name="liquidId">The content id of the liquid this effect belongs to.</param>
	/// <param name="effect">The function the game's own limb-use call sites run.</param>
	bool TryRegisterLimbUse(string liquidId, ModLiquidEffect effect);
}
