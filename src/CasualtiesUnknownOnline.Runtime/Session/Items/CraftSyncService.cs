using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The crafting domain (one operation = one report, end-to-end): a crafting
/// operation's complete terminal state arrives as ONE CraftReportMsg — the
/// consumed/changed materials and the products. The host classifies each entry
/// against its tables (CraftReportJudge), applies per verdict, stamps the
/// relay routing (OwnerSteamId + per-entry ApplyKind — the guests' tables are
/// empty, so they apply positionally) and relays the WHOLE report (source
/// excluded — never decomposed into per-entry broadcasts). Accept-with-adopt,
/// never reject: the sender's consumption is irreversible, so untracked
/// entries are skipped with a warning (anti-cheat out of scope). Event-driven,
/// no pump — not an ICuoService (the ItemService/WorldService precedent).
/// </summary>
public sealed class CraftSyncService(
	ISessionControl session, PacketSender sender, ItemService items, ItemArbitration arbitration,
	ILogger<CraftSyncService> log) : ICraftControl
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ItemService _items = items;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly ILogger<CraftSyncService> _log = log;

	/// <summary>A recipe was unlocked (every side) — the adapter sets Recipes.recipes[idx].INT = 0.</summary>
	public event Action<int>? RecipeUnlockReceived;

	public void ReportCraft(CraftReportMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			FireCraftReportReceived(_session.LocalSteamId, msg); // applies the world entries + relays
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.CraftReport, msg);
		}
	}

	public void FireCraftReportReceived(ulong sender, CraftReportMsg msg)
	{
		if (_session.Role == SessionRole.Guest)
		{
			ApplyRelayed(msg);
			return;
		}

		var ownReport = sender == _session.LocalSteamId;
		if (ownReport)
		{
			// The host's own craft: its scene is the fact — only the WORLD-table
			// entries need aligning (the scene consumed/drained them; the table
			// would otherwise keep a ghost entry). Carried items and products
			// need nothing (the scene IS the record). The relay carries the
			// report to the guests, who apply positionally.
			foreach (var entry in msg.Entries)
			{
				var id = entry.Item.InstanceId;
				if (id == 0 || !_items.IsWorldItemRegistered(id))
				{
					continue; // carried — the host's own scene is the fact
				}

				if (entry.Disposition == CraftEntryDisposition.Destroyed)
				{
					_items.RemoveWorldItemLocal(id);
				}
				else
				{
					_items.UpdateWorldItemState(id, entry.Item);
					entry.ApplyKind = CraftApplyKind.WorldCorrection; // the guests' copies adopt through the relay
				}
			}
		}
		else
		{
			ApplyGuestReport(sender, msg);
		}

		msg.OwnerSteamId = sender; // the transport sender is the trusted fact — stamped for the relay's receivers
		if (ownReport)
		{
			_session.Broadcast(NetMsg.CraftReport, msg);
		}
		else
		{
			_session.BroadcastExcept(sender, NetMsg.CraftReport, msg);
		}
	}

	private void ApplyGuestReport(ulong sender, CraftReportMsg msg)
	{
		var entriesById = msg.Entries
			.Where(e => e.Item.InstanceId != 0)
			.GroupBy(e => e.Item.InstanceId)
			.ToDictionary(g => g.Key, g => g.First());
		var worldIds = entriesById.Keys.Where(_items.IsWorldItemRegistered).ToHashSet();
		var transferredIds = entriesById.Keys.Where(id => _arbitration.IsTransferredToGuest(sender, id)).ToHashSet();

		foreach (var (id, verdict) in CraftReportJudge.Classify(msg, worldIds, transferredIds))
		{
			var entry = entriesById[id];
			switch (verdict)
			{
				case CraftVerdict.WorldDestroy:
					_items.RemoveWorldItemLocal(id);
					break;
				case CraftVerdict.TransferredRemove:
					_arbitration.RemoveTransferred(sender, id);
					break;
				case CraftVerdict.UnknownSkip:
					// Never rejected (the consumption is irreversible on the
					// sender): a race with another guest's pickup, or an item
					// that never entered the tables. Skip + warn.
					_log.LogWarning("[Crafting] entry {ItemId} of {Sender} is untracked — skipped.", id, sender);
					break;
				case CraftVerdict.WorldChange:
					_items.UpdateWorldItemState(id, entry.Item);
					_items.FireCorrectionLocal(entry.Item);
					entry.ApplyKind = CraftApplyKind.WorldCorrection;
					break;
				case CraftVerdict.AdoptChange:
					// The sender is the fact source for its own inventory (the
					// use-path philosophy — a craft changes the item's state by
					// definition). Untracked (the carried-inventory report in
					// flight or lost) — the report IS the fact: register it.
					if (_arbitration.AdoptEvidence(sender, id, entry.Item, "crafted") == null)
					{
						_arbitration.RegisterCarried(sender, [entry.Item]);
					}

					break;
			}
		}

		_arbitration.RegisterCarried(sender, msg.Products);
		foreach (var product in msg.Products)
		{
			// This host's clone fact table of the crafter re-renders — the
			// relay's receivers do the same locally (no broadcast here: the
			// relay already carries the products, one operation = one message).
			_items.PublishCarriedSyncLocal(sender, product);
		}

		_log.LogInformation("[Crafting] {Kind} of {Sender}: {Entries} entries, {Products} products applied.",
			msg.Kind, sender, msg.Entries.Count, msg.Products.Count);
	}

	/// <summary>Guest side: the host's relay applies positionally — the guests' tables are empty, so the routing rides the host's stamps.</summary>
	private void ApplyRelayed(CraftReportMsg msg)
	{
		foreach (var entry in msg.Entries)
		{
			var id = entry.Item.InstanceId;
			if (id == 0)
			{
				continue;
			}

			if (entry.Disposition == CraftEntryDisposition.Destroyed)
			{
				// Scene-query removal — a no-op when the item was carried (only
				// the crafter ever had it; this side's clone fact table heals
				// via the 1 Hz character snapshot).
				_items.RemoveWorldItemLocal(id);
			}
			else if (entry.ApplyKind == CraftApplyKind.WorldCorrection)
			{
				_items.FireCorrectionLocal(entry.Item);
			}
		}

		foreach (var product in msg.Products)
		{
			_items.PublishCarriedSyncLocal(msg.OwnerSteamId, product); // the crafter's clone gains the product
		}
	}

	public void SendRecipeUnlock(int recipeIndex)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			FireRecipeUnlockReceived(_session.LocalSteamId, recipeIndex);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = recipeIndex });
		}
	}

	public void FireRecipeUnlockReceived(ulong sender, int recipeIndex)
	{
		RecipeUnlockReceived?.Invoke(recipeIndex); // every side applies its own static
		if (_session.Role == SessionRole.Host)
		{
			if (sender == _session.LocalSteamId)
			{
				_session.Broadcast(NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = recipeIndex });
			}
			else
			{
				_session.BroadcastExcept(sender, NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = recipeIndex });
			}
		}
	}
}
