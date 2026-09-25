using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Applies a RECEIVED block write to this world — the one place the adapter turns
/// the pure <see cref="RemoteBreakPresentation"/> route into the game's own
/// calls, so both receive paths (a relayed air write and an accepted break whose
/// cell still stands) present a break the same way.
///
/// A damage-driven break goes through <c>WorldGeneration.DamageBlock</c> rather
/// than a raw <c>SetBlock</c>: this side's block still stands, and that roll is
/// what plays the block's own hit and step sounds, spawns its break particles and
/// writes the air itself — the very presentation the side that computed the break
/// already had. The roll's damage is the locally derived lethal remainder, never
/// the received number (see <see cref="RemoteBreakPresentation.LethalDamage"/>),
/// and it runs under the caller's REMOTE-APPLY scope, so the local report hooks
/// stay silent and <c>ignoreLoot</c> keeps the drops to the break report that
/// carries them. Every other write stays a raw write: a placement, an
/// earthquake/environment break, a snapshot row and a correction never invent a
/// sound their source did not play.
///
/// A caller has usually read the cell already for its own first-writer guard; the
/// route re-reads it here so this applier is safe on its own. Both readings are of
/// the same frame's cell — a write is applied synchronously, so they cannot
/// disagree — and the re-read leaves the one-line rule as the single owner of the
/// decision.
///
/// The two OUTCOMES of a CLAIMED write are logged at Debug on purpose: "presented
/// here" and "found the cell already air" are exactly the two shapes a "I still
/// do not hear that break" report has to be told apart by. Unclaimed writes stay
/// unlogged — they are the high-frequency placements and environment writes.
/// </summary>
internal static class RemoteBlockWrite
{
	internal static void Apply(WorldGeneration world, Vector2Int cell, ushort block, bool playerBreak, ILogger log)
	{
		if (RemoteBreakPresentation.Route(playerBreak, block, world.GetBlock(cell) != 0) != RemoteBreakPresentation.Action.NativeBreak)
		{
			if (playerBreak)
			{
				log.LogDebug("[BlockBreak] a relayed break at ({X},{Y}) found the cell already air — written without a second presentation.", cell.x, cell.y);
			}

			world.SetBlock(cell, block);
			return;
		}

		var brokenBlock = world.GetBlock(cell);
		var health = world.GetBlockInfo(brokenBlock).health;
		var currentDamage = world.GetBlockDamage(cell)?.damage ?? 0f;
		log.LogDebug("[BlockBreak] presenting a relayed break at ({X},{Y}): the block goes through the game's own damage roll, so its hit/step sounds and its break particles play here.", cell.x, cell.y);
		world.DamageBlock(cell, RemoteBreakPresentation.LethalDamage(health, currentDamage), true, false, true);
	}
}
