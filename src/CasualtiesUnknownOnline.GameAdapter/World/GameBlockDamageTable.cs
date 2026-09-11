using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The game's OWN partial block damage table
/// (<c>WorldGeneration.world.blockDamages</c>): the live list the game breaks
/// blocks from and draws the crack sprite for. It is a SECOND table next to
/// CUO's <c>BlockDamageRegistry</c> — CUO's registry is the host's wire table
/// (what a late joiner receives, cap 256), this one is the gameplay table
/// (cap 128, <c>WorldGeneration.cs:732-737</c>) and it can hold damage CUO's
/// report hooks never observed (the direct <c>DamageBlock(Vector2Int)</c>
/// callers — footstep-crushing, the spider burrow — are not hooked).
///
/// Both live paths go through this one implementation: the host's world-entry
/// snapshot apply (<see cref="BlockBreakSync"/>) and the save restore
/// (<see cref="NativeWorldFacts"/>). Rows are applied ABSOLUTE per cell
/// (latest write wins) and validated against the cell's current block, because a
/// damage row for a cell that is air — or for a damage outside a surviving
/// block's range — would otherwise create a crack over nothing.
///
/// The crack-sprite refresh is the CALLER's (an engine call that creates a
/// GameObject; the table write itself is pure data), which is also what keeps
/// this helper verifiable without a running game.
/// </summary>
internal static class GameBlockDamageTable
{
	/// <summary>The game's own cap (WorldGeneration.cs:732-737). A restored set larger than this cannot be held by the game's list.</summary>
	internal const int Cap = 128;

	/// <summary>One apply's account: the entries the list holds now, and how many rows the validation or the cap refused.</summary>
	internal readonly record struct ApplyResult(IReadOnlyList<BlockDamage> Written, int Refused)
	{
		/// <summary>Rows this apply wrote (created or updated).</summary>
		internal int Applied => Written.Count;
	}

	/// <summary>What one row does against the state read from the live world.</summary>
	internal enum Verdict
	{
		/// <summary>The row lands: a new list entry, or an absolute update of the existing one.</summary>
		Apply,

		/// <summary>The cell is air — a broken block's partial damage is carried by the block-state rows, never by a damage row.</summary>
		RefuseAir,

		/// <summary>The damage is not inside a surviving block's range (non-positive, or at/over its health) — writing it would crack or break the block here.</summary>
		RefuseRange,

		/// <summary>The list is at the game's own cap and this cell is not in it yet.</summary>
		RefuseCap,
	}

	/// <summary>
	/// The per-row decision, pure: every input comes from the live world or the
	/// list, so the rules are verifiable without a running game (the world READS
	/// themselves are the adapter's boundary — <c>GetBlock</c> uses a
	/// netstandard-2.1 API the test host does not have).
	/// </summary>
	internal static Verdict Decide(float damage, ushort currentBlock, float blockHealth, bool alreadyTracked, int trackedCount)
	{
		if (currentBlock == 0)
		{
			return Verdict.RefuseAir;
		}

		if (damage <= 0f || damage >= blockHealth)
		{
			return Verdict.RefuseRange;
		}

		return alreadyTracked || trackedCount < Cap ? Verdict.Apply : Verdict.RefuseCap;
	}

	/// <summary>Every entry of the game's own list in the wire shape the save layer and the snapshots use.</summary>
	internal static List<BlockDamageEntryMsg> Capture(WorldGeneration world)
	{
		var entries = new List<BlockDamageEntryMsg>(world.blockDamages.Count);
		foreach (var damage in world.blockDamages)
		{
			entries.Add(new BlockDamageEntryMsg { X = damage.pos.x, Y = damage.pos.y, Damage = damage.damage });
		}

		return entries;
	}

	/// <summary>
	/// Apply damage rows to the game's list, absolute per cell. Returns every
	/// entry the call wrote so the caller can refresh its crack sprite, plus the
	/// rows that were refused and why they were logged.
	/// </summary>
	internal static ApplyResult Apply(
		WorldGeneration world,
		IReadOnlyList<BlockDamageEntryMsg> entries,
		string origin,
		ILogger log)
	{
		var written = new List<BlockDamage>(entries.Count);
		var refused = 0;
		foreach (var entry in entries)
		{
			var cell = new Vector2Int(entry.X, entry.Y);
			var block = world.GetBlock(cell);
			var existing = block == 0 ? null : world.GetBlockDamage(cell);
			var blockHealth = block == 0 ? 0f : world.GetBlockInfo(block).health;

			var verdict = Decide(entry.Damage, block, blockHealth, existing is not null, world.blockDamages.Count);
			if (verdict != Verdict.Apply)
			{
				refused++;
				LogRefusal(log, origin, verdict, entry, cell, blockHealth);
				continue;
			}

			if (existing is null)
			{
				existing = new BlockDamage { pos = cell, damage = entry.Damage };
				world.blockDamages.Add(existing);
			}
			else
			{
				existing.damage = entry.Damage;
			}

			written.Add(existing);
		}

		return new ApplyResult(written, refused);
	}

	private static void LogRefusal(ILogger log, string origin, Verdict verdict, BlockDamageEntryMsg entry, Vector2Int cell, float blockHealth)
	{
		switch (verdict)
		{
			case Verdict.RefuseAir:
				// Expected, not an anomaly: a broken cell's partial damage is owned
				// by the block-state rows, and the game's list never carried one for
				// air. Debug keeps it observable without turning a 60 s snapshot into
				// per-row warning spam (the old inline loop skipped air silently).
				log.LogDebug("{Origin}: partial damage at ({X},{Y}) is not applied — the cell is air (the block-state rows own broken cells).",
					origin, cell.x, cell.y);
				return;
			case Verdict.RefuseRange:
				log.LogWarning("{Origin}: partial damage {Damage} at ({X},{Y}) is outside a surviving block's range ({Health} hp) — not applied.",
					origin, entry.Damage, cell.x, cell.y, blockHealth);
				return;
			default:
				log.LogWarning("{Origin}: partial damage at ({X},{Y}) is not applied — the game's {Cap}-entry blockDamages list is full.",
					origin, cell.x, cell.y, Cap);
				return;
		}
	}
}
