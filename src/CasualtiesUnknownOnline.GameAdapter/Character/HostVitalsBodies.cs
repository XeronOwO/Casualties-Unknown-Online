using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Time;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The host's per-remote-player body model: the existing render clone, seeded from the stored
/// character snapshot, advanced by the game's own vital stages and writing the three values the
/// host owns back into that snapshot. The clone is not a second body — it is the one body the
/// host already holds for a remote player, so the host's view and its authority are the same
/// object and cannot disagree.
///
/// <para>
/// Data flow per fixed step: read the stored snapshot; seed the body once (health, skills, limbs,
/// painkiller fields; no item wipe, no limb components); on a new snapshot object map its inputs
/// with the owned values preserved; if the stored owned values no longer match what this body
/// last wrote, an authoritative write landed — copy it onto the body before advancing; run
/// <see cref="HostVitalsPass"/>; write the body's three values back. A plain report can
/// never reach the owned values (<c>ContinuousVitals.Preserve</c>), which is what makes the last
/// comparison an authority test.
/// </para>
///
/// <para>
/// Only the host attaches a driver. Guests keep the presentation-only clone, and a local body is
/// never touched: the suppression patches are scoped to the pass, not to a role.
/// </para>
/// </summary>
internal sealed class HostVitalsBodies
{
	private readonly ISessionControl _session;
	private readonly ICharacterDataControl _characters;
	private readonly CharacterRestoreApplier _restore;
	private readonly ITimeSource _time;
	private readonly ILogger<HostVitalsBodies> _log;

	internal HostVitalsBodies(
		ISessionControl session,
		ICharacterDataControl characters,
		CharacterRestoreApplier restore,
		ITimeSource time,
		ILogger<HostVitalsBodies> log)
	{
		_session = session;
		_characters = characters;
		_restore = restore;
		_time = time;
		_log = log;
	}

	/// <summary>True when this body carries the host simulation driver (the patches' marker for "the clone is the model").</summary>
	internal static bool IsSimulated(Body body) =>
		body != null && body.TryGetComponent<HostVitalsDriver>(out _); // Unity object — ==

	/// <summary>Give a host-created clone its fixed-step driver. A guest never reaches this; a repeated attach is a no-op.</summary>
	internal void Attach(Body body, ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		if (body.TryGetComponent<HostVitalsDriver>(out _))
		{
			return;
		}

		var driver = body.gameObject.AddComponent<HostVitalsDriver>();
		driver.Initialize(this, body, steamId);
	}

	/// <summary>One fixed step of one body. Called by <see cref="HostVitalsDriver.FixedUpdate"/>.</summary>
	internal void Step(HostVitalsDriver driver)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!_session.IsRemoteInWorld(driver.SteamId))
		{
			return;
		}

		var data = _characters.GetSavedCharacter(driver.SteamId);
		if (data?.Health is null)
		{
			return; // no snapshot yet — nothing authoritative to advance from
		}

		var changed = !driver.Seeded
			|| !ReferenceEquals(data, driver.LastApplied)
			|| data.LocalRevision != driver.LastAppliedRevision;

		// The finite scan runs on the edges only (a report is refused at the store, so this catches
		// an authority writer's value); scanning every fixed step was self-inflicted frame cost.
		if (changed && CharacterDataFinite.FirstNonFinite(data) is { } nonFinite)
		{
			if (!driver.WarnedNonFiniteSnapshot)
			{
				driver.WarnedNonFiniteSnapshot = true;
				_log.LogWarning("[Vitals] {Player}'s stored snapshot carries a non-finite {Field} — the simulated body keeps its last finite state.",
					driver.SteamId, nonFinite);
			}

			return;
		}

		if (driver.WarnedNonFiniteSnapshot)
		{
			driver.WarnedNonFiniteSnapshot = false;
			_log.LogInformation("[Vitals] {Player}'s stored snapshot is finite again; the simulated body resumes.", driver.SteamId);
		}

		if (!driver.Seeded)
		{
			Seed(driver, data);
		}
		else if (changed)
		{
			ApplyInputs(driver, data);
			driver.LastApplied = data;
			driver.LastAppliedRevision = data.LocalRevision;
		}

		// The stored owned values can only have moved without this body writing them if an event
		// the host arbitrated wrote the snapshot: adopt it before the advance overwrites it.
		if (ContinuousVitals.Differs(data, driver.LastWrittenBloodVolume, driver.LastWrittenTotalBleedSpeed, driver.LastWrittenBleeds))
		{
			HostVitalsOwned.Apply(data, driver.Body);
			CaptureWritten(driver);
			_log.LogInformation("[Vitals] {Player}'s body re-seeded from an authoritative write (blood {Blood:F1}).",
				driver.SteamId, driver.LastWrittenBloodVolume);
		}

		var dt = Time.deltaTime;
		if (!driver.LoggedFixedStep)
		{
			if (!HostVitalsPass.IsFixedStep(dt, Time.fixedDeltaTime))
			{
				_log.LogWarning("[Vitals] {Player}'s simulated body got a {Dt:F4}s step in FixedUpdate (fixed step {Fixed:F4}s) — the model's dt assumption is wrong for this build.",
					driver.SteamId, dt, Time.fixedDeltaTime);
			}
			else
			{
				driver.LoggedFixedStep = true;
				_log.LogDebug("[Vitals] {Player}'s simulated body advances on the fixed step ({Dt:F4}s).", driver.SteamId, dt);
			}
		}

		if (!HostVitalsPass.StepWithinCap(dt))
		{
			if (!driver.WarnedStalledStep)
			{
				driver.WarnedStalledStep = true;
				_log.LogWarning("[Vitals] {Player}'s simulated body skipped a {Dt:F3}s step (cap {Cap:F2}s) — the model must not advance by a stalled delta.",
					driver.SteamId, dt, HostVitalsPass.MaxStepSeconds);
			}

			return;
		}

		HostVitalsPass.Run(driver.Body, driver.Quarantine);
		HostVitalsOwned.Capture(driver.Body, driver.LastWrittenBleeds, out var blood, out var total);
		ContinuousVitals.Write(data, blood, total, driver.LastWrittenBleeds);
		driver.LastWrittenBloodVolume = blood;
		driver.LastWrittenTotalBleedSpeed = total;
		// The 10 Hz row is gated on this mark: a clone that cannot be seeded/advanced, a refused
		// snapshot or a stopped FixedUpdate (the start gate's timeScale 0) must not keep pinning the
		// owner's body to stale host values.
		_characters.MarkHostModelAdvancing(driver.SteamId, _time.NowMs);
	}

	/// <summary>The first usable snapshot: the full mapped state plus the painkiller component's fields, and the owned values become the authority baseline.</summary>
	private void Seed(HostVitalsDriver driver, CharacterDataMsg data)
	{
		_restore.ApplyStats(driver.Body, data, applyComponents: false);
		ApplyComponentInputs(driver.Body, data);
		SeedDerivedState(driver.Body);
		driver.Seeded = true;
		driver.LastApplied = data;
		driver.LastAppliedRevision = data.LocalRevision;
		CaptureWritten(driver);
		_log.LogInformation("[Vitals] seeded {Player}'s simulated body ({Limbs} limbs, blood {Blood:F1}) from the character snapshot.",
			driver.SteamId, driver.Body.limbs.Length, driver.LastWrittenBloodVolume);
	}

	/// <summary>
	/// Force the periodic derived fields onto the LAST template values: the clone is an
	/// <c>Instantiate</c> of the local player's body, so timers such as the one-second and
	/// half-second checks arrive at arbitrary phases, and the fields those checks compute
	/// (<c>bleedingSpeedMultiplier</c>, <c>bleedClottingSpeed</c>, <c>clothingTemperature</c>,
	/// <c>hasScubaGear</c>, <c>immunity</c>, the encumbrance cap) are not on the wire. Without this
	/// the first second of the host's model would use the LOCAL player's bleeding multiplier and
	/// clothing. The random-event timers are zeroed at the same time: the seeded period must not
	/// inherit the template's half-elapsed minute check and roll an event on its first pass.
	/// </summary>
	private static void SeedDerivedState(Body body)
	{
		body.minuteCheckTime = 0f;
		body.halfMinuteCheckTime = 0f;
		body.limbBloodUpdateTimer = 0f;
		body.tempCheckTime = Time.time; // the first full temperature refresh waits one game-second
										// Sleep-quality inputs are not on the health message; the neutral value is the honest seed.
		body.usingSleepingBag = false;
		body.curSleep = Body.SleepQuality.Okay;
		body.forcedSleepQuality = null;
		var fields = Traverse.Create(body);
		fields.Field("secondCheckTime").SetValue(1.01f);      // >1: the one-second derived pass runs once
		fields.Field("halfSecondCheckTime").SetValue(0.51f); // >0.5: the half-second derived pass runs once
															 // The limb infection roll is a client-owned random outcome; a far-future check time means
															 // the host's Limb.Update never reaches it (the field is private and not on the wire).
		foreach (var limb in body.limbs)
		{
			Traverse.Create(limb).Field("infectionCheckTime").SetValue(9999f);
		}
	}

	/// <summary>
	/// A new snapshot object: map every input onto the body, but keep the values the host owns —
	/// the body has advanced since the snapshot was built, so the snapshot's copies are stale.
	/// </summary>
	private void ApplyInputs(HostVitalsDriver driver, CharacterDataMsg data)
	{
		HostVitalsOwned.Capture(driver.Body, driver.LastWrittenBleeds, out var blood, out var total);
		_restore.ApplyStats(driver.Body, data, applyComponents: false);
		ApplyComponentInputs(driver.Body, data);
		HostVitalsOwned.Restore(driver.Body, blood, total, driver.LastWrittenBleeds);
	}

	/// <summary>
	/// The component-derived terms the native stages read or the computed happiness uses: the
	/// painkiller fields (circulation and temperature), the opiate/antidepressant happiness terms
	/// the destroyed components would otherwise leave at the template's local-player values, and
	/// then the painkiller component's own Update is disabled — the owner's report seeds the
	/// reception, the host must not run a second opiate ramp on the clone.
	/// </summary>
	private static void ApplyComponentInputs(Body body, CharacterDataMsg data)
	{
		PainkillersSync.Apply(body, data.Health);
		var painkillers = body.GetComponent<Painkillers>();
		if (painkillers != null) // Unity object — ==
		{
			painkillers.enabled = false;
		}

		if (data.Health is { } health)
		{
			body.opiateHappiness = RemoteCharacterDisplayProjection.OpiateHappinessFromReception(health.ActualOpiateReception);
			body.antidepressantHappiness = RemoteCharacterDisplayProjection.AntidepressantHappiness(
				body.happiness, health.AntidepressantsAmount, health.AntidepressantsCurrentAmount);
		}
	}

	/// <summary>Record what the body last wrote, so the next authority comparison has its baseline.</summary>
	private static void CaptureWritten(HostVitalsDriver driver) =>
		HostVitalsOwned.Capture(driver.Body, driver.LastWrittenBleeds, out driver.LastWrittenBloodVolume, out driver.LastWrittenTotalBleedSpeed);
}
