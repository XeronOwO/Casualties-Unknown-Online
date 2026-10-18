using System;
using System.Reflection;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Writes <see cref="HostVitalsInputs"/> onto a host clone for the duration of one native pass and
/// puts every presentation value back after it. The native stages read plain body fields, so the
/// inputs are applied at the same seam the pass owns; <c>currentClimbable</c> is a scene reference
/// the wire cannot carry, so the scope resolves the host's own nearest <c>Climbable</c> (the same
/// lookup the native climbing start uses, <c>Body.cs:3086</c>) — the model only reads it as
/// "climbing or not", and a host world without one is named once by the caller.
/// </summary>
internal readonly struct HostVitalsInputScope : IDisposable
{
	/// <summary>
	/// <c>Body.exercising</c> is a public get with a private set (<c>Body.cs:345</c>): the game's
	/// workout coroutine owns the property, so the model input is written through its setter.
	/// Resolved once per process into a typed delegate (a renamed or read-only property fails at
	/// first use, not per pass, and each pass allocates nothing for it).
	/// </summary>
	private static readonly Action<Body, bool> SetExercising = ResolveExercisingSetter();

	private readonly Body _body;
	private readonly bool _inWater;
	private readonly bool _exercising;
	private readonly Vector2 _moveDir;
	private readonly Climbable? _climbable;

	private HostVitalsInputScope(Body body)
	{
		_body = body;
		_inWater = body.inWater;
		_exercising = body.exercising;
		_moveDir = body.moveDir;
		_climbable = body.currentClimbable;
	}

	/// <summary>Set the model's inputs and capture what the clone presented, so <see cref="Dispose"/> can put it back.</summary>
	internal static HostVitalsInputScope Apply(Body body, in HostVitalsInputs inputs)
	{
		var scope = new HostVitalsInputScope(body);
		// Resolve before writing: the scene lookup is the one call that can throw on a torn-down
		// world, and a throw here must not leave half of the clone's fields overwritten.
		var climbable = inputs.Climbing ? Climbable.GetClosestClimbable(body.transform.position) : null;
		try
		{
			body.inWater = inputs.InWater;
			// The pass reads only the x component against the threshold; the y component is physics
			// input the model does not consume. A value at the threshold's far end states the verdict.
			body.moveDir = new Vector2(inputs.Moving ? 1f : 0f, 0f);
			SetExercising(body, inputs.Exercising);
			body.currentClimbable = climbable;
			return scope;
		}
		catch
		{
			scope.Dispose();
			throw;
		}
	}

	/// <summary>Put back what this clone presented outside the pass (the visual/action path owns those values).</summary>
	public void Dispose()
	{
		_body.inWater = _inWater;
		_body.moveDir = _moveDir;
		SetExercising(_body, _exercising);
		_body.currentClimbable = _climbable;
	}

	private static Action<Body, bool> ResolveExercisingSetter()
	{
		var property = typeof(Body).GetProperty("exercising", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("Body.exercising not found.");
		var setter = property.GetSetMethod(nonPublic: true)
			?? throw new InvalidOperationException("Body.exercising has no setter.");
		return (Action<Body, bool>)setter.CreateDelegate(typeof(Action<Body, bool>));
	}
}
