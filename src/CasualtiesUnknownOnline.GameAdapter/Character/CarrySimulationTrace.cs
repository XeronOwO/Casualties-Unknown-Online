using System.Globalization;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Observability for the carried rider's own client. The reported symptoms
/// (flat ECG, limbs twitching with a growing frequency) could only be read off
/// a real two-client session, so the simulation this side actually runs must be
/// measurable there:
/// - one Information line per carry-relation change, naming the presentation
///   mode the rider's body is in and the ECG driver's current value;
/// - a Debug line per second while carried, carrying the heart progression and
///   its delta (the ECG animation driver the flat trace was missing), the pose
///   and movement gates and how many carry placement writes and frames the
///   window saw — the cadence that a twitch would show up in.
/// The per-second trace is Debug on purpose (high frequency by construction);
/// a diagnostic run raises <c>Logging.MinimumLevel</c> to capture it.
/// </summary>
internal sealed class CarrySimulationTrace
{
	private const float WindowSeconds = 1f;

	private float _windowStartTime;
	private bool _windowOpen;
	private int _windowFrames;
	private int _windowCarryFollows;
	private float _windowStartHeartProg;
	private float _windowStartHeartRate;
	private Vector3 _windowStartPosition;

	/// <summary>
	/// Counts one carry-follow write: the frames whose carrier anchor was found
	/// and written. Frames without an anchor are counted by <see cref="Tick"/>
	/// alone, which is what makes the two numbers comparable.
	/// </summary>
	internal void CountCarryFollow() => _windowCarryFollows++;

	/// <summary>
	/// Records one frame of the local carried rider and emits the per-second
	/// trace. <paramref name="keepsNativeSimulation"/> is the mode the body
	/// patches are applying this frame.
	/// </summary>
	internal void Tick(Body rider, ulong carrierSteamId, bool keepsNativeSimulation, ILogger log)
	{
		var now = Time.unscaledTime;
		if (!_windowOpen)
		{
			OpenWindow(rider, now);
		}

		_windowFrames++;
		if (now - _windowStartTime < WindowSeconds)
		{
			return;
		}

		var elapsed = now - _windowStartTime;
		var heartProg = rider.heartProg;
		var heartRate = rider.heartRate;
		var position = rider.transform.position;
		var movingAllowed = Traverse.Create(rider).Field("movingAllowed").GetValue<bool>();
		log.LogDebug(
			"[Carry] rider {Carrier} frame window: mode={Mode} frames={Frames} carryFollows={Follows} elapsed={Elapsed} heartRate={HeartRate} heartProg={HeartProg} heartProgPerSecond={HeartProgRate} heartRateDrift={HeartRateDrift} standing={Standing} rbSimulated={RbSimulated} grounded={Grounded} velocity=({VelX},{VelY}) moveDir=({MoveX},{MoveY}) movingAllowed={MovingAllowed} moved={Moved}",
			carrierSteamId,
			keepsNativeSimulation ? "native-simulation" : "pinned-ragdoll",
			_windowFrames,
			_windowCarryFollows,
			Format(elapsed),
			Format(heartRate),
			Format(heartProg),
			Format((heartProg - _windowStartHeartProg) / elapsed),
			Format(heartRate - _windowStartHeartRate),
			rider.standing,
			rider.rb.simulated,
			rider.grounded,
			Format(rider.rb.velocity.x),
			Format(rider.rb.velocity.y),
			Format(rider.moveDir.x),
			Format(rider.moveDir.y),
			movingAllowed,
			Format(Vector3.Distance(position, _windowStartPosition)));

		OpenWindow(rider, now);
	}

	/// <summary>
	/// One Information line when the local body becomes a carried rider: which
	/// presentation mode its own client applies and the ECG driver's value at
	/// that instant, so a log from a real session says whether the body's
	/// simulation runs at all.
	/// </summary>
	internal static void LogAttached(Body rider, ulong carrierSteamId, bool keepsNativeSimulation, ILogger log) =>
		log.LogInformation(
			"[Carry] rider simulation attached to carrier {Carrier}: mode={Mode} standing={Standing} rbSimulated={RbSimulated} heartRate={HeartRate} heartProg={HeartProg} (set Logging.MinimumLevel=Debug for the per-second rider trace)",
			carrierSteamId,
			keepsNativeSimulation ? "native-simulation" : "pinned-ragdoll",
			rider.standing,
			rider.rb.simulated,
			Format(rider.heartRate),
			Format(rider.heartProg));

	/// <summary>
	/// One Information line when the local body stops being carried: the state
	/// the body was handed back in, so the release half of the relation is
	/// readable from the same log.
	/// </summary>
	internal static void LogReleased(Body rider, ulong carrierSteamId, ILogger log) =>
		log.LogInformation(
			"[Carry] rider simulation released from carrier {Carrier}: standing={Standing} rbSimulated={RbSimulated} heartRate={HeartRate} heartProg={HeartProg}",
			carrierSteamId,
			rider.standing,
			rider.rb.simulated,
			Format(rider.heartRate),
			Format(rider.heartProg));

	private void OpenWindow(Body rider, float now)
	{
		_windowOpen = true;
		_windowStartTime = now;
		_windowFrames = 0;
		_windowCarryFollows = 0;
		_windowStartHeartProg = rider.heartProg;
		_windowStartHeartRate = rider.heartRate;
		_windowStartPosition = rider.transform.position;
	}

	private static string Format(float value) =>
		value.ToString("0.###", CultureInfo.InvariantCulture);
}
