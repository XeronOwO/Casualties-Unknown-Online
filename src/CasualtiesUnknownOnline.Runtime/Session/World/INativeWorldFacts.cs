using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world facts that only the Game Adapter can read back and re-apply: the
/// DECIDED values a layer's generation produced but the Runtime cannot see —
/// keypad codes and geyser liquid types — plus the partial block damage, which
/// has NO Runtime table at all and lives only in the GAME's own list. Keypads and
/// geysers are captured with their entity's world position, which is the identity
/// (both sides regenerate the same object at the same place); the damage table is
/// keyed by its block cell.
///
/// It is the native half of <see cref="IWorldFactSource"/> and is deliberately
/// OPTIONAL: a build that registers no implementation captures and restores no
/// native fact, which is the layer-end path (a layer-end cut writes no in-layer
/// fact at all, so nothing is lost silently). A caller that HAS one captures it
/// into the cut and applies it after the Runtime facts.
///
/// The capture/apply split follows the game's own lifetime: every table here
/// exists only while a GENERATED world object does. A restore therefore cannot
/// write them at the Continue click — the world is still generating, and the
/// entities a keypad code or a geyser type belongs to do not exist yet. The
/// adapter holds the handed-over values and applies them at the world-entry edge
/// (the host's first frame after the generation completed, the same seam the
/// world-entry broadcasts use), which is why the apply methods are
/// "carry these until the world exists" rather than an immediate write. The
/// adapter must NOT try to write while the world object is absent.
///
/// The partial block damage has its own kind in the file
/// (<c>native-block-damage</c>) because only this port can read and write the
/// list it lives in. CUO keeps no copy of it: a second bounded registry beside
/// the game's own 128-entry list is exactly what let the two sets drift, so the
/// registry was DELETED rather than taught the same eviction. The kind is carried
/// by the PAYLOAD the caller hands over, so an implementation never has to guess
/// where a row belongs.
/// </summary>
public interface INativeWorldFacts
{
	/// <summary>
	/// ONE read of every native table the cut carries: the decided keypad codes,
	/// the geyser liquid types and the game's OWN partial block-damage list
	/// (<c>WorldGeneration.world.blockDamages</c> — the live table the game breaks
	/// blocks from, including damage CUO's report hooks never observed). All three
	/// are read at one instant, and a table that cannot be read is reported
	/// through <see cref="NativeWorldFactCapture.Failure"/> instead of coming back
	/// as "empty" (which the save layer could not tell apart from a real one).
	/// </summary>
	NativeWorldFactCapture Capture();

	/// <summary>
	/// The game's own partial-damage list, and ONLY that — the late-joiner
	/// snapshot's read (world entry / reconnect / the 60 s resend). It is
	/// deliberately narrower than <see cref="Capture"/>: reading the keypad codes
	/// GENERATES the ones the game has not rolled yet
	/// (<c>KeypadMinigame.GenerateCode</c> consumes the host's random stream), so a
	/// damage snapshot must not move that roll. Null = the list could not be read
	/// (there is no live world); an empty list is a real empty table.
	/// </summary>
	IReadOnlyList<BlockDamageEntryMsg>? CaptureBlockDamages();

	/// <summary>
	/// Host only: merge a guest's ABSOLUTE partial-damage report into the game's
	/// own list and return THIS host's authoritative value for EVERY reported
	/// cell (0 = this host holds no damage for it), which the caller relays as the
	/// report's answer. The merge never lowers a cell's damage — two players'
	/// contributions and this host's own all count — and never breaks a block: a
	/// row outside a surviving block's range, or one the list's own 128-entry cap
	/// cannot take, is refused by the same rules the snapshot apply uses, and the
	/// host's current value (possibly none) is what the answer carries.
	///
	/// Separate from <see cref="CaptureBlockDamages"/> because it is the only
	/// WRITE this port performs on the game's list from a peer's input, and it is
	/// reached only while a live world exists (the report of a member that arrived
	/// before the world is not mergeable at all). Null = no live world to merge
	/// into: nothing is merged and nothing is answered, so the reporter's pending
	/// entry survives to the next cycle instead of being cleared against a table
	/// that does not exist.
	/// </summary>
	IReadOnlyList<BlockDamageEntryMsg>? MergeBlockDamages(IReadOnlyList<BlockDamageEntryMsg> reported);

	/// <summary>
	/// The live recipe table's UNLOCKED indices — every recipe that currently
	/// draws no INT requirement (<c>Recipe.INT == 0</c>, the state the game's OWN
	/// blueprint use writes, <c>Item.cs:4284</c>). This is the recipe-unlock
	/// backfill's read (sync-coverage audit I6), and both halves of it are the
	/// same fact: the host's world-entry / 60 s repair set and a guest's
	/// re-reported set are read from the same table at send time.
	///
	/// Deliberately narrower than <see cref="CaptureRunFields"/>: no
	/// <c>hasMadeBefore</c> (a per-player crafting history, not a run fact) and no
	/// clock. Null = the table could not be read (no live world, or a slot this
	/// build cannot describe); an EMPTY list is a real "nothing is unlocked"
	/// table, which is why a caller must never send one as if it were a refusal —
	/// the set is unlock-only, so an empty one asks for no write at all.
	/// </summary>
	IReadOnlyList<int>? CaptureUnlockedRecipeIndexes();

	/// <summary>
	/// ONE read of the native values a cut carries that the kernel's run baseline
	/// does not hold: the two rarity multipliers (which the encoder stamps into the
	/// run baseline row, because that is where a side that GENERATES the layer
	/// reads them) plus the run clock base and the recipe table's unlock state.
	///
	/// Separate from <see cref="Capture"/> because it is safe at every seam — it
	/// rolls no random value — so the layer-end cut carries them too (a cut that
	/// did not would let a continued world start its clock at zero and re-lock
	/// every recipe). A reader that met no live world reports the failure instead
	/// of returning defaults.
	/// </summary>
	NativeRunFields CaptureRunFields();

	/// <summary>
	/// Host only: a RESTORED cut's run clock base is waiting for the world. The
	/// adapter writes it when a live world can take it (the world does not exist at
	/// the Continue click, and <c>WorldGeneration.Start</c> derives the layer's time
	/// limit from it before any later seam). The rarity multipliers are NOT handed
	/// over here — they ride the restored run baseline, which the
	/// generation-parameter path applies. The recipe unlock table is not handed over
	/// here either: it needs the world's COMPLETE recipe table, so it goes through
	/// <see cref="ApplyRecipeUnlocks"/> and lands at the world-entry seam.
	/// </summary>
	void ApplyCutRunFields(float savedRunTime);

	/// <summary>
	/// Host only: the restored recipe unlock table is waiting for the world-entry
	/// seam. It is a world-entry value rather than a save-slot value because the game
	/// rebuilds <c>Recipes.recipes</c> in <c>WorldGeneration.Awake</c> and CUO's
	/// mod-content provider appends the custom recipes on a LATER Update frame
	/// (<c>GameAdapterRecipeContentProvider</c>) — a row written before that would
	/// name a recipe the table does not have yet.
	/// </summary>
	void ApplyRecipeUnlocks(IReadOnlyList<SaveRecipeUnlockRow> recipes);

	/// <summary>
	/// Both roles: apply the run baseline's generation-boundary rarity multipliers.
	/// They belong to the baseline (a layer's loot/trap distribution is scaled by
	/// them), so a side that generates with the game's fresh 1f builds a different
	/// layer than the run's authority. Returns false when there is no live world
	/// yet (the host's Continue click runs before the scene loads) — the values are
	/// then held for <see cref="TryWritePendingRunFields"/>.
	/// </summary>
	bool ApplyRunGenerationMultipliers(float lootRarityMultiplier, float trapRarityMultiplier);

	/// <summary>
	/// Write every restored run value that is still waiting, into the live world.
	/// Called from the slot the native <c>SaveSystem.TryLoadGame</c> used to run in,
	/// before <c>WorldGeneration.Start</c> derives the layer's time limit from them.
	/// That slot is also the ONLY one they need: the world object exists there
	/// (<c>WorldGeneration.Awake</c> assigns it before <c>Start</c>), so a <c>false</c>
	/// return means the caller met a composition that has no live world at all and
	/// the values stay pending for the next run's Start — never written, never
	/// silently dropped. The recipe unlock table is NOT waiting here: it needs the
	/// world's complete recipe table and is read through
	/// <see cref="ReadPendingRestore"/> at the world-entry seam instead.
	/// </summary>
	bool TryWritePendingRunFields();

	/// <summary>Host only: apply the restored keypad codes absolutely (replace, never merge).</summary>
	void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes);

	/// <summary>Host only: apply the restored geyser liquid types absolutely.</summary>
	void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers);

	/// <summary>
	/// Host only: apply the restored entries of the game's partial-damage list
	/// absolutely (replace, never merge). A row this list's own cap refuses is
	/// named by the LIVE-WORLD replay, which returns every write's applied/refused
	/// counts — the account a restore's caller reads (the restore-report
	/// completeness rule of the format doc §6).
	/// </summary>
	void ApplyBlockDamages(IReadOnlyList<BlockDamageEntryMsg> damages);

	/// <summary>
	/// Host only: restored native values are waiting for the world-entry seam —
	/// the adapter holds them because the world they belong to does not exist yet.
	/// False for every normal run.
	/// </summary>
	bool HasPendingRestore { get; }

	/// <summary>
	/// Host only: read the waiting restored values WITHOUT consuming them. A
	/// generation that cannot take every value must keep them for the retry, so
	/// the replay reads first and commits only after the live world has them all.
	/// <see cref="NativeWorldFactRestore.Empty"/> when nothing is pending.
	/// </summary>
	NativeWorldFactRestore ReadPendingRestore();

	/// <summary>Host only: the waiting restored values ARE in the live world — the handover is done, and a second generation must never be handed the same set.</summary>
	void CommitPendingRestore();

	/// <summary>
	/// Host only: a run that will never reach the world-entry seam supersedes the
	/// waiting values — a refused or abandoned Continue, or the end of the
	/// session. Without this the NEXT run's first generation would be mistaken for
	/// the generation the cut was restored for and would receive the old world's
	/// keypad codes, geyser liquid types, block damage, run clock and recipe
	/// unlocks.
	/// </summary>
	void CancelPendingRestore();
}
