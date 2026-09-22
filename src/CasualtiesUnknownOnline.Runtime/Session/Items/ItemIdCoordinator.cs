using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item-instance-id coordination: per-guest counter watermarks (guest →
/// host reports, host → guest grants on handshake completion — a
/// crashed-and-rejoined guest's counter restarts from zero and would reuse ids
/// the host's tables still hold) and the carried-inventory registration (the
/// guest's self-assigned starting-supply ids, registered in the transfer table
/// so its use/slot reports arbitrate normally). The registration is reported as
/// an ABSOLUTE set and repeated on the cadence of
/// <see cref="CarriedInventoryReportSchedule"/> — sync-coverage row I8: one
/// frame lost in the lazy-P2P swallow window must heal without a reconnect, and
/// ids the guest self-assigns later (a crafted product) must converge too. Ids
/// are (counter &lt;&lt; 32) | SteamId — the space is per-SteamId, so a
/// per-guest watermark is all the coordination needed.
/// Split out of ItemService when the 600-line gate demanded it.
/// </summary>
public sealed class ItemIdCoordinator : IDisposable, ISessionReset
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ItemArbitration _arbitration;
	private readonly ITimeSource _time;
	private readonly ILogger _log;

	/// <summary>Guest side: the carried-registration cadence (row I8) — the dense window that heals a swallowed registration, then the steady re-assertion.</summary>
	private readonly CarriedInventoryReportSchedule _carriedRegistration = new();

	/// <summary>Guest side: the largest carried set this session has actually registered — the baseline the inert-capture check compares an empty capture against.</summary>
	private int _registeredItems;

	/// <summary>Whether the inert-capture warning was already written for the current window.</summary>
	private bool _emptyCaptureWarned;

	/// <summary>Host side: each guest's item-id counter high-water mark (guest → host reports). Granted back on handshake completion — the guest resumes from watermark + 1.</summary>
	private readonly Dictionary<ulong, ulong> _watermarks = [];

	/// <summary>Guest side: the host granted the id counter high-water mark (join/reconnect) — the adapter resumes the allocator from counter + 1.</summary>
	public event Action<ulong>? ItemIdWatermarkReceived;

	public ItemIdCoordinator(ISessionControl session, PacketSender sender, ItemArbitration arbitration, ITimeSource time, ILogger log)
	{
		_session = session;
		_sender = sender;
		_arbitration = arbitration;
		_time = time;
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

	/// <inheritdoc />
	public void Dispose() => _session.MemberAdded -= OnMemberAdded;

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

	/// <summary>Session ended: id watermarks and the registration window are session-scoped — a new lobby's host records fresh grants from the new handshakes, and the guest's next world opens its own window.</summary>
	public void ResetSessionState()
	{
		_watermarks.Clear();
		_carriedRegistration.Reset();
		_registeredItems = 0;
		_emptyCaptureWarned = false;
	}

	/// <summary>
	/// Guest only: the carried inventory with self-assigned ids — the host
	/// registers it in the guest's transfer table. The report is ABSOLUTE and
	/// REPEATABLE (row I8): every send spends one step of the registration
	/// window, and an empty capture spends a step too, so the pump can drive the
	/// cadence without capturing the body again on every frame.
	/// </summary>
	public void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var opened = _carriedRegistration.BurstRemaining == CarriedInventoryReportSchedule.BurstReports;
		_carriedRegistration.MarkReported(_time.NowMs);
		if (items.Count == 0)
		{
			if (_registeredItems > 0 && !_emptyCaptureWarned)
			{
				// A window that captures nothing while this session HAS registered a set spends
				// its whole budget sending nothing — the registration silently stops converging.
				// That is the inert-capture failure mode (a capture that skips whatever it has
				// already reported), so it is named instead of passing as "nothing to register".
				_emptyCaptureWarned = true;
				_log.LogWarning("[CarriedInventory] the registration captured nothing although this session already registered {Count} carried item(s) — if the guest still carries them, the capture has stopped seeing them and the re-report cannot converge.",
					_registeredItems);
			}
			else
			{
				_log.LogDebug("[CarriedInventory] nothing to register on this step of the window.");
			}

			return;
		}

		_registeredItems = Math.Max(_registeredItems, items.Count);
		_emptyCaptureWarned = false;

		if (opened)
		{
			_log.LogInformation("[CarriedInventory] reported {Count} carried item(s) with self-assigned ids — re-reporting every {Interval} ms while the window is open.",
				items.Count, CarriedInventoryReportSchedule.BurstIntervalMs);
		}
		else
		{
			_log.LogDebug("[CarriedInventory] re-reported {Count} carried item(s) with self-assigned ids ({Remaining} dense report(s) left in this window).",
				items.Count, _carriedRegistration.BurstRemaining);
		}

		_sender.Send(_session.HostSteamId, NetMsg.CarriedInventory, new CarriedInventoryMsg { Items = [.. items] });
	}

	/// <summary>Guest only: open the carried-registration window — the local generation finished, or the host granted the id watermark (the join/reconnect signal: a rejoined guest does not re-run its world generation, so that grant is its only registration edge).</summary>
	public void ArmCarriedInventoryRegistration(string reason)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_carriedRegistration.Arm(_time.NowMs);
		_emptyCaptureWarned = false; // a fresh window may name its own failure
		_log.LogDebug("[CarriedInventory] registration window opened: {Reason}.", reason);
	}

	/// <summary>Guest only: whether the registration cadence wants a report now — the Game Adapter captures the current carried set and calls <see cref="SendCarriedInventory"/> when it does. Always false on the host and outside an active session.</summary>
	public bool IsCarriedInventoryRegistrationDue() =>
		_session.Role == SessionRole.Guest && _session.SessionActive && _carriedRegistration.IsDue(_time.NowMs);

	/// <summary>Host only: a guest's carried inventory with self-assigned ids arrived — register it in the guest's transfer table (its use/slot reports then arbitrate normally) and surface the fact-table entries (the host's clone of the guest renders the supplies immediately, and the snapshot divergence check sees the entries as already-known instead of a phantom pickup).</summary>
	public void FireCarriedInventoryReceived(ulong sender, IReadOnlyList<CharacterItemMsg> items)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_arbitration.RegisterCarried(sender, items);
		CarriedInventoryReceived?.Invoke(sender, items);
	}

	/// <summary>Host side: a guest's self-assigned carried inventory arrived — the adapter merges it into the guest's fact table (clone render + snapshot divergence baseline).</summary>
	public event Action<ulong, IReadOnlyList<CharacterItemMsg>>? CarriedInventoryReceived;
}
