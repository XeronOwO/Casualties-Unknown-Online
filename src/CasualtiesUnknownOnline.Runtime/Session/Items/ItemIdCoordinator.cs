using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item-instance-id coordination: per-guest counter watermarks (guest →
/// host reports, host → guest grants on handshake completion — a
/// crashed-and-rejoined guest's counter restarts from zero and would reuse ids
/// the host's tables still hold) and the carried-inventory registration (the
/// guest's self-assigned starting-supply ids, reported once its generation
/// finished, registered in the transfer table so its use/slot reports
/// arbitrate normally). Ids are (counter &lt;&lt; 32) | SteamId — the space is
/// per-SteamId, so a per-guest watermark is all the coordination needed.
/// Split out of ItemService when the 600-line gate demanded it.
/// </summary>
public sealed class ItemIdCoordinator
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ItemArbitration _arbitration;
	private readonly ILogger _log;

	/// <summary>Host side: each guest's item-id counter high-water mark (guest → host reports). Granted back on handshake completion — the guest resumes from watermark + 1.</summary>
	private readonly Dictionary<ulong, ulong> _watermarks = [];

	/// <summary>Guest side: the host granted the id counter high-water mark (join/reconnect) — the adapter resumes the allocator from counter + 1.</summary>
	public event Action<ulong>? ItemIdWatermarkReceived;

	public ItemIdCoordinator(ISessionControl session, PacketSender sender, ItemArbitration arbitration, ILogger log)
	{
		_session = session;
		_sender = sender;
		_arbitration = arbitration;
		_log = log;
		// The handshake-completion event — grant the watermark on join and on
		// reconnect (the presence table is stable across reconnects, so the
		// event re-fires; the recorded watermark is the resume point).
		session.MemberAdded += OnMemberAdded;
	}

	private void OnMemberAdded(ulong steamId)
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			var watermark = _watermarks.TryGetValue(steamId, out var w) ? w : 0;
			GrantItemIdWatermark(steamId, watermark);
		}
	}

	/// <summary>Guest only: an item-instance id was allocated locally — report the counter high-water mark (the host grants it back on a reconnect).</summary>
	public void SendItemIdWatermark(ulong counter)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.ItemIdWatermark, new ItemIdWatermarkMsg { Counter = counter });
	}

	/// <summary>Host only: grant a member's id watermark (its allocations may resume from counter + 1 — 0 = it never allocated).</summary>
	public void GrantItemIdWatermark(ulong targetSteamId, ulong counter)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.ItemIdWatermark, new ItemIdWatermarkMsg { Counter = counter });
		_log.LogInformation("[IdWatermark] granted {Counter} to {Peer}.", counter, targetSteamId);
	}

	/// <summary>The id counter high-water mark arrived: host records it (the reconnect grant point), guest applies it (resume from counter + 1).</summary>
	public void FireItemIdWatermarkReceived(ulong sender, ulong counter)
	{
		if (_session.Role == SessionRole.Host)
		{
			if (!_watermarks.TryGetValue(sender, out var w) || counter > w)
			{
				_watermarks[sender] = counter;
			}

			return;
		}

		ItemIdWatermarkReceived?.Invoke(counter);
	}

	/// <summary>Guest only: the carried inventory with self-assigned ids (the local generation finished) — the host registers it in the guest's transfer table.</summary>
	public void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || items.Count == 0)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.CarriedInventory, new CarriedInventoryMsg { Items = [.. items] });
	}

	/// <summary>Host only: a guest's carried inventory with self-assigned ids arrived — register it in the guest's transfer table (its use/slot reports then arbitrate normally).</summary>
	public void FireCarriedInventoryReceived(ulong sender, IReadOnlyList<CharacterItemMsg> items)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_arbitration.RegisterCarried(sender, items);
	}
}
