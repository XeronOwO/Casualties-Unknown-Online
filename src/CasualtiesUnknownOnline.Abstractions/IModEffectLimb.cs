using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The limb a liquid effect was applied to, reached through named operations. It
/// is present only on the limb path: the native <c>onHealthUse</c> delegate — and
/// therefore both the topical apply and the injection — receives a limb, and the
/// drink path receives a body alone.
///
/// <para>
/// Every member is grown from a write the game's own shipped liquids perform on a
/// limb (the census in <c>docs/backlog/review/mod-authored-effects.md</c>), and the
/// game's own semantics are what runs: <see cref="Disinfect"/> is the game's own
/// <c>Limb.SetDisinfect</c>, not a field write, and the vitals are the fields the
/// game's own formulas touch. Two of them are reached from the drink path as well
/// as the limb path in the game's own code (<c>bleach</c> wears
/// <c>body.limbs[0].skinHealth</c> down from a drink tick, and <c>procoagulant</c>
/// scales every limb's <c>bleedAmount</c> from both) — the accessor is the same one
/// either way, because it is the limb this effect landed on.
/// </para>
///
/// <para>
/// Level <see cref="ApiStabilityLevel.Advanced"/>: this surface follows the game's
/// own limb fields, so a game update that moves one may move this with no CUO
/// decision involved.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Advanced)]
public interface IModEffectLimb
{
	/// <summary>
	/// This limb's index in the body's own limb array — the same index
	/// <see cref="IModEffectBody.Limbs"/> and the game's own <c>body.limbs[i]</c>
	/// use, so an effect can tell the two apart and write the one it means.
	/// </summary>
	int Index { get; }

	/// <summary>The limb's own display name, as the game's own timed-op keys and UI use it.</summary>
	string Name { get; }

	/// <summary>Whether the game classifies this limb as an arm.</summary>
	bool IsArm { get; }

	/// <summary>Whether the game classifies this limb as a leg.</summary>
	bool IsLeg { get; }

	/// <summary>Whether this is the head.</summary>
	bool IsHead { get; }

	/// <summary>Whether the game treats this limb as vital.</summary>
	bool IsVital { get; }

	/// <summary>The body this limb belongs to — the same accessor the context carries.</summary>
	IModEffectBody Body { get; }

	/// <summary>
	/// The engine object of THIS limb — the counterpart of
	/// <see cref="IModLiquidEffectContext.NativeObject"/>, which is always the
	/// body's. Same rules: this client's own object, Unity null semantics, and a mod
	/// that uses it declares the native binding it makes.
	/// </summary>
	[ApiStability(ApiStabilityLevel.Advanced)]
	GameObject? NativeObject { get; }

	/// <summary>
	/// The limbs the game connects to this one, in the game's own order — what
	/// <c>antiserum</c>'s own effect walks to disinfect a whole region. Each entry
	/// is a live accessor; the list itself is a snapshot of the array.
	/// </summary>
	IReadOnlyList<IModEffectLimb> ConnectedLimbs { get; }

	/// <summary>
	/// Reads one limb vital. An undefined <paramref name="vital"/> returns 0 and logs.
	/// </summary>
	float GetVital(ModLimbVital vital);

	/// <summary>
	/// Adds to one limb vital. An undefined <paramref name="vital"/> is refused and logs.
	/// </summary>
	void AddVital(ModLimbVital vital, float amount);

	/// <summary>
	/// Sets one limb vital outright — how the game pins a pain value
	/// (<c>limb.pain *= 0.9f</c> is the read-modify-write form of the same thing).
	/// Same refusal rule as <see cref="AddVital"/>.
	/// </summary>
	void SetVital(ModLimbVital vital, float value);

	/// <summary>
	/// Runs the game's own <c>Limb.SetDisinfect</c> — the disinfection value
	/// <c>Liquids.cs</c>'s alcohol, bleach, disinfectant, relief cream, wound glue,
	/// antiserum and soap all set. It is an operation rather than a field because
	/// the game's own method is what the rest of the game reads back.
	/// </summary>
	/// <param name="amount">The amount the game's own method receives.</param>
	void Disinfect(float amount);
}
