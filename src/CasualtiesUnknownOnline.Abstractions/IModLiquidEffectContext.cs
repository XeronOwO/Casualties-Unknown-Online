using UnityEngine;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// What a registered liquid effect landed on and what it may write through. The
/// context is created per native call, on the affected player's OWN client — the
/// client that owns the body — and it stays valid only for that call (a timed body
/// started from it is the exception: its tick keeps the body accessor alive, so a
/// captured context is the supported way to keep writing).
///
/// <para>
/// The reachable objects are CUO-typed: <see cref="Body"/> and <see cref="Limb"/>
/// are the framework's own operation surfaces and never the game's types, so the
/// contract does not churn with a game update. The one engine-typed member is
/// <see cref="NativeObject"/>, the escape hatch.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModLiquidEffectContext
{
	/// <summary>The content id of the liquid this effect was registered for.</summary>
	string LiquidId { get; }

	/// <summary>
	/// The volume the native call carried, in the game's own millilitre unit — the
	/// same number the game's <c>onDrink</c> / <c>onHealthUse</c> delegates receive.
	/// The framework never scales it; a per-ml formula is the effect's own.
	/// </summary>
	float AmountMl { get; }

	/// <summary>
	/// The Steam id of the player whose body this is — always the LOCAL player, and
	/// read live at the moment of the call rather than from the bind-time session
	/// snapshot. It is 0 while the local identity is not established yet (a
	/// not-yet-activated IP-direct transport), which is the same value
	/// <see cref="ISessionInfo.LocalSteamId"/> carries then.
	/// </summary>
	ulong OwnerSteamId { get; }

	/// <summary>The named body operations. Never null.</summary>
	IModEffectBody Body { get; }

	/// <summary>
	/// The limb the effect LANDED on, or null on the drink path. It is the limb the
	/// native call carried — the effect sees exactly what the game's own
	/// <c>onHealthUse</c> delegate sees and nothing more — and the framework never
	/// invents one for it.
	///
	/// <para>
	/// One caveat this contract states rather than hides: the injection entry's own
	/// request may name NO limb (that is a legal value on its wire), and CUO's
	/// applier then resolves it with the rule the game's own injection path uses —
	/// the operator's pick when it is a real, attached limb of this body, otherwise
	/// the most injured attached one. So on that entry this member can be a limb the
	/// request did not name; the topical and limb-tool entries refuse such a request
	/// instead (decision 246). The accessor carries no flag saying which case it was,
	/// because the game's own delegate carries none either.
	/// </para>
	/// </summary>
	IModEffectLimb? Limb { get; }

	/// <summary>
	/// The engine object of the BODY this effect landed on — the same
	/// <c>GameObject</c> on both paths, so <c>NativeObject as Body</c> is the same
	/// cast whichever native delegate ran. The limb's own object, when there is one,
	/// is <see cref="IModEffectLimb.NativeObject"/>. The escape hatch for an author
	/// who wants the game's own objects: every body and limb in this game is a
	/// <c>MonoBehaviour</c>, so the cast or a <c>GetComponent</c> recovers them.
	///
	/// <para>
	/// Its rules, which are the price of using it: it is the object of the state
	/// THIS client owns (a fact about another player is never read here); Unity null
	/// semantics apply, so test it with <c>== null</c>; and a mod that uses it is
	/// outside this API's promise — it declares what it binds
	/// (<see cref="CuoModAttribute.NativeBinding"/>) like any other native binding,
	/// because what it names belongs to the game and a game update may move it with
	/// no CUO decision.
	/// </para>
	/// </summary>
	[ApiStability(ApiStabilityLevel.Advanced)]
	GameObject? NativeObject { get; }
}
