using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The inputs the native vital stages read from a body's own PHYSICS/ACTION state, which a host
/// clone does not produce: it skips <c>HandlePhysics</c> (where <c>inWater</c> is computed,
/// <c>Body.cs:3088</c>), its <c>moveDir</c> is zeroed every frame (it is input, not a reported
/// pose; the game's stamina rule reads <c>Mathf.Abs(moveDir.x) &lt; 0.1f</c>, <c>Body.cs:2994</c>),
/// and its <c>exercising</c>/<c>currentClimbable</c> flags are the owner's local actions. Three of
/// those reads steer the temperature/stamina branch of <c>HandleBody</c> (<c>Body.cs:2994-3002</c>),
/// and the in-water one also steers the breath rule in <c>HandleCirculation</c> (<c>Body.cs:669</c>);
/// the owned bleeding values are computed from the limbs and the internal/haemorrhage inputs, and
/// these four reach them only through temperature (&gt;41 °C drains blood, <c>Body.cs:3417-3419</c>) —
/// they are the part of the model the constant snapshot inputs do not determine.
///
/// <para>
/// The owner's client is the source (its own body, its own fluid world, its own input); the 20 Hz
/// player stream carries the verdicts and the host's clone receives them through
/// <see cref="HostVitalsInputScope"/>. The scope restores the clone's presentation values after the
/// pass, so nothing outside the pass sees the model's inputs.
/// </para>
/// </summary>
internal readonly struct HostVitalsInputs
{
	/// <summary>
	/// The game's own walk/still threshold on <c>moveDir.x</c> (<c>Body.cs:2994</c>: below it the body
	/// regenerates stamina; at or above it, stamina drains and temperature rises).
	/// </summary>
	internal const float MoveThreshold = 0.1f;

	private HostVitalsInputs(bool inWater, bool moving, bool exercising, bool climbing)
	{
		InWater = inWater;
		Moving = moving;
		Exercising = exercising;
		Climbing = climbing;
	}

	/// <summary>No input is active — a member whose stream has not produced a frame yet looks exactly like a dry, still, idle body.</summary>
	internal static HostVitalsInputs None => default;

	/// <summary>The owner's body is in water (<c>Body.inWater</c>).</summary>
	internal bool InWater { get; }

	/// <summary>The owner's move input is walking (<see cref="MoveThreshold"/>).</summary>
	internal bool Moving { get; }

	/// <summary>The owner is mid-workout (<c>Body.exercising</c>).</summary>
	internal bool Exercising { get; }

	/// <summary>The owner's current action is climbing (<c>Body.currentClimbable != null</c>) — the native regen branch excludes it.</summary>
	internal bool Climbing { get; }

	/// <summary>
	/// Whether a move input counts as walking for the game's own rule. The native test is
	/// <c>Mathf.Abs(moveDir.x) &lt; 0.1f</c>, so this is its exact complement: a NaN input is NOT
	/// stationary (the native comparison is false, which selects the drain branch).
	/// </summary>
	internal static bool IsMoving(float moveX) => !(Mathf.Abs(moveX) < MoveThreshold);

	/// <summary>The inputs one stream frame carries, or <see cref="None"/> while the member has no entity buffer.</summary>
	internal static HostVitalsInputs From(PlayerEntity? entity) =>
		entity is null
			? None
			: new HostVitalsInputs(entity.InWater, entity.Moving, entity.Exercising, entity.Climbing);

	/// <summary>
	/// The same four verdicts read off the owner's own body — the publish side of the stream. One
	/// definition for the threshold, the workout flag and the climb state, so the publisher and the
	/// model can never disagree about what a verdict means.
	/// </summary>
	internal static HostVitalsInputs FromBody(Body body) =>
		new(
			body.inWater,
			IsMoving(body.moveDir.x),
			body.exercising,
			body.currentClimbable != null); // Unity object — ==
}
