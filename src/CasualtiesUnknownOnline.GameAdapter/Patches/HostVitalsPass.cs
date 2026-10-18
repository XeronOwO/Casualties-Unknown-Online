using System;
using System.Collections;
using CasualtiesUnknownOnline.GameAdapter.Character;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The host's per-frame vitals pass for ONE remote player's simulated clone: the five vital-sign
/// stages of the native <c>Body.Update</c> plus <c>Limb.Update</c>, run from a fixed step with
/// every owner-local side effect suppressed.
///
/// <para>
/// The stages are the game's own (<see cref="BodyUpdatePatch.RunVitalsSubset"/>,
/// <see cref="BodyPatches.RunLimbUpdates"/>). The limbs advance first, then the body stages in the
/// game's own order: natively <c>Limb.Update</c> and <c>Body.Update</c> are separate MonoBehaviour
/// callbacks with no defined relative order, so the relative position is fixed here as a
/// definition rather than left to Unity's dispatch order.
/// </para>
///
/// <para>
/// The pass runs from <c>HostVitalsDriver.FixedUpdate</c>, where Unity reports the fixed step as
/// <c>Time.deltaTime</c>; every stage therefore sees one dt, and a variable frame delta can never
/// enter the model. <see cref="StepWithinCap"/> is the belt: a step above 0.25 s is skipped by the
/// caller (and named once) instead of teleporting a value.
/// </para>
///
/// <para>
/// Everything the native stages would do to the HOST is suppressed here or by the patches that
/// check <see cref="IsActive"/>: <c>UnityEngine.Random</c> is saved, seeded and restored around
/// the pass (two hosts replay the same numbers and the host's own world stream is untouched), and
/// <c>PlayerCamera.threatMusicTime</c> — a field write with no patch point — is saved and
/// restored. The list of patch-backed suppressions is the ticket's table; the pin test is
/// <c>HostVitalsSuppressionPinTests</c>.
/// </para>
/// </summary>
internal static class HostVitalsPass
{
	/// <summary>The largest step one pass may advance: a stalled fixed step must not teleport a value.</summary>
	internal const float MaxStepSeconds = 0.25f;

	/// <summary>
	/// The pass's own RNG seed. The stages draw from the process-global <c>UnityEngine.Random</c>
	/// (infection checks, radiation, brain damage); seeding it per pass makes every host's model
	/// replay the same numbers, and the saved/restored state keeps the host's own world stream out
	/// of it. The drawn outcomes are deterministic placeholders, never authoritative: the owner's
	/// report seeds the real outcome on its next snapshot.
	/// </summary>
	private const int Seed = 0x43554F; // "CUO"

	/// <summary>The fixed step a <c>FixedUpdate</c> reports, checked once per body so a wrong assumption is a named log line instead of a silent speed error. 1 ms keeps a 60 fps frame delta (0.0167 s) far outside a 0.02 s fixed step.</summary>
	internal const float FixedStepToleranceSeconds = 0.001f;

	[ThreadStatic]
	private static int _depth;

	/// <summary>True while a host clone's pass is on the stack; every suppression patch reads it.</summary>
	internal static bool IsActive => _depth > 0;

	/// <summary>Whether a step is usable: positive, finite and at most <see cref="MaxStepSeconds"/>.</summary>
	internal static bool StepWithinCap(float dt) =>
		!float.IsNaN(dt) && !float.IsInfinity(dt) && dt > 0f && dt <= MaxStepSeconds;

	/// <summary>Whether the reported step is the fixed step (the pass's dt assumption, checked once per body).</summary>
	internal static bool IsFixedStep(float dt, float fixedDeltaTime) =>
		!float.IsNaN(dt) && !float.IsInfinity(dt) && Math.Abs(dt - fixedDeltaTime) <= FixedStepToleranceSeconds;

	/// <summary>Enter the pass scope. The caller owns the returned scope; a disposed scope cannot decrement twice.</summary>
	internal static IDisposable EnterScope() => new Scope();

	/// <summary>An empty coroutine body for a suppressed native coroutine: <c>StartCoroutine</c> still gets a valid enumerator.</summary>
	internal static IEnumerator EmptyRoutine() => new EmptyRoutineEnumerator();

	/// <summary>
	/// Run one full pass over <paramref name="body"/>: the client-owned fields are captured first
	/// (<paramref name="quarantine"/>), the limb half and the five body stages run under the pass
	/// scope, the fixed RNG seed and the camera-threat field's preservation, then the quarantine is
	/// restored and the random-event timers are cleared so the next pass cannot reach the game's
	/// minute/half-minute random blocks.
	/// </summary>
	internal static void Run(Body body, HostVitalsQuarantine quarantine)
	{
		if (IsActive)
		{
			throw new InvalidOperationException("HostVitalsPass.Run is not re-entrant.");
		}

		var randomState = Random.state;
		var camera = PlayerCamera.main; // Unity object — == below
		var threatMusicTime = camera != null ? camera.threatMusicTime : 0f;
		using (EnterScope())
		{
			try
			{
				Random.InitState(Seed);
				quarantine.Capture(body);
				try
				{
					BodyPatches.RunLimbUpdates(body);
					BodyUpdatePatch.RunVitalsSubset(body);
				}
				finally
				{
					quarantine.Restore(body);
					// The minute/half-minute blocks are the game's random-event blocks; skipping them is
					// how the host stops rolling for another body. The second-check derived pass stays
					// (bleeding/clothing/immunity values the model needs); its draws are quarantined.
					body.minuteCheckTime = 0f;
					body.halfMinuteCheckTime = 0f;
				}
			}
			finally
			{
				Random.state = randomState;
				if (camera != null)
				{
					camera.threatMusicTime = threatMusicTime;
				}
			}
		}
	}

	/// <summary>A shared-state-free empty enumerator; each suppressed coroutine gets its own instance.</summary>
	private sealed class EmptyRoutineEnumerator : IEnumerator
	{
		public object Current => null!;

		public bool MoveNext() => false;

		public void Reset()
		{
		}
	}

	/// <summary>One pass's scope; nested scopes are not created by the pass (it refuses re-entry) but the depth is counted so a test can observe it.</summary>
	private sealed class Scope : IDisposable
	{
		private bool _disposed;

		internal Scope()
		{
			_depth++;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_depth--;
		}
	}
}
