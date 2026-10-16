using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The fate of the drops a MEMBER's break report carries (split from
/// BlockBreakSync — the 600-line gate; the report/relay plumbing and the
/// first-writer machine stayed there, the drops' judgement and both of its
/// refusal shapes live here).
/// <para>
/// Two shapes, and they answer differently on purpose:
/// </para>
/// <list type="bullet">
/// <item>a drop the destroyed block's own table cannot produce is refused ALONE
/// (<see cref="Bound"/>) — the rest of the set is registered and relayed
/// normally, because the bound exists to stop an item no local roll can create,
/// not to punish the breaker for a roll the table misreads;</item>
/// <item>a break that lost first-writer-wins is refused WHOLE
/// (<see cref="RefuseAll"/>) — none of its drops were ever this cell's.</item>
/// </list>
/// <para>
/// The bound's own evidence — why the host bounds a roll instead of re-deriving
/// it — is <see cref="BlockDropBoundPolicy"/>'s, and the tables it bounds
/// against are <see cref="GameBlockDropTable"/>'s: the game's own loot switch
/// for a vanilla id, a custom tile's authored drops for a CUO tile index.
/// </para>
/// </summary>
internal sealed class BlockDropBoundJudge(
	IItemControl items,
	GameAdapterTileContentProvider tileContent,
	ILogger<BlockDropBoundJudge> log)
{
	private readonly IItemControl _items = items;
	private readonly GameAdapterTileContentProvider _tileContent = tileContent;
	private readonly ILogger<BlockDropBoundJudge> _log = log;

	/// <summary>
	/// Host: bound a member's reported drops against what the block that break
	/// DESTROYED can produce, and hand back the drops the relay may carry.
	/// <para>
	/// The input is this host's own read of the cell: <paramref name="destroyedBlock"/>
	/// comes from <c>GetBlock</c> before the air write was applied (Fresh), from
	/// the running accepted record (Repeat) or from the cell that still stands
	/// (LostAirWrite) — never from the reporter.
	/// </para>
	/// <para>
	/// A refused drop is answered by name (the breaker forgets it and destroys its
	/// local copy) while the rest of the set keeps its normal fate. The rejects go
	/// out BEFORE the relay the caller sends next on purpose: the guest's pending
	/// break set must shrink first, or that relay's echo would not cover it and the
	/// break would be re-reported once more.
	/// </para>
	/// </summary>
	internal IReadOnlyList<BlockDropEntryMsg>? Bound(ulong sender, Vector2Int cell, ushort destroyedBlock, IReadOnlyList<BlockDropEntryMsg>? drops)
	{
		if (drops is not { Count: > 0 })
		{
			return drops;
		}

		var bounds = ResolveBounds(destroyedBlock, out var table);
		var outcome = BlockDropBoundPolicy.Judge(bounds, drops);
		if (!outcome.AnyRefused)
		{
			return drops;
		}

		foreach (var refused in outcome.Refused)
		{
			_items.SendItemReject(sender, refused.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
		}

		_log.LogWarning(
			"[BlockBreak] {Sender}'s break at ({X},{Y}) claimed {RefusedCount} drop(s) {Table} cannot produce — refused by name ({Refused}), {AcceptedCount} drop(s) accepted and relayed.",
			sender, cell.x, cell.y, outcome.Refused.Count, table, Describe(outcome.Refused), outcome.Accepted.Count);
		return outcome.Accepted;
	}

	/// <summary>
	/// Host: the break's drops are not this cell's — every one goes back to the
	/// breaker, which destroys its local copy (they were never picked up, so there
	/// is no ground position to roll back to). <paramref name="reason"/> is the
	/// caller's own verdict, named in the line so a first-writer loss and a
	/// report from another world generation read differently.
	/// </summary>
	internal void RefuseAll(ulong sender, Vector2Int cell, string reason, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (drops is not null)
		{
			foreach (var drop in drops)
			{
				_items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
			}
		}

		if (buildingDrops is not null)
		{
			foreach (var drop in buildingDrops)
			{
				_items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
			}
		}

		_log.LogInformation("[BlockBreak] {Sender}'s break report at ({X},{Y}) refused ({Reason}) — {BlockCount} block drop(s) + {BuildingCount} building drop(s) rejected.",
			sender, cell.x, cell.y, reason, drops?.Count ?? 0, buildingDrops?.Count ?? 0);
	}

	/// <summary>The destroyed block's own table: a CUO custom tile index has its authored drops, every other id the game's own loot switch (which yields no rows for a block that drops nothing).</summary>
	private IReadOnlyList<BlockDropBound> ResolveBounds(ushort destroyedBlock, out string table)
	{
		if (_tileContent.TryGetDefinitionByIndex(destroyedBlock, out var definition))
		{
			table = $"tile '{definition.Id}'";
			return GameBlockDropTable.ForTile(definition);
		}

		table = $"block {destroyedBlock}";
		return GameBlockDropTable.ForVanilla(destroyedBlock);
	}

	private static string Describe(IReadOnlyList<BlockDropEntryMsg> drops)
	{
		var parts = new List<string>(drops.Count);
		foreach (var drop in drops)
		{
			parts.Add(drop.Item is { } item ? $"{item.ItemId}@{item.Condition:F2}" : "a drop with no item state");
		}

		return string.Join(", ", parts);
	}
}
