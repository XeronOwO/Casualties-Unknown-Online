using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The host's continuous-vitals authority (the 2026-10-18 ruling): for every player in the world
/// it keeps a <see cref="PlayerBleedingState"/>, advances it with the game's own bleeding rules,
/// and pushes the row down to that player's client, which applies it to its own body.
///
/// <para>
/// The split is the ruling: EVENTS and everything terminal stay with the affected client (its
/// own judgment produces them and the host never re-derives one); the CONTINUOUS values between
/// events belong to this service, so a client that edits its own bleed or blood volume between
/// two authoritative points is overwritten by the next row instead of being believed.
/// </para>
///
/// <para>
/// The host's saved snapshot carries this service's values, and THAT is the ownership seam:
/// <see cref="ICharacterDataControl.SaveReportedCharacterData"/> keeps the two owned fields away
/// from a plain 1 Hz report, while every event the host arbitrates writes them through
/// <see cref="ICharacterDataControl.SaveCharacterData"/> — including the clone-and-save appliers
/// (a revive, an amputation, a shrapnel wound). So the service re-seeds exactly when the
/// snapshot's owned values stop matching the state's own, which only an event can do.
/// </para>
///
/// <para>
/// No reconciliation yet: this stage computes and pushes the values but does not compare them
/// against the member's report (the comparison is the ticket's stage-2 item 4). Until it lands,
/// a wrong model is silent — the ticket's stage-1 section says so.
/// </para>
/// </summary>
public sealed class PlayerVitalsService : ICuoService, ISessionReset
{
	/// <summary>The row cadence: 10 Hz keeps a correction smaller than the panel's own rounding.</summary>
	private const float SendIntervalSeconds = 0.1f;

	/// <summary>The largest step one tick may advance: a stalled frame must not teleport a bleed.</summary>
	private const float MaxStepSeconds = 0.25f;

	/// <summary>The disagreement a snapshot must exceed before it counts as an event write.</summary>
	private const float AuthorityEpsilon = 0.0005f;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ICharacterDataControl _characters;
	private readonly ITimeSource _time;
	private readonly IPlayerVitalsTuning _tuning;
	private readonly ILogger<PlayerVitalsService> _log;
	private readonly Dictionary<ulong, PlayerBleedingState> _states = [];

	/// <summary>Players whose latest stored snapshot carries a non-finite value; the warning is named once, not at 10 Hz.</summary>
	private readonly HashSet<ulong> _refusedReports = [];

	private long _nextSendMs = long.MinValue;
	private long _lastAdvanceMs;

	public PlayerVitalsService(ISessionControl session, PacketSender sender, ICharacterDataControl characters,
		ITimeSource time, IPlayerVitalsTuning tuning, ILogger<PlayerVitalsService> log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_time = time;
		_tuning = tuning;
		_log = log;
		_lastAdvanceMs = time.NowMs;

		// The modelled values belong to ONE session and ONE run: a new session or a cleared
		// character table (new run / world restore) must never hand a stale blood volume to the
		// next bodies. ISessionReset is only a declaration; the subscription is the reacting half.
		_session.SessionEnded += ResetSessionState;
		_characters.SavedCharactersCleared += ResetSessionState;
	}

	void ICuoService.Initialize() => _lastAdvanceMs = _time.NowMs;

	void ICuoService.Start()
	{
		_lastAdvanceMs = _time.NowMs;
		_nextSendMs = long.MinValue; // send on the first host tick; Process time can be negative (signed TickCount)
	}

	void ICuoService.Stop()
	{
	}

	void ICuoService.Update() => Tick(_time.NowMs);

	/// <summary>ICuoService is IDisposable: the container disposes the singleton, and a dispose may run more than once.</summary>
	public void Dispose()
	{
		_session.SessionEnded -= ResetSessionState;
		_characters.SavedCharactersCleared -= ResetSessionState;
		ResetSessionState();
	}

	/// <summary>Session end / cleared table: the rows belong to the dead session — a stale blood volume must never reach the next bodies.</summary>
	public void ResetSessionState()
	{
		_states.Clear();
		_refusedReports.Clear();
	}

	/// <summary>Test/observability seam (InternalsVisibleTo): how many players the pump currently models.</summary>
	internal int TrackedPlayers => _states.Count;

	/// <summary>
	/// The service's one clock step, separated from the pump so a test can drive it. The step is
	/// measured from the PREVIOUS ADVANCE, never from the previous frame: <see cref="ICuoService.Update"/>
	/// runs every frame, so a per-frame delta would advance the model by one frame per 10 Hz row and
	/// every drain would run at a sixth of the game's speed.
	/// </summary>
	internal void Tick(long nowMs)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			// No body exists outside the host's live world; re-anchor the clock so the first host
			// tick cannot advance across the time spent in a menu.
			_lastAdvanceMs = nowMs;
			return;
		}

		if (nowMs < _nextSendMs)
		{
			return;
		}

		_nextSendMs = nowMs + (long)(SendIntervalSeconds * 1000f);

		var dt = (nowMs - _lastAdvanceMs) / 1000f;
		_lastAdvanceMs = nowMs;

		if (dt <= 0f)
		{
			return;
		}

		if (dt > MaxStepSeconds)
		{
			dt = MaxStepSeconds;
		}

		foreach (var member in _session.Members)
		{
			// Only a body in the world is simulated: a member in the menu has no Body/Limb to
			// update natively, so advancing its saved values would be a CUO invention.
			if (!member.Handshaken || !member.InWorld || member.SteamId == _session.LocalSteamId)
			{
				continue;
			}

			var data = _characters.GetSavedCharacter(member.SteamId);
			if (data is null)
			{
				continue; // no snapshot yet — nothing authoritative to advance from
			}

			if (!AcceptReport(member.SteamId, data))
			{
				// The claim is refused by name; the host keeps its own values and still sends
				// them, so the client's body is corrected by the authority rather than by a
				// corrupt report. A state that was never seeded has nothing to send.
				if (_states.TryGetValue(member.SteamId, out var held))
				{
					held.Advance(dt);
					WriteBack(held, data);
					Send(member.SteamId, held);
				}

				continue;
			}

			if (!_states.TryGetValue(member.SteamId, out var state))
			{
				state = Seed(member.SteamId, data);
			}
			else if (AuthorityChanged(state, data))
			{
				state.ApplyAuthority(data);
				state.ApplyInputs(data);
				_log.LogInformation("[Vitals] re-seeded {Player}'s bleeding state after an authoritative event (blood {Blood:F1}).",
					member.SteamId, state.BloodVolume);
			}
			else
			{
				state.ApplyInputs(data);
				state.Advance(dt);
			}

			WriteBack(state, data);
			Send(member.SteamId, state);
		}
	}

	private PlayerBleedingState Seed(ulong steamId, CharacterDataMsg data)
	{
		var state = PlayerBleedingState.Seed(data, _tuning.HealingRate, _tuning.BleedRate);
		_states[steamId] = state;
		_log.LogInformation("[Vitals] seeded {Player}'s bleeding state from an authoritative snapshot (blood {Blood:F1}).",
			steamId, state.BloodVolume);
		return state;
	}

	private void Send(ulong steamId, PlayerBleedingState state) =>
		_sender.Send(steamId, NetMsg.PlayerVitals, Capture(steamId, state), reliable: false);

	/// <summary>
	/// A report is a CLAIM. The values it states are used only as model inputs, and a non-finite
	/// one is refused by name: it would poison the state (and <see cref="Math.Sign(float)"/> throws
	/// on NaN, which would abort the pump for every player). At most one warning per player until
	/// a usable report arrives.
	/// </summary>
	private bool AcceptReport(ulong steamId, CharacterDataMsg data)
	{
		if (FirstNonFinite(data) is not { } field)
		{
			if (_refusedReports.Remove(steamId))
			{
				_log.LogInformation("[Vitals] {Player}'s report is finite again; the host resumes using its inputs.", steamId);
			}

			return true;
		}

		if (_refusedReports.Add(steamId))
		{
			_log.LogWarning("[Vitals] {Player}'s report carries a non-finite {Field} — the host keeps its own values and ignores the report.",
				steamId, field);
		}

		return false;
	}

	private static string? FirstNonFinite(CharacterDataMsg data)
	{
		if (data.Health is { } health)
		{
			if (!IsFinite(health.BloodVolume))
			{
				return "blood volume";
			}

			if (!IsFinite(health.BloodViscosity))
			{
				return "blood viscosity";
			}

			if (!IsFinite(health.VenomCurrent))
			{
				return "venom";
			}

			if (!IsFinite(health.Hunger))
			{
				return "hunger";
			}

			if (!IsFinite(health.InternalBleeding))
			{
				return "internal bleeding";
			}
		}

		foreach (var limb in data.Limbs)
		{
			if (!IsFinite(limb.BleedAmount))
			{
				return $"limb {limb.Index} bleed";
			}

			if (!IsFinite(limb.SkinHealth))
			{
				return $"limb {limb.Index} skin health";
			}

			if (!IsFinite(limb.BandageSlowAmount))
			{
				return $"limb {limb.Index} bandage";
			}
		}

		return null;
	}

	/// <summary>net48 has no <c>float.IsFinite</c>.</summary>
	private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

	/// <summary>
	/// True when the host's own snapshot no longer carries the state's values — which only an
	/// event the host arbitrated can do, because a report is not allowed to write the owned
	/// fields (<see cref="ICharacterDataControl.SaveReportedCharacterData"/>).
	/// </summary>
	private static bool AuthorityChanged(PlayerBleedingState state, CharacterDataMsg data)
	{
		if (data.Health is { } health && Math.Abs(health.BloodVolume - state.BloodVolume) > AuthorityEpsilon)
		{
			return true;
		}

		for (var i = 0; i < data.Limbs.Count; i++)
		{
			var limb = data.Limbs[i];
			if (state.TryGetBleed(limb.Index, out var bleed) && Math.Abs(limb.BleedAmount - bleed) > AuthorityEpsilon)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Keep the host's saved snapshot carrying the model's values, so the next tick's comparison is exact.</summary>
	private static void WriteBack(PlayerBleedingState state, CharacterDataMsg data)
	{
		if (data.Health is { } health)
		{
			health.BloodVolume = state.BloodVolume;
		}

		for (var i = 0; i < data.Limbs.Count; i++)
		{
			var limb = data.Limbs[i];
			if (state.TryGetBleed(limb.Index, out var bleed))
			{
				limb.BleedAmount = bleed;
			}
		}
	}

	private static PlayerVitalsMsg Capture(ulong steamId, PlayerBleedingState state)
	{
		var msg = new PlayerVitalsMsg
		{
			SteamId = steamId,
			BloodVolume = state.BloodVolume,
			TotalBleedSpeed = state.TotalBleedSpeed,
		};

		for (var i = 0; i < state.LimbCount; i++)
		{
			if (state.TryGetBleed(i, out var bleed))
			{
				msg.Limbs.Add(new PlayerLimbBleedMsg { Index = i, BleedAmount = bleed });
			}
		}

		return msg;
	}
}
