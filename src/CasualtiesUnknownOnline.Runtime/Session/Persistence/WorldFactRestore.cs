using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Puts ONE RESTORED CUT's world facts back: the Runtime half (the block diff and
/// the radiation line) through <see cref="IWorldFactSource"/>, the native half
/// (keypad codes, geyser liquid types and the partial block damage, which has no
/// Runtime table) through <see cref="INativeWorldFacts"/>.
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
	/// Puts one restored cut's world facts back. The Runtime half (the block diff
	/// and the radiation line) is applied absolutely through
	/// <see cref="IWorldFactSource"/>, which resets first — the cut is the whole
	/// truth for those tables. The native half (keypad codes, geyser liquid types
	/// and the partial block damage) is handed to
	/// <see cref="INativeWorldFacts"/>. Everything the RUNTIME half could NOT put
	/// back is RETURNED as damage for the restore report, because a keypad left to
	/// re-roll is a value the player can see (§6: never a quiet default). The native
	/// half's own refusals land later, at the world-entry replay, and reach the log
	/// today rather than the outcome — the restore-report gap tracked in
	/// `todo/save-mid-run-consistent-cut.md` (scope 6).
	///
	/// <paramref name="restoreSequence"/> is the restore attempt these values belong
	/// to (the kernel restore that produced them): the Runtime arm stamps it, and the
	/// world-entry contribution carries it, so the account opened for a LATER restore
	/// cannot count this attempt's write as its own.
	/// </summary>
	internal List<string> Apply(
		ulong restoreSequence,
		IReadOnlyList<SaveWorldBlockRow> blocks,
		IReadOnlyList<SaveWorldTransientRow> transients,
		SaveNativeRunFields? nativeRunFields = null)
	{
		var damage = new List<string>();
		var blockStates = new List<BlockStateEntryMsg>();
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

		var report = worldFacts.ApplyFacts(blockStates, radiationLine, restoreSequence);
		if (report.Describe() is { } refused)
		{
			// The bounded tables refused rows: a restore that reported success
			// while a row was dropped is exactly what §6 forbids.
			damage.Add(refused);
		}

		return ApplyNativeHalf(damage, keypads, geysers, nativeDamages, nativeRunFields);
	}

	/// <summary>
	/// The native half: the layer facts (keypad codes, geyser liquid types and the
	/// partial block damage) and the run values (the clock base and the recipe
	/// unlock table). Everything that cannot be put back is RETURNED as damage for
	/// the restore report, because a value left to re-derive is a value the player
	/// can see (§6: never a quiet default).
	/// </summary>
	private List<string> ApplyNativeHalf(
		List<string> damage,
		List<KeypadEntryMsg> keypads,
		List<GeyserStateEntryMsg> geysers,
		List<BlockDamageEntryMsg> nativeDamages,
		SaveNativeRunFields? nativeRunFields)
	{
		var runFields = TakeRunFields(nativeRunFields, damage);
		var nativeCount = nativeDamages.Count;
		if (keypads.Count + geysers.Count + nativeCount == 0 && runFields is null)
		{
			return damage;
		}

		if (nativeWorldFacts is null)
		{
			// Names what was actually LOST, not a zero count: a snapshot whose only
			// native content is the run fields must not be reported as "0 keypad
			// code(s) ... were not restored".
			var runFieldText = runFields is null
				? string.Empty
				: $", the run clock base ({runFields.SavedRunTime:F1}s) and {runFields.Recipes.Count} recipe unlock row(s)";
			damage.Add($"{keypads.Count} keypad code(s), {geysers.Count} geyser liquid type(s) and {nativeCount} game block-damage row(s){runFieldText} were not restored (no native applier in this build)");
			log.LogError(
				"Restored world WITHOUT {Keypads} keypad code(s), {Geysers} geyser liquid type(s), {Damages} game block-damage row(s){RunFields}: this build has no INativeWorldFacts, so those native facts cannot be put back.",
				keypads.Count, geysers.Count, nativeCount, runFieldText);
			return damage;
		}

		// The adapter owns these tables: the Continue click runs before the world
		// object exists, so the restore hands the values over rather than writing
		// them from here. The adapter writes the layer facts into the live world at
		// its world-entry seam and the run values at the slot the native save used
		// to occupy (§4/§6.1), which is also where a row its own tables refuse is
		// logged.
		try
		{
			nativeWorldFacts.ApplyKeypadCodes(keypads);
			nativeWorldFacts.ApplyGeysers(geysers);
			nativeWorldFacts.ApplyBlockDamages(nativeDamages);
			if (runFields is { } fieldsToApply)
			{
				// The clock base goes to the native save slot (WorldGeneration.Start
				// derives the layer's time limit from it); the recipe unlock table goes
				// to the WORLD-ENTRY seam, because only there is the world's recipe
				// table complete — the game rebuilds it in Awake and CUO's mod-content
				// provider appends the custom recipes on a later Update frame.
				nativeWorldFacts.ApplyCutRunFields(fieldsToApply.SavedRunTime);
				nativeWorldFacts.ApplyRecipeUnlocks(fieldsToApply.Recipes);
			}

			log.LogInformation(
				"Handed {Keypads} keypad code(s), {Geysers} geyser liquid type(s), {Damages} game block-damage row(s) and {RunFields} to the native applier.",
				keypads.Count, geysers.Count, nativeCount,
				runFields is null ? "no native run fields" : $"run fields (clock {runFields.SavedRunTime:F1}, {runFields.Recipes.Count} recipe row(s))");
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

	/// <summary>
	/// The run fields a restore has to hand over — or null when the snapshot does
	/// not carry them, which is a NAMED gap: the continued run keeps whatever clock
	/// and recipe state the live session has, and the player is told that the
	/// archive could not describe those fields. Never a quiet default in either
	/// direction (§6).
	/// </summary>
	private SaveNativeRunFields? TakeRunFields(SaveNativeRunFields? nativeRunFields, List<string> damage)
	{
		if (nativeRunFields is not null)
		{
			return nativeRunFields;
		}

		const string missing = "the snapshot carries no native run fields (the run clock base and the recipe unlock table), so the continued run keeps the live values";
		damage.Add(missing);
		log.LogWarning("Restored world WITHOUT native run fields: {Missing}.", missing);
		return null;
	}

}
