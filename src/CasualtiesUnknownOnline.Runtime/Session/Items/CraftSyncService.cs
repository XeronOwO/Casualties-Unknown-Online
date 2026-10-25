using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
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
/// entries are skipped with a warning (anti-cheat out of scope). A Destroyed
/// entry IS the destruction event for a carried row: the apply retires that
/// owner's clone fact-table row (host and relay receiver alike, and the host's
/// own craft retires its kernel row); a destroyed world row is stamped
/// WorldDestroy for the relay's scene removal. Event-driven, no pump.
///
/// The recipe-unlock SET is this domain's absolute fact (sync-coverage audit
/// I6). The unlock is irreversible on the side that spent the blueprint and the
/// recipe table is a per-process static, so a swallowed report/relay or a member
/// that joined later left a crafting list short with nothing to heal it. The set
/// is read from the GAME's own table through <see cref="INativeWorldFacts"/> at
/// send time — never mirrored into a table of our own, which is what would
/// drift — and both halves use the same read: the host sends it on the
/// world-entry and 60 s repair groups, a guest reports its own on the shared
/// fallback cadence until the host's set carries it, and the host merges a
/// guest's set through the ordinary unlock path (apply + relay), so the host
/// stays the authority and there is still exactly one apply path.
/// </summary>
public sealed class CraftSyncService(
	ISessionControl session, PacketSender sender, ItemService items, ItemKernelAuthority kernelAuthority, ItemArbitration arbitration,
	ILogger<CraftSyncService> log, INativeWorldFacts? nativeWorldFacts = null) : ICraftControl, IDisposable
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ItemService _items = items;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly INativeWorldFacts? _nativeWorldFacts = nativeWorldFacts;
	private readonly ILogger<CraftSyncService> _log = log;

	/// <summary>The destroyed rows' attribution and the host's own-row retirement (the rule lives in one place).</summary>
	private readonly CraftCarriedRowRetirement _carriedRows = new(session, kernelAuthority, log);

	/// <summary>The set re-report's cadence: the shared pending window (the 5 s entry phase after this guest's own world entry, the steady 60 s after it; armed by the first outstanding unlock).</summary>
	private readonly PendingReportFallback _recipeFallback = new(session);

	/// <summary>How many fallback windows one recipe's re-report may spend before this side gives up on it and NAMES the divergence. The FIRST window is the swallowed-send heal, so the budget only runs out for an index this host's own table cannot hold — where the host's set can never carry it, and an unbounded retry would be a permanent drip.</summary>
	private const int MaxRecipeSetReports = 3;

	/// <summary>
	/// The unconfirmed unlocks, keyed by recipe index: an index this side reported
	/// whose carrying by the host's absolute set has not been observed yet, and how
	/// many fallback windows have been spent on it. The fallback's work check is
	/// this table's count and its bound is the per-index budget — a removed index
	/// was either confirmed by the host's set or dropped by name.
	/// </summary>
	private readonly Dictionary<int, int> _uncarriedRecipes = [];

	/// <summary>A recipe was unlocked (every side) — the adapter sets Recipes.recipes[idx].INT = 0.</summary>
	public event Action<int>? RecipeUnlockReceived;

	/// <summary>An absolute unlock set arrived (guest side only) — the adapter writes INT = 0 for every index without the per-recipe alert.</summary>
	public event Action<IReadOnlyList<int>>? RecipeUnlockSetReceived;

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
			// The host's own craft: its scene is the fact for the ITEMS — only the
			// WORLD-table entries need aligning there (the scene consumed/drained
			// them; the table would otherwise keep a ghost). A destroyed CARRIED row
			// is the exception: the in-craft destroy claim kept the ordinary destroy
			// report off its kernel row, so the report is the one event that can
			// retire it; the relay's receivers retire the host's clone fact row.
			foreach (var entry in msg.Entries)
			{
				var id = entry.Item.InstanceId;
				if (id == 0)
				{
					continue; // unbound — no table or row can hold it
				}

				if (!_items.IsWorldItemRegistered(id))
				{
					if (entry.Disposition == CraftEntryDisposition.Destroyed)
					{
						_carriedRows.RetireOwnRow(id);
						entry.ApplyKind = CraftApplyKind.CarriedRetire; // the guests retire the HOST's clone fact row through the stamped route
					}

					continue; // carried — the scene owns the object, nothing else to align here
				}

				if (entry.Disposition == CraftEntryDisposition.Destroyed)
				{
					_items.RemoveWorldItemLocal(id);
					entry.ApplyKind = CraftApplyKind.WorldDestroy; // the guests' copies leave their scene through the stamped route
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
		// Asked of the authority: the transfer cache is rebuilt from these rows, so the answer is the same one, from the one source
		// (ticket item-custody-single-authority, stage 4 - the cache losing its identity role).
		var transferredIds = entriesById.Keys.Where(id => _items.OwnsCarriedItem(sender, id)).ToHashSet();

		foreach (var (id, verdict) in CraftReportJudge.Classify(msg, worldIds, transferredIds))
		{
			var entry = entriesById[id];
			// The report IS the destruction event for a carried row — but the retirement must be ANCHORED:
			// the transfer table says the sender owns it, or (the registration is the lost half) this host's
			// kernel row still says the sender carries it. Anything else is skipped without touching a fact
			// row: the sender's report may not name another member's item, and a retirement is addressed by
			// owner, so it must never be fired on a guess.
			var attributed = verdict == CraftVerdict.TransferredRemove
				|| (verdict == CraftVerdict.UnknownSkip && _carriedRows.IsOwnedBy(id, sender));
			if (attributed)
			{
				_items.RetireCarriedLocal(sender, id);
				entry.ApplyKind = CraftApplyKind.CarriedRetire; // the relay's receivers retire the sender's row, never a scene copy
			}

			switch (verdict)
			{
				case CraftVerdict.WorldDestroy:
					_items.RemoveWorldItemLocal(id);
					_kernelAuthority.TryDestroy(sender, id, TerminalKind.Destroyed, out _, out _);
					entry.ApplyKind = CraftApplyKind.WorldDestroy; // the guests' copies leave their scene through the stamped route, never the fact table's
					break;
				case CraftVerdict.TransferredRemove:
					_arbitration.RemoveTransferred(sender, id);
					_kernelAuthority.TryDestroy(sender, id, TerminalKind.Destroyed, out _, out _);
					break;
				case CraftVerdict.UnknownSkip:
					// Never rejected (the consumption is irreversible on the
					// sender): a race with another guest's pickup, or an item
					// that never entered the tables. A destroy the authority
					// row still attributes to the sender is retired above and
					// named here; everything else is skipped and warned.
					if (attributed)
					{
						_log.LogInformation("[Crafting] entry {ItemId} of {Sender} is absent from the transfer table but the authority row says the sender carries it — the carried fact is retired.",
							id, sender);
					}
					else
					{
						_log.LogWarning("[Crafting] entry {ItemId} of {Sender} is untracked — skipped.", id, sender);
					}

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
			_kernelAuthority.TrySpawnCarried(sender, product.InstanceId, product.ItemId, product, out _, out _);
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
		var worldDestroys = 0;
		var carriedRetires = 0;
		var skipped = 0;
		foreach (var entry in msg.Entries)
		{
			var id = entry.Item.InstanceId;
			if (id == 0)
			{
				continue;
			}

			if (entry.Disposition == CraftEntryDisposition.Destroyed)
			{
				switch (entry.ApplyKind)
				{
					case CraftApplyKind.WorldDestroy:
						_items.RemoveWorldItemLocal(id); // stamped by the host: a world row — its scene copy goes
						worldDestroys++;
						break;
					case CraftApplyKind.CarriedRetire:
						// Stamped by the host with the owner it attributed the id to: retire THAT owner's clone
						// fact-table row, never a scene copy and never anybody else's row.
						_items.RetireCarriedLocal(msg.OwnerSteamId, id);
						carriedRetires++;
						break;
					default:
						// Unstamped: the host found no record that the id was the sender's — the receivers have
						// no tables to classify with either, so they act on nothing.
						skipped++;
						break;
				}
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

		_log.LogInformation("[Crafting] {Kind} relay of {Sender}: {WorldDestroys} world destroy(s), {CarriedRetires} carried retirement(s), {Skipped} skipped entry/entries, {Products} product(s) applied.",
			msg.Kind, msg.OwnerSteamId, worldDestroys, carriedRetires, skipped, msg.Products.Count);
	}

	public void SendRecipeUnlock(int recipeIndex)
	{
		if (_session.Role == SessionRole.Guest)
		{
			// The unlock is irreversible on THIS side (the blueprint is spent) and
			// the live report below is one-shot, so the index is armed for the
			// fallback cadence BEFORE the session check: an unlock made while the
			// session is still coming up must still go out with the set once there
			// is a session to report into (the pump fires only while one is
			// active). A fresh arm starts that index's window budget over.
			_uncarriedRecipes[recipeIndex] = 0;
		}

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

	public void FireRecipeUnlockReceived(ulong sender, int recipeIndex) => TryApplyReportedUnlock(sender, recipeIndex);

	/// <summary>
	/// The one apply path for an unlock: judge the report, apply it here (every side sets its own
	/// static) and, on the host, relay it. Returns whether it was APPLIED — a refused index is not,
	/// which is the count the set merge reports and the reason the two callers read one verdict
	/// instead of counting a refusal as learned.
	/// </summary>
	private bool TryApplyReportedUnlock(ulong sender, int recipeIndex)
	{
		if (!MayApplyReportedUnlock(sender, recipeIndex))
		{
			return false; // refused by name in the log: nothing applied here, nothing relayed
		}

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

		return true;
	}

	/// <summary>
	/// Host: may this reported index be applied at all? The unlock is irreversible and
	/// every side writes <c>Recipes.recipes[index].INT = 0</c> onto its OWN table, so an
	/// index this host's table does not have is a latch no side can serve — the census
	/// row's gap was that the live report path compared it with nothing (the snapshot
	/// merge at least unioned against the host's known set). The anchor is the HOST's
	/// own recipe table, entry by entry, which is the very table every other side is
	/// then asked to write.
	///
	/// The host's own unlock and every guest-side apply are not this rule's business.
	/// A read that FAILED (no live world yet, or no reader registered in this
	/// composition) is NOT a refusal: the report applies as before and the reason is
	/// named, because a build without a native-fact reader would otherwise lose every
	/// member's unlock.
	/// </summary>
	private bool MayApplyReportedUnlock(ulong sender, int recipeIndex)
	{
		if (_session.Role != SessionRole.Host || sender == _session.LocalSteamId)
		{
			return true;
		}

		var fields = _nativeWorldFacts?.CaptureRunFields();
		if (fields is null || fields.Value.Failure is not null)
		{
			_log.LogWarning("[Crafting] {Sender} reported recipe {Index} unlocked, but this host has no live recipe table to judge it against ({Reason}) — the report is applied as before.",
				sender, recipeIndex, fields?.Failure ?? "no native-fact reader is registered");
			return true;
		}

		foreach (var row in fields.Value.Recipes)
		{
			if (row.Index == recipeIndex)
			{
				return true;
			}
		}

		_log.LogWarning("[Crafting] refused {Sender}'s recipe {Index}: this host's table has no such recipe ({Count} entries) — nothing applied, nothing relayed.",
			sender, recipeIndex, fields.Value.Recipes.Count);
		return false;
	}

	// ===== The absolute unlock set (I6): the host's backfill and the guest's re-report =====
	public void SendRecipeUnlockSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || targetSteamId == 0)
		{
			return;
		}

		var unlocked = CaptureUnlockedRecipes();
		if (unlocked is null)
		{
			_log.LogWarning("[Crafting] no live recipe table to read this host's unlocked set from — {Peer} receives no recipe-unlock backfill.", targetSteamId);
			return;
		}

		if (unlocked.Count == 0)
		{
			// The set is unlock-only and monotonic: an empty one asks for no
			// write, so it is not worth a frame.
			return;
		}

		_sender.Send(targetSteamId, NetMsg.RecipeUnlockSnapshot, new RecipeUnlockSnapshotMsg { RecipeIndexes = [.. unlocked] });
		_log.LogInformation("[Crafting] sent {Count} unlocked recipe(s) to {Peer}.", unlocked.Count, targetSteamId);
	}

	public void SendRecipeUnlockSetToHost()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var unlocked = CaptureUnlockedRecipes();
		if (unlocked is null || unlocked.Count == 0)
		{
			// No live table to read, or nothing unlocked: nothing to report. A
			// table that cannot be read is NOT an empty one, so the unconfirmed
			// index survives to the next cycle — and says why it could not go out.
			_log.LogDebug("[Crafting] {Count} unconfirmed recipe unlock(s) could not be re-reported: the live recipe table is {State}.",
				_uncarriedRecipes.Count, unlocked is null ? "not readable" : "empty");
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.RecipeUnlockSnapshot, new RecipeUnlockSnapshotMsg { RecipeIndexes = [.. unlocked] });
		_log.LogInformation("[Crafting] re-reported this guest's {Count} unlocked recipe(s) to the host.", unlocked.Count);
	}

	public void PumpRecipeUnlockFallback(long nowMs) =>
		_recipeFallback.Pump(nowMs, _uncarriedRecipes.Count, ResendOwnRecipeUnlockSet);

	/// <summary>
	/// One fallback window elapsed with at least one unlock still unconfirmed:
	/// charge the window to every unconfirmed index, drop (and NAME) the ones whose
	/// budget is spent, then re-report this side's absolute set. Dropping is what
	/// makes the fallback BOUNDED: an index this host's table does not have can
	/// never come back in its set, so retrying it forever would be one small message
	/// every minute for the rest of the session with nothing to converge on. The
	/// first window is what heals a swallowed send, so what is dropped here is the
	/// residue the host's own table refused, not a lost report.
	/// </summary>
	private void ResendOwnRecipeUnlockSet()
	{
		foreach (var index in _uncarriedRecipes.Keys.ToArray())
		{
			var spent = _uncarriedRecipes[index] + 1;
			if (spent > MaxRecipeSetReports)
			{
				_uncarriedRecipes.Remove(index);
				_log.LogWarning("[Crafting] recipe {Index} is still not carried by the host's set after {Attempts} re-report(s) — the re-report stops for it (this host's set never carried it, so the windows spent on it cannot converge).",
					index, MaxRecipeSetReports);
				continue;
			}

			_uncarriedRecipes[index] = spent;
		}

		if (_uncarriedRecipes.Count == 0)
		{
			_recipeFallback.Reset();
			return;
		}

		SendRecipeUnlockSetToHost();
	}

	public void FireRecipeUnlockSnapshotReceived(ulong sender, IReadOnlyList<int> recipeIndexes)
	{
		if (_session.Role == SessionRole.Guest)
		{
			ApplyUnlockSet(sender, recipeIndexes);
			return;
		}

		MergeGuestUnlockSet(sender, recipeIndexes);
	}

	/// <summary>
	/// Guest: the host's authoritative set applies silently and idempotently
	/// (the adapter writes INT = 0 and shows no alert — the receiver performed no
	/// unlock), and carrying this side's own set is the answer that ends the
	/// re-report.
	/// </summary>
	private void ApplyUnlockSet(ulong sender, IReadOnlyList<int> recipeIndexes)
	{
		if (recipeIndexes.Count == 0)
		{
			return;
		}

		RecipeUnlockSetReceived?.Invoke([.. recipeIndexes]);
		_log.LogInformation("[Crafting] applied {Count} unlocked recipe(s) from {Sender}'s set.", recipeIndexes.Count, sender);
		ConfirmOwnRecipeUnlockSet(recipeIndexes);
	}

	/// <summary>
	/// The host's set arrived: every index it carries is CONFIRMED and leaves the
	/// unconfirmed table, so the re-report stops once the last of them is answered.
	/// An index the set does not carry is left to the budget in
	/// <see cref="ResendOwnRecipeUnlockSet"/> — never cleared against a set that
	/// merely did not mention it — and an unlock made while the previous report was
	/// in flight was armed after it, so it keeps the fallback alive.
	/// </summary>
	private void ConfirmOwnRecipeUnlockSet(IReadOnlyList<int> hostSet)
	{
		if (_uncarriedRecipes.Count == 0)
		{
			return;
		}

		var carried = new HashSet<int>(hostSet);
		var confirmed = 0;
		foreach (var index in _uncarriedRecipes.Keys.ToArray())
		{
			if (carried.Contains(index) && _uncarriedRecipes.Remove(index))
			{
				confirmed++;
			}
		}

		if (confirmed == 0)
		{
			return;
		}

		if (_uncarriedRecipes.Count == 0)
		{
			_recipeFallback.Reset();
		}

		_log.LogDebug("[Crafting] the host's set carries {Confirmed} of this guest's reported unlock(s); {Remaining} still unconfirmed.",
			confirmed, _uncarriedRecipes.Count);
	}

	/// <summary>
	/// Host: a guest's ABSOLUTE set. Every index this host's live table does not
	/// already hold goes through the ORDINARY unlock path — apply locally (the
	/// adapter's write, with the alert only on a real learn) and relay to the
	/// other members, source excluded — so the merge adds no second apply path, a
	/// repeated set costs one comparison, and an index whose recipe this host does
	/// not have is refused BY NAME before the apply the live report uses, so
	/// nothing is applied and nothing is relayed for it. A set that cannot be compared (no live table) is NOT
	/// applied blindly: the reporter's entry stays pending on its side and comes
	/// back on the next cycle.
	/// </summary>
	private void MergeGuestUnlockSet(ulong sender, IReadOnlyList<int> recipeIndexes)
	{
		if (recipeIndexes.Count == 0)
		{
			return;
		}

		var held = CaptureUnlockedRecipes();
		if (held is null)
		{
			_log.LogWarning("[Crafting] {Sender} reported {Count} unlocked recipe(s) but this host has no live recipe table — the set is not judged.", sender, recipeIndexes.Count);
			return;
		}

		var known = new HashSet<int>(held);
		var learned = 0;
		foreach (var index in recipeIndexes)
		{
			if (!known.Add(index))
			{
				continue; // this host already holds it — the merge is a union, never a re-lock
			}

			if (TryApplyReportedUnlock(sender, index))
			{
				learned++;
			}
		}

		_log.LogInformation("[Crafting] {Sender}'s unlock set: {Total} reported, {Learned} this host had not learned.", sender, recipeIndexes.Count, learned);
	}

	/// <summary>This composition's live recipe table, or null when no reader is registered / no live table exists — never an empty set standing in for "could not read".</summary>
	private IReadOnlyList<int>? CaptureUnlockedRecipes() => _nativeWorldFacts?.CaptureUnlockedRecipeIndexes();

	/// <inheritdoc />
	public void Dispose() => _recipeFallback.Unbind();
}
