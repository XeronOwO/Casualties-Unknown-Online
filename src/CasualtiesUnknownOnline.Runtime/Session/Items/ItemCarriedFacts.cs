using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The carried-inventory statement and the item-id coordination: a GUEST's absolute statement of the
/// ids it holds (registered in the host's transfer table), a HOST's one-shot registration of its own
/// authority, the watermark forwards, the restored-id seed, and the host's carried-fact publish.
///
/// Extracted from <see cref="ItemService"/> as the architecture watchlist's named seam; the facade
/// keeps every public member as a one-line forward, so no call site changes.
/// </summary>
internal sealed class ItemCarriedFacts
{
	private readonly ISessionControl _session;
	private readonly ItemIdCoordinator _idCoordinator;
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly ItemCarriedSyncService _carriedSync = new();
	private readonly ItemOwnCarriedRegistration _ownCarried;

	public ItemCarriedFacts(
		ISessionControl session,
		ItemIdCoordinator idCoordinator,
		ItemKernelAuthority kernelAuthority,
		ILogger log)
	{
		_session = session;
		_idCoordinator = idCoordinator;
		_kernelAuthority = kernelAuthority;
		_ownCarried = new ItemOwnCarriedRegistration(session, SendItemCarriedSync, log);
	}

	// ===== Item-id coordination =====

	public void SendItemIdWatermark(ulong counter) => _idCoordinator.SendItemIdWatermark(counter);

	/// <summary>
	/// Report this client's carried inventory with the ids it holds. A GUEST sends the absolute
	/// statement the host registers in its transfer table; a HOST writes the same set into its own
	/// authority — nobody else can, and without it a host item the player has not used, moved or
	/// dropped yet has neither a row nor an id for any verb (or any member's record) to name.
	/// </summary>
	public void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items)
	{
		if (_session.Role == SessionRole.Host)
		{
			_ownCarried.Register(items);
			return;
		}

		_idCoordinator.SendCarriedInventory(items);
	}

	/// <summary>
	/// Open the carried-registration opportunity, whichever side this client is: a GUEST opens the
	/// report window the cadence spreads over its dense frame; a HOST owes the one-shot the class
	/// above owns. The edges that arm it are the same fact on both sides — the local generation
	/// finished, or the session (re)appeared on the world this client is already in.
	/// </summary>
	public void ArmCarriedInventoryRegistration(string reason)
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			_ownCarried.Arm(reason);
			return;
		}

		_idCoordinator.ArmCarriedInventoryRegistration(reason);
	}

	/// <summary>Whether the adapter should capture this client's carried set now: the guest's report cadence, or the host's unspent one-shot.</summary>
	public bool IsCarriedInventoryRegistrationDue() =>
		_session.Role == SessionRole.Host
			? _ownCarried.IsDue
			: _idCoordinator.IsCarriedInventoryRegistrationDue();

	public void GrantItemIdWatermark(ulong targetSteamId, ulong counter) => _idCoordinator.GrantItemIdWatermark(targetSteamId, counter);

	public void FireItemIdWatermarkReceived(ulong sender, ulong counter) => _idCoordinator.FireItemIdWatermarkReceived(sender, counter);

	public void FireCarriedInventoryReceived(ulong sender, IReadOnlyList<CharacterItemMsg> items) => _idCoordinator.FireCarriedInventoryReceived(sender, items);

	/// <summary>
	/// The restore's id seed (<see cref="IRestoredItemIdSeed"/>): every id the applied archive
	/// re-issued, handed to the one rule that resumes the counters above them.
	/// </summary>
	public void SeedRestoredItemIds(IReadOnlyList<ulong> instanceIds) => _idCoordinator.ResumeRestoredIds(instanceIds);

	public event Action<ulong, IReadOnlyList<CharacterItemMsg>>? CarriedInventoryReceived
	{
		add => _idCoordinator.CarriedInventoryReceived += value;
		remove => _idCoordinator.CarriedInventoryReceived -= value;
	}

	public event Action<ulong>? ItemIdWatermarkReceived
	{
		add => _idCoordinator.ItemIdWatermarkReceived += value;
		remove => _idCoordinator.ItemIdWatermarkReceived -= value;
	}

	// ===== Carried-item facts =====

	public void SendItemCarriedSync(ulong ownerSteamId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || item.InstanceId == 0)
		{
			return;
		}

		if (item.Contents.Count > 0)
		{
			// The report carries a container's contents, and a child's PLACE is part
			// of that fact: the peers rebuild the carried fact from this batch's
			// kernel locations, so a bare state update would reach them as an empty
			// container with the moved child still top-level (TrySyncContainerFacts).
			_kernelAuthority.TrySyncContainerFacts(ownerSteamId, item, out _, out _);
		}
		else
		{
			var current = _kernelAuthority.FindItem(item.InstanceId);
			if (current is null)
			{
				_kernelAuthority.TrySpawnCarried(ownerSteamId, item.InstanceId, item.ItemId, item, out _, out _);
			}
			else
			{
				_kernelAuthority.TryUpdateState(ownerSteamId, item.InstanceId, item, out _, out _);
			}
		}

		PublishLocal(ownerSteamId, item);
	}

	public void FireItemCarriedSyncReceived(ulong sender, ulong ownerSteamId, CharacterItemMsg item, bool slotKnown)
		=> _carriedSync.FireItemCarriedSyncReceived(sender, ownerSteamId, item, slotKnown);

	public event Action<ulong, CharacterItemMsg, bool>? ItemCarriedSyncReceived
	{
		add => _carriedSync.ItemCarriedSyncReceived += value;
		remove => _carriedSync.ItemCarriedSyncReceived -= value;
	}

	internal void PublishLocal(ulong owner, CharacterItemMsg item) => _carriedSync.PublishLocal(owner, item);

	public void ResetOwnCarriedRegistration() => _ownCarried.Reset();
}
