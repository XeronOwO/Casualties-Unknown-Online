using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player carry/release operation and the host-owned carry relation.
/// The host validates against its authoritative character snapshots, records
/// one carrier/one carried relation, and broadcasts the authoritative state so
/// the carried player's own client follows the carrier. Guests keep the same
/// relation mirror from the broadcast. Session lifecycle cleanup is owned here.
/// </summary>
internal sealed class PlayerCarryService : IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly PlayerCharacterAccess _characters;
	private readonly ILogger _log;

	/// <summary>Host-owned carry table: carried SteamId → carrier SteamId.</summary>
	private readonly Dictionary<ulong, ulong> _carriedBy = [];

	/// <summary>Host-owned carry table: carrier SteamId → carried SteamId (kept in lockstep for O(1) lookups).</summary>
	private readonly Dictionary<ulong, ulong> _carrying = [];

	/// <summary>An authoritative carry relation changed — the Game Adapter sets/clears the local carried-body driver; the UI refreshes buttons.</summary>
	public event Action<PlayerCarryStateMsg>? CarryStateChanged;

	public PlayerCarryService(
		ISessionControl session,
		PacketSender sender,
		PlayerCharacterAccess characters,
		ILogger log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_log = log;

		_session.SessionEnded += OnSessionEnded;
		_session.MemberRemoved += OnMemberRemoved;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
	}

	/// <summary>Online UI entry: the local player starts carrying another player.</summary>
	public void SendCarryStartRequest(ulong targetSteamId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerCarryStartRequestMsg { TargetSteamId = targetSteamId };
		if (_session.Role == SessionRole.Host)
		{
			HandleCarryStartRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerCarryStartRequest, msg);
		}
	}

	/// <summary>Online UI entry: the local player releases the player they carry.</summary>
	public void SendCarryStopRequest(ulong carriedSteamId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerCarryStopRequestMsg { CarriedSteamId = carriedSteamId };
		if (_session.Role == SessionRole.Host)
		{
			HandleCarryStopRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerCarryStopRequest, msg);
		}
	}

	/// <summary>Host only: a carry-start request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleCarryStartRequest(ulong sender, PlayerCarryStartRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var carrier = sender;
		var carried = msg.TargetSteamId;
		if (carrier == carried || carrier == 0 || carried == 0)
		{
			return;
		}

		if (!_characters.IsInWorld(carrier) || !_characters.IsInWorld(carried))
		{
			_log.LogWarning("[Carry] refused: {Carrier} or {Carried} is not in-world.", carrier, carried);
			return;
		}

		// Cooperative default: only an unconscious or dead body can be carried.
		// The Online UI surfaces the button only in that state; the host
		// re-checks the authoritative snapshot here.
		var target = _characters.GetCharacterData(carried);
		if (target?.Health is not { } health || (health.Conscious && health.Alive))
		{
			_log.LogInformation("[Carry] refused: {Carried} is conscious/alive and not carryable.", carried);
			return;
		}

		var carrierData = _characters.GetCharacterData(carrier);
		if (carrierData?.Health is not { } carrierHealth || !carrierHealth.Conscious || !carrierHealth.Alive)
		{
			_log.LogInformation("[Carry] refused: {Carrier} is not conscious/alive and cannot carry.", carrier);
			return;
		}

		if (TryGetCarrier(carrier, out _) || TryGetCarried(carrier, out _)
			|| TryGetCarrier(carried, out _) || TryGetCarried(carried, out _))
		{
			_log.LogInformation("[Carry] refused: {Carrier} or {Carried} already participates in a carry relation.", carrier, carried);
			return;
		}

		_carriedBy[carried] = carrier;
		_carrying[carrier] = carried;
		_log.LogInformation("[Carry] {Carrier} starts carrying {Carried}.", carrier, carried);
		PublishCarryState(new PlayerCarryStateMsg
		{
			CarrierSteamId = carrier,
			CarriedSteamId = carried,
		});
	}

	/// <summary>Host only: a carry-stop request arrived.</summary>
	public void HandleCarryStopRequest(ulong sender, PlayerCarryStopRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var carrier = sender;
		var carried = msg.CarriedSteamId;
		if (carrier == 0 || carried == 0 || !_carrying.TryGetValue(carrier, out var current) || current != carried)
		{
			_log.LogWarning("[Carry] stop refused: {Carrier} is not carrying {Carried}.", carrier, carried);
			return;
		}

		_carriedBy.Remove(carried);
		_carrying.Remove(carrier);
		_log.LogInformation("[Carry] {Carrier} stops carrying {Carried}.", carrier, carried);
		PublishCarryState(new PlayerCarryStateMsg
		{
			CarrierSteamId = carrier,
			CarriedSteamId = 0,
		});
	}

	/// <summary>Wire handler path: a carry-state broadcast arrived — update the local mirror and surface it for the Game Adapter/UI.</summary>
	public void FireCarryStateReceived(PlayerCarryStateMsg msg)
	{
		ApplyCarryState(msg);
		CarryStateChanged?.Invoke(msg);
	}

	/// <summary>Read-only UI mirror: who currently carries the given player, if any.</summary>
	public bool TryGetCarrier(ulong carriedSteamId, out ulong carrierSteamId) =>
		_carriedBy.TryGetValue(carriedSteamId, out carrierSteamId);

	/// <summary>Read-only UI mirror: whom the given player currently carries, if any.</summary>
	public bool TryGetCarried(ulong carrierSteamId, out ulong carriedSteamId) =>
		_carrying.TryGetValue(carrierSteamId, out carriedSteamId);

	private void PublishCarryState(PlayerCarryStateMsg msg)
	{
		// The host applies its own side locally; every guest receives the same
		// authoritative state (including the two participants).
		ApplyCarryState(msg);
		CarryStateChanged?.Invoke(msg);
		_sender.SendToAll(_session.Members
			.Where(m => m.SteamId != _session.LocalSteamId)
			.Select(m => m.SteamId), NetMsg.PlayerCarryState, msg);
	}

	private void ApplyCarryState(PlayerCarryStateMsg msg)
	{
		if (msg.CarriedSteamId == 0)
		{
			if (_carrying.TryGetValue(msg.CarrierSteamId, out var oldCarried))
			{
				_carriedBy.Remove(oldCarried);
				_carrying.Remove(msg.CarrierSteamId);
			}

			return;
		}

		_carriedBy[msg.CarriedSteamId] = msg.CarrierSteamId;
		_carrying[msg.CarrierSteamId] = msg.CarriedSteamId;
	}

	// ---- Session cleanup (host-owned carry table + guest mirror) ----

	private void OnSessionEnded()
	{
		_carriedBy.Clear();
		_carrying.Clear();
	}

	private void OnMemberRemoved(ulong steamId) => ClearIfInvolved(steamId);

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (!inWorld)
		{
			ClearIfInvolved(steamId);
		}
	}

	private void ClearIfInvolved(ulong steamId)
	{
		var releasedCarrier = 0UL;
		ulong? releasedCarried = null;

		if (_carrying.TryGetValue(steamId, out var oldCarried))
		{
			_carriedBy.Remove(oldCarried);
			_carrying.Remove(steamId);
			releasedCarrier = steamId;
			releasedCarried = oldCarried;
		}

		if (_carriedBy.TryGetValue(steamId, out var oldCarrier))
		{
			_carrying.Remove(oldCarrier);
			_carriedBy.Remove(steamId);
			releasedCarrier = oldCarrier;
			releasedCarried = steamId;
		}

		if (releasedCarrier != 0 && releasedCarried is { } carried)
		{
			_log.LogInformation("[Carry] cleaned up relation involving {SteamId}.", steamId);
			if (_session.Role == SessionRole.Host)
			{
				PublishCarryState(new PlayerCarryStateMsg
				{
					CarrierSteamId = releasedCarrier,
					CarriedSteamId = 0,
				});
			}
		}
	}

	/// <summary>Unsubscribe from session lifecycle events.</summary>
	public void Dispose()
	{
		_session.SessionEnded -= OnSessionEnded;
		_session.MemberRemoved -= OnMemberRemoved;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
	}
}
