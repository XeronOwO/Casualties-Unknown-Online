using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Puts ONE RESTORED CUT's world facts back: the Runtime half (the block diff, the
/// partial damage, the radiation line) through <see cref="IWorldFactSource"/>, the
/// native half (keypad codes, geyser liquid types, the game's own partial-damage
/// list) through <see cref="INativeWorldFacts"/>.
///
/// Split out of <see cref="WorldSaveService"/> — which owns WHICH world a run
/// writes into and WHEN a cut is taken — because the restore's per-row routing is
/// its own responsibility: each row kind goes back to the table it came from, and
/// everything that could not be put back is returned as damage for the restore
/// report (§6: a keypad left to re-roll is a value the player can see, never a
/// quiet default).
/// </summary>
internal sealed class WorldFactRestore(
	IWorldFactSource worldFacts,
	INativeWorldFacts? nativeWorldFacts,
	ILogger<WorldFactRestore> log)
{
	/// <summary>
	/// Puts one restored cut's world facts back. The Runtime half (the block diff,
	/// the partial damage and the radiation line) is applied absolutely through
	/// <see cref="IWorldFactSource"/>, which resets first — the cut is the whole
	/// truth for those tables. The native half (keypad codes, geyser liquid types
	/// and the game's own partial-damage list) is handed to
	/// <see cref="INativeWorldFacts"/>. Everything that could NOT be put back is
	/// RETURNED as damage for the restore report, because a keypad left to re-roll
	/// is a value the player can see (§6: never a quiet default).
	/// </summary>
	internal List<string> Apply(
		IReadOnlyList<SaveWorldBlockRow> blocks,
		IReadOnlyList<SaveWorldTransientRow> transients)
	{
		var damage = new List<string>();
		var blockStates = new List<BlockStateEntryMsg>();
		var blockDamages = new List<BlockDamageEntryMsg>();
		var nativeDamages = new List<BlockDamageEntryMsg>();
		var radiationLine = (RadiationLineStateMsg?)null;
		var keypads = new List<KeypadEntryMsg>();
		var geysers = new List<GeyserStateEntryMsg>();

		foreach (var row in blocks)
		{
			switch (row.Kind)
			{
				case SaveWorldBlockRow.BlockStateKind when row.BlockState is not null:
					// A row out of the archive is ALREADY settled: the air transition
					// it records happened in the saved world, and the building it
					// carried either died there or was rebuilt on top. Marking it here
					// keeps a later guest's snapshot apply from re-killing a building
					// the authority still holds.
					blockStates.Add(new BlockStateEntryMsg
					{
						X = row.BlockState.X,
						Y = row.BlockState.Y,
						Block = row.BlockState.Block,
						SupportLossSettled = true,
					});
					break;
				case SaveWorldBlockRow.BlockDamageKind when row.BlockDamage is not null:
					blockDamages.Add(row.BlockDamage);
					break;
				case SaveWorldBlockRow.NativeBlockDamageKind when row.NativeBlockDamage is not null:
					nativeDamages.Add(row.NativeBlockDamage);
					break;
				default:
					damage.Add($"an unreadable {row.Describe()} row was not applied");
					log.LogWarning("Restored world-block row {Row} carries no applicable payload; it is not applied.", row.Describe());
					break;
			}
		}

		foreach (var row in transients)
		{
			switch (row.Kind)
			{
				case SaveWorldTransientRow.RadiationLineKind when row.RadiationLine is not null:
					radiationLine = row.RadiationLine;
					break;
				case SaveWorldTransientRow.KeypadKind when row.Keypad is not null:
					keypads.Add(row.Keypad);
					break;
				case SaveWorldTransientRow.GeyserKind when row.Geyser is not null:
					geysers.Add(row.Geyser);
					break;
				default:
					damage.Add($"an unreadable {row.Describe()} row was not applied");
					log.LogWarning("Restored world-transient row {Row} carries no applicable payload; it is not applied.", row.Describe());
					break;
			}
		}

		var report = worldFacts.ApplyFacts(blockStates, blockDamages, radiationLine);
		if (report.Describe() is { } refused)
		{
			// The bounded tables refused rows: a restore that reported success
			// while a row was dropped is exactly what §6 forbids.
			damage.Add(refused);
		}

		var nativeCount = nativeDamages.Count;
		if (keypads.Count + geysers.Count + nativeCount == 0)
		{
			return damage;
		}

		if (nativeWorldFacts is null)
		{
			damage.Add($"{keypads.Count} keypad code(s), {geysers.Count} geyser liquid type(s) and {nativeCount} game block-damage row(s) were not restored (no native applier in this build)");
			log.LogError(
				"Restored world WITHOUT {Keypads} keypad code(s), {Geysers} geyser liquid type(s) and {Damages} game block-damage row(s): this build has no INativeWorldFacts, so those native facts cannot be put back.",
				keypads.Count, geysers.Count, nativeCount);
			return damage;
		}

		// The adapter owns these tables: the Continue click runs before the world
		// object exists, so the restore hands the values over rather than writing
		// them from here. The adapter writes them into the live world at its
		// world-entry seam (§4), which is also where a row its own tables refuse is
		// logged.
		try
		{
			nativeWorldFacts.ApplyKeypadCodes(keypads);
			nativeWorldFacts.ApplyGeysers(geysers);
			nativeWorldFacts.ApplyBlockDamages(nativeDamages);
			log.LogInformation(
				"Handed {Keypads} keypad code(s), {Geysers} geyser liquid type(s) and {Damages} game block-damage row(s) to the native world-fact applier.",
				keypads.Count, geysers.Count, nativeCount);
		}
		catch (Exception ex)
		{
			// The adapter is the only layer that can fail here (it touches the live
			// game). A throw must not abort the restore after the kernel and the
			// Runtime facts already applied: it is damage, and it is reported.
			damage.Add($"{keypads.Count} keypad code(s), {geysers.Count} geyser liquid type(s) and {nativeCount} game block-damage row(s) could not be applied by the native world-fact applier");
			log.LogError(ex, "The native world-fact applier failed; those native facts are not restored and the layer keeps freshly-generated values.");
		}

		return damage;
	}

}
