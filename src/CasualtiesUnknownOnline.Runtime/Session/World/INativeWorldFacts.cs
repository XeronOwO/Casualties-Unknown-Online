using System.Collections.Generic;
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
	/// <summary>Every keypad code the host decided for this layer.</summary>
	IReadOnlyList<KeypadEntryMsg> CaptureKeypadCodes();

	/// <summary>Every geyser's decided liquid type.</summary>
	IReadOnlyList<GeyserStateEntryMsg> CaptureGeysers();

	/// <summary>
	/// Every entry of the game's OWN partial block-damage list
	/// (<c>WorldGeneration.world.blockDamages</c>) — the live table the game
	/// breaks blocks from, including damage CUO's report hooks never observed
	/// (the unhooked direct <c>DamageBlock</c> callers).
	/// </summary>
	IReadOnlyList<BlockDamageEntryMsg> CaptureBlockDamages();

	/// <summary>Host only: apply the restored keypad codes absolutely (replace, never merge).</summary>
	void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes);

	/// <summary>Host only: apply the restored geyser liquid types absolutely.</summary>
	void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers);

	/// <summary>
	/// Host only: apply the restored entries of the game's partial-damage list
	/// absolutely (replace, never merge). A row this list's own cap refuses is
	/// named by the applier, but it reaches the LOG only — the restore-report gap
	/// that keeps it out of <c>WorldContinueOutcome.Summary</c> is tracked in
	/// `todo/save-mid-run-consistent-cut.md` (scope 6).
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
	/// keypad codes, geyser liquid types and block damage.
	/// </summary>
	void CancelPendingRestore();
}
