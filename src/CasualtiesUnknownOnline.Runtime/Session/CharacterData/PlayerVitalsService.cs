using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The host's continuous-vitals wire pump (the 2026-10-18 ruling): for every player in the world
/// it reads the THREE values the host owns out of the saved character snapshot and pushes them to
/// that player's own client at 10 Hz, which applies them to its own body.
///
/// <para>
/// The model itself is not here any more. Stage 1 of the health-authority ticket reimplemented the
/// bleeding rules in <c>PlayerBleedingState</c>; stage 2 points the game's own
/// <c>HandleVariableUpdates</c>, <c>HandleBody</c>, <c>HandleBodyTemperature</c>,
/// <c>HandleRadiationSickness</c>, <c>HandlePeriodicChecks</c> and <c>Limb.Update</c> at the host's
/// clone of each remote player, and that clone writes its values back into the saved snapshot
/// after every advance. This service therefore reads the snapshot and sends it: it owns the
/// cadence, the reliability and the refusal of a corrupt row, and nothing else.
/// </para>
///
/// <para>
/// The ownership seam is unchanged: <see cref="ICharacterDataControl.SaveReportedCharacterData"/>
/// keeps a plain report from writing the three values, every event the host arbitrates writes them
/// through <see cref="ICharacterDataControl.SaveCharacterData"/>, and the adapter recognizes such a
/// write as the authoritative input of its clone.
/// </para>
/// </summary>
public sealed class PlayerVitalsService : ICuoService, ISessionReset
{
	/// <summary>The row cadence: 10 Hz keeps a correction smaller than the panel's own rounding.</summary>
	private const float SendIntervalSeconds = 0.1f;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ICharacterDataControl _characters;
	private readonly ITimeSource _time;
	private readonly ILogger<PlayerVitalsService> _log;

	/// <summary>How recent the adapter's last successful advance must be before the row may overwrite the owner's body.</summary>
	private const long ModelAdvancingWindowMs = 250;

	/// <summary>Players whose latest stored snapshot carries a non-finite owned value; the warning is named once, not at 10 Hz.</summary>
	private readonly HashSet<ulong> _refusedRows = [];

	private long _nextSendMs = long.MinValue;

	public PlayerVitalsService(ISessionControl session, PacketSender sender, ICharacterDataControl characters,
		ITimeSource time, ILogger<PlayerVitalsService> log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_time = time;
		_log = log;

		// The refusal window is per session: a stale set must never suppress the first warning of
		// the next one. The modelled values themselves live on the adapter's clones, which die with
		// the session, and on the character table, which the store clears.
		_session.SessionEnded += ResetSessionState;
		_characters.SavedCharactersCleared += ResetSessionState;
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start() => _nextSendMs = long.MinValue; // send on the first host tick; Process time can be negative (signed TickCount)

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

	/// <summary>Session end / cleared table: the rows belong to the dead session — a stale refusal must not outlive it.</summary>
	public void ResetSessionState()
	{
		_refusedRows.Clear();
		_nextSendMs = long.MinValue;
	}

	/// <summary>
	/// The service's one clock step, separated from the pump so a test can drive it. It only
	/// throttles and sends; the values are the adapter's clone's, mirrored into the saved snapshot
	/// after every fixed step.
	/// </summary>
	internal void Tick(long nowMs)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (nowMs < _nextSendMs)
		{
			return;
		}

		_nextSendMs = nowMs + (long)(SendIntervalSeconds * 1000f);

		foreach (var member in _session.Members)
		{
			if (!member.Handshaken || !member.InWorld || member.SteamId == _session.LocalSteamId)
			{
				continue;
			}

			var data = _characters.GetSavedCharacter(member.SteamId);
			if (data is null)
			{
				continue; // no snapshot yet — nothing to send
			}

			// The model must have advanced recently: a clone that cannot be seeded, a refused
			// snapshot or a stopped FixedUpdate (the start gate's timeScale 0) must not keep
			// overwriting the owner's body with frozen host values.
			if (!_characters.IsHostModelAdvancing(member.SteamId, nowMs, ModelAdvancingWindowMs))
			{
				continue;
			}

			if (Capture(member.SteamId, data) is { } row)
			{
				_sender.Send(member.SteamId, NetMsg.PlayerVitals, row, reliable: false);
			}
		}
	}

	/// <summary>
	/// Read the host-owned values out of the stored snapshot, or null when the row must not be
	/// sent. A non-finite value here can only mean an authoritative writer produced one (a plain
	/// report never reaches the snapshot), and it is refused by name: a corrupt row would set the
	/// owner's blood to NaN, aborting its native rules.
	/// </summary>
	private PlayerVitalsMsg? Capture(ulong steamId, CharacterDataMsg data)
	{
		if (data.Health is not { } health)
		{
			return null;
		}

		if (!CharacterDataFinite.IsFinite(health.BloodVolume))
		{
			Refuse(steamId, "blood volume");
			return null;
		}

		if (!CharacterDataFinite.IsFinite(health.TotalBleedSpeed))
		{
			Refuse(steamId, "total bleeding speed");
			return null;
		}

		var msg = new PlayerVitalsMsg
		{
			SteamId = steamId,
			BloodVolume = health.BloodVolume,
			TotalBleedSpeed = health.TotalBleedSpeed,
			RefusedField = _characters.GetReportRefusal(steamId) ?? "",
		};

		foreach (var limb in data.Limbs)
		{
			// A dismembered limb no longer bleeds and its value is not owned any more (the
			// amputation's authority zeroes it on the body); it must not be pushed.
			if (limb.Dismembered)
			{
				continue;
			}

			if (!CharacterDataFinite.IsFinite(limb.BleedAmount))
			{
				Refuse(steamId, $"limb {limb.Index} bleed");
				return null;
			}

			msg.Limbs.Add(new PlayerLimbBleedMsg { Index = limb.Index, BleedAmount = limb.BleedAmount });
		}

		if (_refusedRows.Remove(steamId))
		{
			_log.LogInformation("[Vitals] {Player}'s stored values are finite again; the host resumes the row.", steamId);
		}

		return msg;
	}

	/// <summary>Name the refusal once per player until a usable snapshot arrives.</summary>
	private void Refuse(ulong steamId, string field)
	{
		if (_refusedRows.Add(steamId))
		{
			_log.LogWarning("[Vitals] {Player}'s stored {Field} is not finite — the row is refused; the host keeps its own values.",
				steamId, field);
		}
	}
}
