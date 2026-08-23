using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The host-authoritative trader-recruit domain (KrokMP-inspired co-op revive):
/// the acting side locates a nearby trader and requests a recruit; the host
/// re-validates the trader gates and the dead player's authoritative snapshot,
/// then sends the revived physiological state directly to that player. The
/// target's local body is healed in place — no inventory wipe, no position
/// teleport, no full character restore. The acting side has no vanilla game
/// method to run, so this is a dedicated request/result path, not a
/// TraderActionKind.
/// </summary>
internal sealed class TraderRecruitCoordinator(
	SessionService session,
	WorldService world,
	ICharacterDataControl characterData,
	CharacterDataSync characterDataSync,
	IOptionsMonitor<RespawnOptions> respawnOptions,
	ILogger<TraderRecruitCoordinator> log)
{
	private const float PositionTolerance = 2f;

	private readonly SessionService _session = session;
	private readonly WorldService _world = world;
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly CharacterDataSync _characterDataSync = characterDataSync;
	private readonly IOptionsMonitor<RespawnOptions> _respawnOptions = respawnOptions;
	private readonly ILogger<TraderRecruitCoordinator> _log = log;

	/// <summary>Host-side used-trader registry (one recruit per trader instance
	/// per world). Cleared with the session; the trader objects themselves are
	/// unmodified.</summary>
	private readonly HashSet<int> _usedTraders = [];

	internal void BindToSession()
	{
		_world.TraderRecruitRequestReceived += OnTraderRecruitRequestReceived;
		_world.TraderRecruitResultReceived += OnTraderRecruitResultReceived;
	}

	internal void Unbind()
	{
		_world.TraderRecruitRequestReceived -= OnTraderRecruitRequestReceived;
		_world.TraderRecruitResultReceived -= OnTraderRecruitResultReceived;
	}

	internal void Reset() => _usedTraders.Clear();

	/// <summary>
	/// Online UI / plugin entry: the local player requests a recruit of a dead
	/// in-world teammate at the nearest trader. Host: handled immediately.
	/// Guest: reported to the host (the host remains the authority — the acting
	/// side only located the trader).
	/// </summary>
	internal bool TryRequest(ulong targetSteamId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return false;
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null)
		{
			return false;
		}

		var trader = FindNearestTrader(body.transform.position);
		if (trader == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] no trader within {Range} — request refused.", TraderRecruitPolicy.RecruitRange);
			return false;
		}

		var msg = new TraderRecruitRequestMsg
		{
			TargetSteamId = targetSteamId,
			TraderPosition = new NetVector2Msg(trader.transform.position.x, trader.transform.position.y),
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleHostRequest(_session.LocalSteamId, msg, trader);
		}
		else
		{
			_world.SendTraderRecruitRequest(msg);
			_log.LogInformation("[TradeRecruit] request sent target={Target} trader=({X:0.0},{Y:0.0}).",
				targetSteamId, msg.TraderPosition.X, msg.TraderPosition.Y);
		}

		return true;
	}

	/// <summary>Host: a guest's recruit request arrived — validate and execute.</summary>
	private void OnTraderRecruitRequestReceived(ulong sender, TraderRecruitRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var trader = FindTraderAt(msg.TraderPosition);
		if (trader == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] trader not found at ({X:0.0},{Y:0.0}) — dropped.", msg.TraderPosition.X, msg.TraderPosition.Y);
			return;
		}

		HandleHostRequest(sender, msg, trader);
	}

	/// <summary>Guest: the host's authoritative revive result arrived — apply it
	/// to the local Body (the host already persisted the revived snapshot).</summary>
	private void OnTraderRecruitResultReceived(TraderRecruitResultMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive
			|| msg.TargetSteamId != _session.LocalSteamId || msg.Health is null)
		{
			return;
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[TradeRecruit] revive result for {Target} but no local body — dropped.", msg.TargetSteamId);
			return;
		}

		_log.LogInformation("[TradeRecruit] applying revive to local body health={Health}.", msg.Health.BrainHealth);
		ApplyRevive(body, msg.Health, msg.Limbs);
	}

	/// <summary>
	/// Host-side execute: re-check every gate against the authoritative trader
	/// and character snapshots, persist the revived snapshot, and deliver the
	/// result (wire for a guest, direct local apply for the host itself).
	/// </summary>
	private void HandleHostRequest(ulong requester, TraderRecruitRequestMsg msg, TraderScript trader)
	{
		var target = msg.TargetSteamId;
		if (requester == target || requester == 0 || target == 0)
		{
			_log.LogWarning("[TradeRecruit] refused: invalid requester/target pair {Requester}→{Target}.", requester, target);
			return;
		}

		if (!RespawnPolicy.CanUseTraderRecruit(_respawnOptions.CurrentValue))
		{
			_log.LogInformation("[TradeRecruit] refused: trader revive disabled by host rules (permadeath={Permadeath}, fromTrader={FromTrader}).",
				_respawnOptions.CurrentValue.Permadeath, _respawnOptions.CurrentValue.ReviveFromTrader);
			return;
		}

		if (!IsInWorld(requester) || !IsInWorld(target))
		{
			_log.LogWarning("[TradeRecruit] refused: {Requester} or {Target} is not in-world.", requester, target);
			return;
		}

		var traderState = TradeExecutor.Read(trader);
		if (!TraderRecruitPolicy.CanRecruit(traderState, used: _usedTraders.Contains(trader.GetInstanceID())))
		{
			_log.LogInformation("[TradeRecruit] refused: trader gates fail (rep={Rep} hostility={Host} build={Build} used={Used}).",
				traderState.Reputation, traderState.Hostility, traderState.BuildHealth, _usedTraders.Contains(trader.GetInstanceID()));
			return;
		}

		var requesterData = GetCharacterData(requester);
		if (requesterData?.Health is not { Alive: true, Conscious: true })
		{
			_log.LogInformation("[TradeRecruit] refused: {Requester} is not alive/conscious.", requester);
			return;
		}

		var targetData = GetCharacterData(target);
		if (!TraderRecruitPolicy.IsDead(targetData))
		{
			_log.LogInformation("[TradeRecruit] refused: {Target} is not dead (or no snapshot).", target);
			return;
		}

		var revived = TraderRecruitPolicy.PrepareRevive(targetData!);
		SaveCharacterData(target, revived);
		_usedTraders.Add(trader.GetInstanceID());

		_log.LogInformation(
			"[TradeRecruit] {Requester} recruited {Target} at trader ({X:0.0},{Y:0.0}); health={Health}.",
			requester, target, trader.transform.position.x, trader.transform.position.y, revived.Health?.BrainHealth);

		if (target == _session.LocalSteamId)
		{
			var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
			if (body != null && revived.Health is { } health) // Unity object — ==
			{
				ApplyRevive(body, health, revived.Limbs);
			}
		}
		else
		{
			_world.SendTraderRecruitResult(target, new TraderRecruitResultMsg
			{
				TargetSteamId = target,
				Health = revived.Health,
				Limbs = [.. revived.Limbs],
			});
		}
	}

	private void ApplyRevive(Body body, CharacterHealthMsg health, IReadOnlyList<CharacterLimbMsg> limbs)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			_characterDataSync.ApplyHealState(body, health, limbs);
			// Re-report immediately so the peer clones and the host's saved
			// snapshot see the revived state without waiting for the next 1 Hz tick.
			_characterDataSync.ReportInventoryChanged(body);
		}
	}

	private CharacterDataMsg? GetCharacterData(ulong steamId) =>
		steamId == _session.LocalSteamId
			? _characterData.GetHostCharacterData()
			: _characterData.GetSavedCharacter(steamId);

	private void SaveCharacterData(ulong steamId, CharacterDataMsg data)
	{
		if (steamId == _session.LocalSteamId)
		{
			_characterData.SaveHostCharacterData(data);
		}
		else
		{
			_characterData.SaveCharacterData(steamId, data);
		}
	}

	private bool IsInWorld(ulong steamId) =>
		steamId == _session.LocalSteamId ? _session.LocalInWorld : _session.IsRemoteInWorld(steamId);

	private static TraderScript? FindNearestTrader(Vector2 position)
	{
		TraderScript? best = null; // Unity object — ==
		var bestDistance = float.MaxValue;
		foreach (var trader in UnityEngine.Object.FindObjectsOfType<TraderScript>())
		{
			var distance = Vector2.Distance(trader.transform.position, position);
			if (distance <= TraderRecruitPolicy.RecruitRange && distance < bestDistance)
			{
				best = trader;
				bestDistance = distance;
			}
		}

		return best;
	}

	/// <summary>Position-keyed trader lookup — same identity rule as the trade domain.</summary>
	private static TraderScript? FindTraderAt(NetVector2Msg position)
	{
		foreach (var trader in UnityEngine.Object.FindObjectsOfType<TraderScript>())
		{
			if (Vector2.Distance(trader.transform.position, new Vector2(position.X, position.Y)) < PositionTolerance)
			{
				return trader;
			}
		}

		return null;
	}
}
