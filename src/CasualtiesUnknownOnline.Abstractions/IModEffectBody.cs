using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The body a liquid effect landed on, reached through named operations. This is
/// the whole write surface the primary path offers: every member below is an
/// operation CUO names, documents and keeps stable, and each one is grown from a
/// write the game's own shipped liquids perform — the census in
/// <c>docs/backlog/review/mod-authored-effects.md</c> is where the list comes from.
///
/// <para>
/// The operations are the game's own semantics, not a reimplementation: the two
/// actions call the game's own <c>Body.Drink</c> / <c>Body.Eat</c> (including what
/// they do besides the field they write — dropping the item held in the eat slot,
/// the eat timer, the dislocated-jaw penalty), the doses go through the game's own
/// components, and the timed body is the game's own <c>CoUtils</c> clock. A mod
/// that wants an effect the list cannot express uses
/// <see cref="IModLiquidEffectContext.NativeObject"/> and leaves the API's promise.
/// </para>
///
/// <para>
/// Level <see cref="ApiStabilityLevel.Advanced"/>: this surface follows the game's
/// own fields, so a game update that moves one may move this with no CUO decision
/// involved.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Advanced)]
public interface IModEffectBody
{
	/// <summary>The game's own alive flag. Read-only: life and death are the game's to decide.</summary>
	bool Alive { get; }

	/// <summary>Whether the body is on its feet. Read-only.</summary>
	bool Standing { get; }

	/// <summary>Whether the body is in cardiac arrest. Read-only.</summary>
	bool InCardiacArrest { get; }

	/// <summary>
	/// The body's limbs, in the game's own array order — so entry 0 is what a
	/// vanilla effect writes as <c>body.limbs[0]</c> (each accessor's
	/// <see cref="IModEffectLimb.Index"/> states the index it answers for). An entry
	/// the game has already destroyed is left out, so the list can be SHORTER than
	/// the array it snapshots; the indices are never renumbered.
	/// </summary>
	IReadOnlyList<IModEffectLimb> Limbs { get; }

	/// <summary>
	/// The game's own drink action: it adds to thirst and runs the rest of
	/// <c>Body.Drink</c> (dropping whatever the eat slot holds, starting the eat
	/// timer). Use it for hydration rather than writing the vital directly.
	/// </summary>
	/// <param name="ml">Hydration in the game's own unit, exactly as the native call would pass it.</param>
	void Drink(float ml);

	/// <summary>
	/// The game's own eat action: hunger and weight gain, capped, with the game's
	/// own consequences (a dislocated jaw costs pain and scales both amounts down, a
	/// full stomach turns into sickness, and a big bite may make the body vomit).
	/// </summary>
	/// <param name="hunger">Hunger added, in the units the game's own liquids pass.</param>
	/// <param name="weightGain">Weight added, in the units the game's own liquids pass.</param>
	void Eat(float hunger, float weightGain);

	/// <summary>Makes the body vomit through the game's own vomiter component.</summary>
	void Vomit();

	/// <summary>The game's own burp (restarts its burp timer).</summary>
	void Burp();

	/// <summary>Drops the body into the game's own ragdoll state.</summary>
	void Ragdoll();

	/// <summary>
	/// Starts the game's own fibrillation, through the same entry the game's own
	/// epinephrine branch uses (<c>TryStartFibrillation(true)</c> — forced).
	/// </summary>
	void StartFibrillation();

	/// <summary>
	/// Attaches the game's own mind-wipe component — the one
	/// <c>Liquids.cs</c>'s <c>braingrow</c> attaches past its dose threshold. A
	/// no-op when the body already carries it, exactly as the native branch is.
	/// </summary>
	void StartMindWipe();

	/// <summary>
	/// Adds to every limb's rigidbody velocity, in one call — the shake
	/// <c>Liquids.cs</c>'s <c>oxylinedrink</c> gives a body that is not standing.
	/// This is the one operation here that moves the body in the physics world
	/// rather than writing a state field.
	/// </summary>
	/// <param name="velocity">The velocity delta, in Unity units per second.</param>
	void AddLimbVelocity(Vector2 velocity);

	/// <summary>
	/// Reads one body vital. An undefined <paramref name="vital"/> returns 0 and logs.
	/// </summary>
	float GetVital(ModBodyVital vital);

	/// <summary>
	/// Adds to one body vital — the shape most of the game's own effects use.
	/// <see cref="ModBodyVital.OverdoseIndex"/> is integer-valued in the game and is
	/// truncated on write. An undefined <paramref name="vital"/> is refused and logs.
	/// </summary>
	void AddVital(ModBodyVital vital, float amount);

	/// <summary>
	/// Sets one body vital outright — the shape the game uses where a value is
	/// pinned rather than accumulated (<c>consciousness = 0</c>,
	/// <c>adrenaline = 100</c>, <c>heartRate = 200</c>). Same truncation and refusal
	/// rules as <see cref="AddVital"/>.
	/// </summary>
	void SetVital(ModBodyVital vital, float value);

	/// <summary>
	/// Adds to the body's opiate amount through the game's own painkiller
	/// component, creating it if the body has none — the dose every opiate in
	/// <c>Liquids.cs</c> applies.
	/// </summary>
	void AddOpiate(float amount);

	/// <summary>Adds to the body's opiate antagonist amount through the game's own painkiller component (naloxone, naltrexone).</summary>
	void AddOpiateAntagonist(float amount);

	/// <summary>Adds to the body's opiate tolerance through the game's own painkiller component.</summary>
	void AddOpiateTolerance(float amount);

	/// <summary>Applies one antidepressant dose through the game's own antidepressant component (<c>TakeDose</c>), creating it if absent.</summary>
	void TakeAntidepressant(float amount);

	/// <summary>Adds to the body's sleeping-pill amount through the game's own sleeping-pill component, creating it if absent.</summary>
	void AddSleepingPill(float amount);

	/// <summary>
	/// Starts (or extends) a timed body on the game's own clock: the tick runs once
	/// IMMEDIATELY and then once per game second while the remaining duration lasts.
	/// <paramref name="key"/> is scoped to this mod — the framework prefixes it — so
	/// a mod can never collide with the game's own timed ops or with another mod's.
	///
	/// <para>
	/// The game's own semantics apply and are worth knowing: calling this again
	/// under a live key ADDS to its remaining duration and KEEPS the tick registered
	/// first, so the second call's <paramref name="tick"/> never runs. That is what
	/// the game's own liquids do with it, and it is why the read-back pair below
	/// exists: <c>antirad</c>'s overdose branch is a duration threshold, not a
	/// counter.
	/// </para>
	/// </summary>
	/// <param name="key">This mod's own name for the timed body.</param>
	/// <param name="seconds">How long it runs, added to what is already left under the key.</param>
	/// <param name="tick">The operation: once now, then once per game second. Invoked with this same body accessor.</param>
	void StartTimedEffect(string key, float seconds, Action<IModEffectBody> tick);

	/// <summary>How long the named timed body still has to run, in game seconds (0 when it is not running).</summary>
	float TimedEffectRemaining(string key);

	/// <summary>The longest remaining duration the named timed body has ever had — the game's own overdose-tier input.</summary>
	float TimedEffectHighest(string key);

	/// <summary>
	/// Plays one of the game's own sound clips at the body's position, through the
	/// same call the game's own liquids use — so CUO's existing sound relay carries
	/// it to the peers exactly as it carries a vanilla liquid's clip.
	/// </summary>
	/// <param name="clipId">The game's own clip id, e.g. <c>"pills"</c> or <c>"cream"</c>.</param>
	void PlaySound(string clipId);

	/// <summary>
	/// The engine object of the body this accessor stands for — the escape hatch for
	/// an author who wants the game's own objects, beside the item's
	/// (<see cref="IModItemActionContext.NativeObject"/>) and the limb's
	/// (<see cref="IModEffectLimb.NativeObject"/>). Every body and limb in this game
	/// is a <c>MonoBehaviour</c>, so the cast or a <c>GetComponent</c> recovers them.
	/// On a liquid effect it is the same object
	/// <see cref="IModLiquidEffectContext.NativeObject"/> carries.
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
