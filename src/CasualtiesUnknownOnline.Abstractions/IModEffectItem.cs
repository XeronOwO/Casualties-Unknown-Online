using UnityEngine;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The item a mod-authored action landed on, reached through named operations. It
/// is the item half of what <see cref="IModEffectBody"/> / <see cref="IModEffectLimb"/>
/// are for the body: the whole write surface an action has on the object it was
/// handed, and the only one besides the engine-typed escape hatch.
///
/// <para>
/// Every member is grown from a write the game's own shipped items perform on
/// <c>Item.condition</c> — the one item field the 195 delegates in the game's own
/// item registry actually write (78 of the use actions and 22 of the limb actions
/// adjust it, and one item drives <c>decayMultiplier</c>, which is a mechanism
/// rather than an effect). Everything else those delegates touch on the item is
/// engine work rather than state — a sprite swap, a sound, a replacement object —
/// and is reached through <see cref="IModItemActionContext.NativeObject"/>.
/// </para>
///
/// <para>
/// The write goes through the game's own <c>Item.SetCondition</c> rather than the
/// field, because that method is not a plain assignment: a liquid container scaled
/// to a new condition drains the difference, and the game's own items that change
/// condition go through it for that reason.
/// </para>
///
/// <para>
/// Level <see cref="ApiStabilityLevel.Advanced"/>: this surface follows the game's
/// own item field, so a game update that moves it may move this with no CUO
/// decision involved.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Advanced)]
public interface IModEffectItem
{
	/// <summary>
	/// The item's condition, in the game's own unit: <c>1</c> is pristine and
	/// <c>0</c> is worn out. An item whose definition declares
	/// <see cref="IModItemDefinition.DestroyAtZeroCondition"/> is destroyed by the
	/// game when it reaches zero, exactly as a vanilla item is — an action that
	/// spends condition can therefore end the object it is writing.
	/// </summary>
	float Condition { get; }

	/// <summary>
	/// Adds to the item's condition — the shape the game's own items use
	/// (<c>item.condition -= 0.34f</c>) and the one an action that costs durability
	/// wants. Negative spends, positive restores; the framework does not clamp, and
	/// the game's own readers are what bound it.
	/// </summary>
	/// <param name="amount">The condition delta, in the game's own unit.</param>
	void AddCondition(float amount);

	/// <summary>
	/// Sets the item's condition outright, through the game's own
	/// <c>Item.SetCondition</c>. Use it where a value is pinned rather than
	/// accumulated; a liquid container scaled down this way drains to match, which
	/// is the game's own behaviour and not a framework rule.
	/// </summary>
	/// <param name="value">The new condition, in the game's own unit.</param>
	void SetCondition(float value);

	/// <summary>
	/// The engine object of this item — the same object
	/// <see cref="IModItemActionContext.NativeObject"/> carries, offered here so an
	/// accessor handed to a helper keeps its own way back to the game's object. Same
	/// rules: this client's own object, Unity null semantics, and a mod that uses it
	/// declares the native binding it makes.
	/// </summary>
	[ApiStability(ApiStabilityLevel.Advanced)]
	GameObject? NativeObject { get; }
}
