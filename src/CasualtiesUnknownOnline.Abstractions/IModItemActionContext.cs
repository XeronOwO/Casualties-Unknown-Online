using UnityEngine;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// What a registered item action landed on and what it may write through. The
/// context is created per native call, on the client whose body the action was
/// performed on, and it stays valid only for that call.
///
/// <para>
/// The reachable objects are CUO-typed: <see cref="Item"/>, <see cref="Body"/> and
/// <see cref="Limb"/> are the framework's own operation surfaces and never the
/// game's types, so the contract does not churn with a game update. The one
/// engine-typed member is <see cref="NativeObject"/>, the escape hatch.
/// </para>
///
/// <para>
/// The body accessor is the one the liquid effects use
/// (<see cref="IModEffectBody"/>), because it is the same body either way: the
/// game's own <c>useAction</c> and <c>useLimbAction</c> receive the body (or the
/// limb's body), and the effects an item performs on it are the effects the game's
/// own items perform.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModItemActionContext
{
	/// <summary>The content id of the item this action was registered for.</summary>
	string ItemId { get; }

	/// <summary>
	/// The Steam id of the player whose body the action was performed on — always
	/// the LOCAL player, and read live at the moment of the call rather than from
	/// the bind-time session snapshot. It is 0 while the local identity is not
	/// established yet (a not-yet-activated IP-direct transport), which is the same
	/// value <see cref="ISessionInfo.LocalSteamId"/> carries then.
	/// </summary>
	ulong OwnerSteamId { get; }

	/// <summary>The named item operations. Never null.</summary>
	IModEffectItem Item { get; }

	/// <summary>The named body operations, on the body the native call handed over. Never null.</summary>
	IModEffectBody Body { get; }

	/// <summary>
	/// The limb the action LANDED on, or null on the use path. It is the limb the
	/// native call carried — the action sees exactly what the game's own
	/// <c>useLimbAction</c> delegate sees and nothing more — and the framework never
	/// invents one for it.
	/// </summary>
	IModEffectLimb? Limb { get; }

	/// <summary>
	/// The engine object of the ITEM this action landed on, so
	/// <c>NativeObject as Item</c> is the game's own item component and a
	/// <c>GetComponent</c> reaches whatever else the prefab carries. This is the
	/// escape hatch for an author who wants the game's own objects; the body's
	/// object is <see cref="IModEffectBody.NativeObject"/> and the limb's is
	/// <see cref="IModEffectLimb.NativeObject"/>.
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
