using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player carry/piggyback operation and the host-owned carry
/// relation. Classic carry picks up an unconscious/dead body; the piggyback
/// mode lets a conscious/alive teammate ride on another player's back (same
/// one-carrier/one-carried relation and body-driver presentation). The host
/// validates against its authoritative character snapshots and commits the
/// relation as a kernel carry fact; the committed batch projection keeps both
/// the host and guest carry mirrors in sync so the carried player's own client
/// follows the carrier. Both the carrier and the carried player may request
/// release. Session lifecycle cleanup is owned here.
/// </summary>
internal sealed class PlayerCarryService : IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly PlayerCharacterAccess _characters;
	private readonly IPlayerInteractionVisibility _visibility;
	private readonly ItemKernelAuthority _kernelAuthority;
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
		IPlayerInteractionVisibility visibility,
		ItemKernelAuthority kernelAuthority,
		ILogger log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_visibility = visibility;
		_kernelAuthority = kernelAuthority;
		_log = log;

		_session.SessionEnded += OnSessionEnded;
		_session.MemberRemoved += OnMemberRemoved;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
	}

	/// <summary>Online UI entry: the local player starts carrying an unconscious/dead player.</summary>
	public void SendCarryStartRequest(ulong targetSteamId) =>
		SendStartRequest(targetSteamId, piggyback: false, requesterIsCarrier: false);

	/// <summary>Online UI entry: the local player climbs onto a conscious-alive teammate's back.</summary>
	public void SendPiggybackRequest(ulong targetSteamId) =>
		SendStartRequest(targetSteamId, piggyback: true, requesterIsCarrier: false);

	/// <summary>Online UI entry: the local player invites a conscious-alive teammate to ride on the local player's back.</summary>
	public void SendCarryOnBackRequest(ulong targetSteamId) =>
		SendStartRequest(targetSteamId, piggyback: true, requesterIsCarrier: true);

	private void SendStartRequest(ulong targetSteamId, bool piggyback, bool requesterIsCarrier)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerCarryStartRequestMsg
		{
			TargetSteamId = targetSteamId,
			Piggyback = piggyback,
			RequesterIsCarrier = requesterIsCarrier,
		};
		if (_session.Role == SessionRole.Host)
		{
			HandleCarryStartRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerCarryStartRequest, msg);
		}
	}

	/// <summary>Online UI entry: the local player releases the player they carry (or themselves from a carrier).</summary>
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

	/// <summary>Host only: a carry/piggyback start request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleCarryStartRequest(ulong sender, PlayerCarryStartRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var requester = sender;
		var requested = msg.TargetSteamId;
		var carrier = requester;
		var carried = requested;
		if (msg.Piggyback && !msg.RequesterIsCarrier)
		{
			// The requester climbs onto the target's back: the target is the
			// carrier, the requester is the carried rider.
			carrier = requested;
			carried = requester;
		}
		// With RequesterIsCarrier=true the requester remains the carrier and the
		// target is the conscious/alive rider, matching the local-as-carrier UI.

		if (carrier == carried || carrier == 0 || carried == 0)
		{
			return;
		}

		if (!_characters.IsInWorld(carrier) || !_characters.IsInWorld(carried))
		{
			_log.LogWarning("[Carry] refused: {Carrier} or {Carried} is not in-world.", carrier, carried);
			return;
		}

		if (!_visibility.HasLineOfSight(requester, requested))
		{
			_log.LogInformation("[Carry] refused: {Requester} cannot see {Requested}.",
				requester, requested);
			return;
		}

		var carriedData = _characters.GetCharacterData(carried);
		if (carriedData?.Health is not { } carriedHealth)
		{
			_log.LogWarning("[Carry] refused: {Carried} has no authoritative health snapshot.", carried);
			return;
		}

		if (msg.Piggyback)
		{
			if (!carriedHealth.Conscious || !carriedHealth.Alive)
			{
				_log.LogInformation("[Piggyback] refused: {Carried} is not conscious/alive and cannot climb.", carried);
				return;
			}
		}
		else if (carriedHealth.Conscious && carriedHealth.Alive)
		{
			// Cooperative default: the classic carry remains for unconscious/dead
			// bodies; conscious-alive targets use the piggyback mode instead.
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

		if (msg.Piggyback)
		{
			if (msg.RequesterIsCarrier)
			{
				_log.LogInformation("[Piggyback] {Carrier} takes {Rider} onto their back.", carrier, carried);
			}
			else
			{
				_log.LogInformation("[Piggyback] {Rider} climbs onto {Carrier}'s back.", carried, carrier);
			}
		}
		else
		{
			_log.LogInformation("[Carry] {Carrier} starts carrying {Carried}.", carrier, carried);
		}

		PublishCarryState(new PlayerCarryStateMsg
		{
			CarrierSteamId = carrier,
			CarriedSteamId = carried,
		});
	}

	/// <summary>Host only: a carry-stop request arrived. The requester may be the carrier or the carried player.</summary>
	public void HandleCarryStopRequest(ulong sender, PlayerCarryStopRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var carried = msg.CarriedSteamId;
		var carrier = ResolveStopCarrier(sender, carried);
		if (carrier == 0)
		{
			_log.LogWarning("[Carry] stop refused: {Sender} is not allowed to end the relation with {Carried}.", sender, carried);
			return;
		}

		_log.LogInformation("[Carry] {Carrier} stops carrying {Carried} (requested by {Sender}).", carrier, carried, sender);
		PublishCarryState(new PlayerCarryStateMsg
		{
			CarrierSteamId = carrier,
			CarriedSteamId = 0,
		});
	}

	private ulong ResolveStopCarrier(ulong sender, ulong carried)
	{
		if (carried == 0)
		{
			return 0;
		}

		if (_carrying.TryGetValue(sender, out var currentCarried) && currentCarried == carried)
		{
			return sender;
		}

		if (_carriedBy.TryGetValue(sender, out var currentCarrier) && sender == carried)
		{
			return currentCarrier;
		}

		return 0;
	}

	/// <summary>Project a committed kernel carry fact into this local mirror and surface it for the Game Adapter/UI.</summary>
	internal void ApplyCommittedCarry(ulong carrierSteamId, ulong carriedSteamId)
	{
		var msg = new PlayerCarryStateMsg
		{
			CarrierSteamId = carrierSteamId,
			CarriedSteamId = carriedSteamId,
		};
		ApplyCarryState(msg);
		CarryStateChanged?.Invoke(msg);
	}

	/// <summary>Clear the carry mirror without raising presentation events (players reset / session reset).</summary>
	internal void ResetCarryMirror()
	{
		_carriedBy.Clear();
		_carrying.Clear();
	}

	/// <summary>Rebuild the carry mirror from a kernel checkpoint (late-join / reconnect restore).</summary>
	internal void RebuildFromCheckpoint(PlayerStateTable? players)
	{
		ResetCarryMirror();
		if (players is null)
		{
			return;
		}

		foreach (var player in players.Players)
		{
			if (player.CarrierOfSteamId is { } carried && carried != 0)
			{
				ApplyCommittedCarry(player.SteamId, carried);
			}
		}
	}

	/// <summary>Read-only UI mirror: who currently carries the given player, if any.</summary>
	public bool TryGetCarrier(ulong carriedSteamId, out ulong carrierSteamId) =>
		_carriedBy.TryGetValue(carriedSteamId, out carrierSteamId);

	/// <summary>Read-only UI mirror: whom the given player currently carries, if any.</summary>
	public bool TryGetCarried(ulong carrierSteamId, out ulong carriedSteamId) =>
		_carrying.TryGetValue(carrierSteamId, out carriedSteamId);

	private void PublishCarryState(PlayerCarryStateMsg msg)
	{
		// The kernel is the single authoritative write. The committed batch
		// projection (PlayerKernelCarryProjection) updates both this host mirror
		// and (via KernelEnvelope) the guest mirrors; no legacy carry wire is sent.
		if (msg.CarriedSteamId == 0)
		{
			if (!_kernelAuthority.TryClearPlayerCarry(
				_session.LocalSteamId,
				msg.CarrierSteamId,
				0,
				out _,
				out var rejection))
			{
				_log.LogWarning(
					"[CarryKernel] clear rejected {Carrier}: {Reason} ({Message}).",
					msg.CarrierSteamId, rejection!.Reason, rejection.Message);
			}

			return;
		}

		if (!_kernelAuthority.TrySetPlayerCarry(
			_session.LocalSteamId,
			msg.CarrierSteamId,
			msg.CarriedSteamId,
			out _,
			out var setRejection))
		{
			_log.LogWarning(
				"[CarryKernel] set rejected {Carrier} -> {Carried}: {Reason} ({Message}).",
				msg.CarrierSteamId, msg.CarriedSteamId, setRejection!.Reason, setRejection.Message);
		}
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
		if (_session.Role == SessionRole.Host)
		{
			// Cleanup is another host mutation: the committed batch projection
			// removes the mirror and broadcasts the clear through KernelEnvelope.
			if (_carrying.TryGetValue(steamId, out var hostCarried))
			{
				_log.LogInformation("[Carry] cleaned up relation involving {SteamId} (carrier).", steamId);
				PublishCarryState(new PlayerCarryStateMsg
				{
					CarrierSteamId = steamId,
					CarriedSteamId = 0,
				});
				return;
			}

			if (_carriedBy.TryGetValue(steamId, out var hostCarrier))
			{
				_log.LogInformation("[Carry] cleaned up relation involving {SteamId} (carried).", steamId);
				PublishCarryState(new PlayerCarryStateMsg
				{
					CarrierSteamId = hostCarrier,
					CarriedSteamId = 0,
				});
			}

			return;
		}

		// Guest mirror only: the host's cleanup broadcast will arrive through the
		// kernel journal; dropping the local relation here keeps the mirror correct
		// even before/if that batch is delayed.
		if (_carrying.TryGetValue(steamId, out var guestCarried))
		{
			_carriedBy.Remove(guestCarried);
			_carrying.Remove(steamId);
		}

		if (_carriedBy.TryGetValue(steamId, out var guestCarrier))
		{
			_carrying.Remove(guestCarrier);
			_carriedBy.Remove(steamId);
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
