using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The remote medical panel's heartbeat cue. The game plays its thump from the LOCAL body's
/// <c>Body.HandleCirculation</c> (Body.cs:888-924): the viewer's own heart drives the beat and the viewer's own
/// panel selects the monitor variant, so a critical viewer heard the monitor alarm while the panel displayed a
/// healthy patient. While a remote medical focus is open the native clip is refused at the string
/// <c>Sound.Play</c> seam (<c>RemoteMedicalPatches.RemoteMedicalNativeHeartbeatPatch</c>) and this class plays
/// the DISPLAYED body's alarm instead: a healthy displayed body is silent, a critically dying one alarms at
/// the display body's own heart phase (the <c>heartProg</c> already advanced for the redirected ECG by
/// <see cref="RemoteCharacterDisplayProjection.AdvanceMedicalDisplay"/>), and its fibrillation progress damps
/// the volume exactly like the native block. Closing the focus destroys the display body and
/// <see cref="RemoteMedicalView.Close"/> resets the latch, so the native behaviour returns untouched.
/// </summary>
internal static class RemoteMedicalHeartbeat
{
	/// <summary>The native thump threshold inside one heart cycle (Body.cs:907).</summary>
	private const float ThumpThreshold = 0.3f;

	/// <summary>
	/// The normal clip. The panel itself never plays it (a healthy displayed body is silent — the ticket's
	/// ruling); it is recognised only so the native block's attempt at it is refused.
	/// </summary>
	internal const string NormalClip = "heartthump";

	/// <summary>The critical variant the native block selects when the monitor one is unavailable (Body.cs:914-918).</summary>
	internal const string HeavyClip = "heartthump-heavy";

	/// <summary>The native monitor variant — selected when the panel is open and the run is not unchipped (Body.cs:915-918).</summary>
	internal const string MonitorClip = "heartthump-heavy-monitor";

	private static bool _didThump;

	private static bool _missingClipReported;

	/// <summary>True for the clips the native thump block can play — the only sounds a remote focus may suppress.</summary>
	internal static bool IsNativeHeartbeatClip(string? clip) =>
		clip is NormalClip or HeavyClip or MonitorClip;

	/// <summary>
	/// The native flatline clip, played by <c>PlayerCamera</c> the instant the body it watches enters cardiac arrest
	/// (<c>PlayerCamera.cs:1859-1864</c>, volume 0.8). On the remote panel that body is the VIEWED one, so the cue has to
	/// follow the display body's own transition instead — the viewer's own heart is not the one that stopped.
	/// </summary>
	internal const string FlatlineClip = "flatline";

	/// <summary>The native flatline volume (<c>PlayerCamera.cs:1863</c>).</summary>
	private const float FlatlineVolume = 0.8f;

	private static bool _lastBeating = true;

	private static bool _missingFlatlineReported;

	/// <summary>Forget the current cycle's beat latch; the focus owns this state, so every open/close starts clean.</summary>
	internal static void Reset()
	{
		_didThump = false;
		_missingClipReported = false;
		_lastBeating = true;
		_missingFlatlineReported = false;
	}

	/// <summary>
	/// The native flatline edge, Unity-free so it can be tested: <c>PlayerCamera.cs:1859-1863</c> plays the clip when a
	/// body that WAS beating enters cardiac arrest, and skips the whole cue in an unchipped run. The native latch is the
	/// viewer's own body; here it is the displayed one, which is the only difference.
	/// </summary>
	internal static bool ShouldPlayFlatline(bool wasBeating, bool inCardiacArrest, bool unchipped) =>
		wasBeating && !unchipped && inCardiacArrest;

	/// <summary>
	/// One frame of the displayed body's heart phase: a beat is due when the progressed cycle passes the native
	/// threshold, and the wrap re-arms it. Returns the alarm clip the beat must play, or null while no alarm is
	/// due. A healthy displayed patient is silent (the ticket's ruling): only a critically dying one alarms, and
	/// the beat that crossed while healthy is consumed so a patient that turns critical mid-cycle alarms on its
	/// next beat instead of on the partial one. Unity-free so the rule is unit-testable; the caller supplies the
	/// display body's already-advanced <c>heartProg</c> so the alarm stays on the ECG's own phase.
	/// </summary>
	internal static string? Beat(float heartProg, bool criticallyDying, bool monitorVariantAllowed)
	{
		if (heartProg <= ThumpThreshold)
		{
			_didThump = false;
			return null;
		}

		if (_didThump)
		{
			return null;
		}

		_didThump = true;
		if (!criticallyDying)
		{
			return null;
		}

		return monitorVariantAllowed ? MonitorClip : HeavyClip;
	}

	/// <summary>
	/// Play the displayed body's due alarm. The panel focus is open by definition here (the coordinator closes
	/// it when the native WoundView is not), which is also the native monitor-variant condition; a run's
	/// unchipped mode keeps the heavy clip, as the native block does. The native volume damping (Body.cs:911,
	/// the fibrillation progress) is mirrored; the call is the game's own 2D, pitch-stable, reverb-free,
	/// mixer-bypassing heartbeat play. The AudioClip overload is used on purpose: the suppression patch binds
	/// only the string overload, so the panel's own alarm is never caught by it.
	/// </summary>
	internal static void Play(Body display, ILogger log)
	{
		var monitorVariantAllowed = WorldGeneration.world != null // Unity object — ==
			&& !WorldGeneration.unchipped;
		var clip = Beat(display.heartProg, display.isCriticallyDying, monitorVariantAllowed);
		if (clip is null)
		{
			return;
		}

		var resource = Resources.Load<AudioClip>("Sounds/" + clip);
		if (resource == null) // Unity object — ==
		{
			// A broken clip would otherwise warn once per beat; the focus reports it once and stays at Debug.
			if (!_missingClipReported)
			{
				_missingClipReported = true;
				log.LogWarning(
					"[MedicalView] heartbeat clip {Clip} is missing from Resources; the panel's alarm is silent.",
					clip);
			}
			else
			{
				log.LogDebug("[MedicalView] heartbeat clip {Clip} is still missing from Resources.", clip);
			}

			return;
		}

		var volume = 1f - (display.fibrillationProgress - 50f) / 80f;
		Sound.Play(resource, Vector2.zero, true, false, null, volume, 1f, true, true);
		log.LogDebug(
			"[MedicalView] panel alarm {Clip} for the displayed body (heartRate={HeartRate:0.#} bpm, critical={Critical}, fibrillation={Fibrillation:0.#}, volume={Volume:0.###}).",
			clip, display.heartRate, display.isCriticallyDying, display.fibrillationProgress, volume);
	}

	/// <summary>
	/// One frame of the displayed body's heart STATE: play the native flatline the first time it reads arrested, and
	/// re-arm when it reads beating again. Called beside <see cref="Play"/> because the panel's alarm and its flatline are
	/// two edges of the same heart: the native code plays the thump from the circulation pass and this clip from the
	/// camera's own per-frame check (<c>PlayerCamera.cs:1859-1864</c>), and both describe the body the player is watching.
	///
	/// <para>
	/// Without this the panel went silent at the exact moment its patient died: the native clip belongs to the VIEWER's
	/// own body, which is why the remote case never triggered it (2026-11-02, reported from a real session — the host
	/// watched a member's panel and heard nothing when that member's heartbeat stopped).
	/// </para>
	/// </summary>
	internal static void PlayFlatline(Body display, ILogger log)
	{
		var arrested = display.inCardiacArrest;
		var shouldPlay = ShouldPlayFlatline(_lastBeating, arrested, WorldGeneration.unchipped);
		_lastBeating = !arrested;
		if (!shouldPlay)
		{
			return;
		}

		var resource = Resources.Load<AudioClip>("Sounds/" + FlatlineClip);
		if (resource == null) // Unity object — ==
		{
			if (!_missingFlatlineReported)
			{
				_missingFlatlineReported = true;
				log.LogWarning("[MedicalView] flatline clip {Clip} is missing from Resources; the panel stays silent at the stop.", FlatlineClip);
			}

			return;
		}

		// The native call's own shape: 2D, no pitch shift, no reverb, mixer bypassed (PlayerCamera.cs:1863).
		Sound.Play(resource, Vector2.zero, true, false, null, FlatlineVolume, 1f, true, true);
		log.LogInformation("[MedicalView] panel flatline for the displayed body (heartRate={HeartRate:0.#} bpm).", display.heartRate);
	}
}
