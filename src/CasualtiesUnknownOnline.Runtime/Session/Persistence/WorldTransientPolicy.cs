using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The cut's transient policy — one explicit verdict per class of in-flight
/// state, so a mid-run cut never loses state silently (§4/§6 of the format doc,
/// and the ticket's "no silent loss" rule).
///
/// The three verdicts and why the rows split the way they do:
///
/// - <see cref="WorldTransientVerdict.ResolveBeforeSave"/> covers the states that
///   exist only because a game operation spans frames, and whose world effect is
///   reported to the kernel by the SAME flush the state waits for: a break's
///   drops do not exist yet in the frame the block broke, a drop's throw velocity
///   lands a frame later. Capturing them is impossible (there is nothing to
///   capture) and dropping them would lose the items, so the cut WAITS — the
///   armed request stays armed and the frame-end seam retries, bounded by
///   <see cref="WorldSaveService.MaxCutDeferralFrames"/>.
/// - <see cref="WorldTransientVerdict.DropWithLog"/> covers states whose world
///   effect is already in the kernel (a medical session's applied wounds, a
///   pickup that has not happened) or that the restored world re-derives (the run
///   clock, the earthquake timers, item velocity — the kernel owns positions
///   only). Capturing them would be a second source of truth; a cut therefore
///   NAMES them in the report and goes on.
/// - <see cref="WorldTransientVerdict.Capture"/> is the world fact a cut carries
///   as data (the decided native values and the radiation line ride
///   <c>world-transients.json</c>).
/// </summary>
public static class WorldTransientPolicy
{
	/// <summary>A local break holds its report one frame for the drops' <c>Item.Start</c> (BlockBreakPendingState).</summary>
	public const string BlockBreakPendingKey = "block-break-pending";

	/// <summary>A destructive trap holds its event for the death-branch drops (TrapDropPendingState, two frames).</summary>
	public const string TrapDropHoldKey = "trap-drop-hold";

	/// <summary>A local drop holds its report one frame for the throw velocity (DropPendingState).</summary>
	public const string DropFlushKey = "drop-flush";

	/// <summary>A pickup claim that beat its item's spawn report, held in PendingPickupQueue.</summary>
	public const string PickupQueueKey = "pickup-queue";

	/// <summary>An injected/removal operation whose session is still open.</summary>
	public const string MedicalSessionKey = "medical-session";

	/// <summary>A shrapnel-removal session still open.</summary>
	public const string ShrapnelSessionKey = "shrapnel-session";

	/// <summary>Any of the other-medical (splint/tourniquet/…) sessions still open.</summary>
	public const string OtherMedicalSessionKey = "other-medical-session";

	/// <summary>The game's own craft coroutine — CUO holds no craft-batch state of its own.</summary>
	public const string CraftBatchKey = "craft-batch";

	/// <summary>A guest's creation report this side has not resolved yet (RuntimeEntityChannel's fallback table).</summary>
	public const string DeferredEntityReportKey = "deferred-entity-report";

	/// <summary>Item velocity / rotation / angular velocity — the kernel owns positions only.</summary>
	public const string ItemPhysicsKey = "item-physics";

	/// <summary>The decided native values and the host's radiation line: carried by <c>world-transients.json</c>.</summary>
	public const string DecidedNativeValuesKey = "decided-native-values";

	/// <summary>The run clock / timelimit — a native run field, decided per field by S3.4.</summary>
	public const string WorldClockKey = "world-clock";

	/// <summary>The game's own earthquake countdown/intensity fields.</summary>
	public const string EarthquakeTimersKey = "earthquake-timers";

	/// <summary>
	/// Every in-flight class the cut must decide about, in report order. The
	/// owners are the ticket's table verbatim (an owner renamed by a refactor
	/// updates this table in the same change).
	/// </summary>
	public static IReadOnlyList<WorldTransientRow> Rows { get; } =
	[
		new(BlockBreakPendingKey, "BlockBreakPendingState", WorldTransientVerdict.ResolveBeforeSave,
			"block-break report(s) waiting for their drops",
			"A local break spans two frames: the drops' Item.Start runs the frame after the block broke, and the flush registers them with the kernel. A cut taken now would name the air cell but not the items the game is about to create."),
		new(TrapDropHoldKey, "TrapDropPendingState", WorldTransientVerdict.ResolveBeforeSave,
			"trap event(s) holding for their drops",
			"The destructive trap's event is held until the death branch's items have run Item.Start, so the trap fact and its drops reach the kernel as one composite. Cutting inside the hold loses the drops."),
		new(PickupQueueKey, "PendingPickupQueue", WorldTransientVerdict.DropWithLog,
			"pickup claim(s) waiting for their spawn report",
			"A network claim window (500 ms), not world state: the claim was made by a client whose item report was still in flight. A restored world has the item; the claim is stale and the claiming client re-reports."),
		new(DropFlushKey, "DropPendingState", WorldTransientVerdict.ResolveBeforeSave,
			"drop report(s) waiting for the throw",
			"The drop was applied in the live world but its kernel fact follows the final throw velocity one frame later. Cutting inside the window restores the item to its pre-drop owner."),
		new(MedicalSessionKey, "MedicalOperationSessionService", WorldTransientVerdict.DropWithLog,
			"medical operation(s) in progress",
			"The session is an interaction protocol over kernel state: the applied wounds/injections are already facts, and the in-flight needle position is presentation. A restore leaves the patient's facts and drops the session."),
		new(ShrapnelSessionKey, "ShrapnelOperationSessionService", WorldTransientVerdict.DropWithLog,
			"shrapnel removal(s) in progress",
			"Same split as medical: the completed pieces are kernel facts, the in-progress removal is a session the player restarts."),
		new(OtherMedicalSessionKey, "OtherMedicalOperationSessionService", WorldTransientVerdict.DropWithLog,
			"other medical operation(s) in progress",
			"Same split as medical: the bandage/splint/tourniquet result is a kernel fact."),
		new(CraftBatchKey, "game craft coroutine", WorldTransientVerdict.DropWithLog,
			"craft batch(es) in progress",
			"The batch lives in the game's own coroutine, not in CUO. Every completed craft already committed its ingredient destroys and product spawn as kernel facts; a restore starts a fresh, empty batch."),
		new(DeferredEntityReportKey, "RuntimeEntityChannel", WorldTransientVerdict.DropWithLog,
			"deferred entity creation report(s)",
			"A guest's creation report that this side has not resolved yet — a network artifact. The entity exists in the reporting client's world and is re-reported on its own fallback cadence."),
		new(ItemPhysicsKey, "item motion path", WorldTransientVerdict.DropWithLog,
			"item physics transient(s) (velocity/rotation)",
			"The kernel owns item X/Y and a settled flag; velocity, rotation and angular velocity ride the wire only. A restored item keeps its position and loses its momentum — the item is not lost, it lands."),
		new(DecidedNativeValuesKey, "NativeWorldFacts", WorldTransientVerdict.Capture,
			"decided keypad/geyser/radiation value(s)",
			"Decided values a regeneration would re-roll (keypad codes, geyser liquid types) and the host's radiation line are carried as rows; re-rolling them would contradict what a player already saw."),
		new(WorldClockKey, "native run fields", WorldTransientVerdict.DropWithLog,
			"world-clock state",
			"The run clock/timelimit belong to the native run fields, decided per field by S3.4 (todo/save-native-run-field-parity.md). Until it carries them, this row is named rather than silently defaulted."),
		new(EarthquakeTimersKey, "WorldGeneration countdown fields", WorldTransientVerdict.DropWithLog,
			"earthquake timer(s)",
			"The countdown and intensity are the game's own fields, re-derived when the layer regenerates. Carrying them would freeze a timer across a load the player cannot observe; the retimed quake is named instead."),
	];

	private static readonly Dictionary<string, WorldTransientRow> ByKey =
		Rows.ToDictionary(row => row.Key, StringComparer.Ordinal);

	/// <summary>The row for a key, or null when nothing declared it (an owner bug the cut refuses on).</summary>
	public static WorldTransientRow? Find(string key) =>
		ByKey.TryGetValue(key, out var row) ? row : null;

	/// <summary>True = the key is one of the declared rows; an owner may only report declared keys.</summary>
	public static bool IsKnown(string key) => ByKey.ContainsKey(key);

	/// <summary>True = the cut waits for this state to resolve before it is taken.</summary>
	public static bool MustResolveBeforeSave(string key) =>
		Find(key)?.Verdict == WorldTransientVerdict.ResolveBeforeSave;

	/// <summary>One reported row as a restore/save report fragment, e.g. "2 pickup claim(s) waiting for their spawn report".</summary>
	public static string Describe(WorldTransientCount count)
	{
		var row = Find(count.Key);
		return row is null
			? $"{count.Pending} unknown transient class '{count.Key}'"
			: $"{count.Pending} {row.Unit}";
	}
}
