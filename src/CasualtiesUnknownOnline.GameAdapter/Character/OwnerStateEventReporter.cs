using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The LOCAL body's owner-state event: the full post-event limb/health payload a limb latch, an injury rise and the heal reset all send, plus the injury trigger
/// that decides when a rise in a limb's bleed is an injury the owner must report.
/// </summary>
/// <remarks>
/// Its own type because the trigger owns three pieces of state (which body it is watching, the last posted bleed set, and the host row's apply counter) and
/// because the payload is the one capture the owner-state event has: keeping the baseline next to the payload it decides about is what stops a rise from being
/// re-baselined by the host's own authority push.
/// </remarks>
internal sealed class OwnerStateEventReporter(
	ISessionControl session,
	ICharacterDataControl characterData,
	Func<Body, CharacterHealthMsg> captureHealth,
	Func<Body, IReadOnlyList<CharacterLimbMsg>> captureLimbs,
	ILogger log)
{
	private Body? _injuryBody; // Unity object — ==
	private float[] _injuryBleedBaseline = [];
	private int _lastRowApplyCount;

	/// <summary>
	/// The local body's limb latch changed (BreakBone/MendBone/Dislocate/UnDislocate/Dismember — the patch verified the write): capture the body's FULL post-event
	/// terminal state (every limb + the body health — Dismember also deactivates the lower limbs and mutates the connected limbs, Limb.cs:91-145) and report it (guest
	/// to host) or broadcast it (host) as the dedicated LimbStateEvent event. The limb indices into Body.limbs ride the message (the same index space the 1 Hz
	/// snapshot uses).
	/// </summary>
	internal void ReportLimbStateEvent(Limb limb)
	{
		if (!session.SessionActive)
		{
			return;
		}

		var body = limb.body;
		if (body == null) // Unity object — ==
		{
			log.LogWarning("[LimbEvent] limb has no body — not reported.");
			return;
		}

		for (var i = 0; i < body.limbs.Length; i++)
		{
			if (body.limbs[i] == limb) // Unity objects — ==
			{
				characterData.SendLimbStateEvent(Capture(body));
				log.LogInformation("[LimbEvent] reported local limb {Limb}.", i);
				return;
			}
		}

		log.LogWarning("[LimbEvent] limb not found in its body's limbs array — not reported.");
	}

	/// <summary>
	/// Send the owner's FULL limb/body state as the owner-state event. A limb latch (the patches) and an injury (the bleed-rise detector) both use it: the payload
	/// is the post-event state, so the host applies the event's limbs (the injury's bleed included) while
	/// <see cref="EnemyTerminalStateApplier.ApplyLimbState"/> keeps the host's two continuous blood values. The host relays it to the other guests like any other
	/// owner event.
	/// </summary>
	internal void ReportOwnerStateEvent(Body body, string reason)
	{
		if (!session.SessionActive || body == null) // Unity object — ==
		{
			return;
		}

		characterData.SendLimbStateEvent(Capture(body));
		log.LogInformation("[LimbEvent] reported local owner state ({Reason}).", reason);
	}

	/// <summary>
	/// Detect a local injury: the owner's own simulation raised a limb's bleed. A RISE is the one change the plain report may not carry (the host owns the value),
	/// so it is reported at once as the full owner state. Two things are deliberately NOT injuries: the HOST's own authority push (a row raised the value —
	/// absorbed by the apply counter) and the value a restore just wrote (the baseline is cleared across a restore). The check runs per frame, not at the row rate, so
	/// the next 10 Hz row cannot overwrite a rise before it is seen.
	/// </summary>
	/// <returns>True when a rise rose this frame and the caller must send the owner-state event.</returns>
	internal bool TrackInjury(Body body, int appliedRowCount)
	{
		var limbs = body.limbs;
		var current = new float[limbs.Length];
		for (var i = 0; i < limbs.Length; i++)
		{
			current[i] = limbs[i].bleedAmount;
		}

		if (!ReferenceEquals(_injuryBody, body) || _injuryBleedBaseline.Length != limbs.Length)
		{
			_injuryBody = body;
			_injuryBleedBaseline = current;
			_lastRowApplyCount = appliedRowCount;
			return false; // a fresh body has no injury to report yet
		}

		if (appliedRowCount != _lastRowApplyCount)
		{
			// The host pushed its authoritative values (or an event it arbitrated). Those raises are the host's, not a new local wound: adopt them as the baseline
			// and do not report.
			_lastRowApplyCount = appliedRowCount;
			_injuryBleedBaseline = current;
			return false;
		}

		var rose = PlayerInjuryRule.AnyBleedRose(_injuryBleedBaseline, current);
		_injuryBleedBaseline = current;
		return rose;
	}

	/// <summary>Drop the injury baseline: a restore writes limb state, and a rise it causes is not an owner-judged wound.</summary>
	internal void ResetInjury() => _injuryBody = null;

	private LimbStateEventMsg Capture(Body body) =>
		new()
		{
			OwnerSteamId = session.LocalSteamId,
			Health = captureHealth(body),
			Limbs = [.. captureLimbs(body)],
		};
}
