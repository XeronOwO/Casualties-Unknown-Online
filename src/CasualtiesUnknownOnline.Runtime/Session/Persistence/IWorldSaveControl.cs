using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The save system's narrow surface for the Game Adapter and packet handlers —
/// the Runtime owns the repository, the format and the kernel, the adapter only
/// reports the moments the game itself knows about (a run starts, the host
/// continues an existing one, the host's pump reached the frame-end seam) and
/// asks whether a CUO world can be continued.
///
/// A cut is never taken from inside the callback that asked for it. A command
/// (<c>/save</c>) ARMS a cut, and the adapter's pump takes it at the one
/// consistent seam it owns (the frame-end pump point, where no command batch and
/// no frame flush is in flight); the deliberate menu return arms the same kind of
/// cut at the same seam instead of writing one inline. That is also why a cut can
/// be DEFERRED: an in-flight state the policy resolves first (a break's drops, a
/// drop's throw) keeps the request armed for a frame or two.
///
/// The world is never saved by the adapter: every trigger is decided here, so a
/// save can never run from a half-applied batch and every transport role is
/// decided in one place.
/// </summary>
public interface IWorldSaveControl
{
	/// <summary>False when this composition root has no save repository (tests, or a build that opted out).</summary>
	bool IsEnabled { get; }

	/// <summary>
	/// Host: the host clicked start — the run this session plays gets its own
	/// world folder. False = the world could not be created (the run still plays;
	/// it just cannot be saved) or this entry is the TUTORIAL, which gets no
	/// archive at all (its own identity is released, so a later <c>/save</c> can
	/// never aim at the previous run's world).
	/// </summary>
	bool TryBeginRun(bool isTutorial);

	/// <summary>
	/// Host: arm a cut for the next pump seam. <paramref name="reason"/> is the
	/// trigger (a console command, the deliberate menu return). False = this host
	/// can never take one right now; <paramref name="refusal"/> says why, in the
	/// words the console shows. Arming twice is idempotent; a later reason
	/// supersedes an earlier one at the same seam.
	/// </summary>
	bool TryRequestCut(WorldCutReason reason, out string? refusal);

	/// <summary>True = a cut is armed and waiting for the seam (or for in-flight state to resolve).</summary>
	bool HasArmedCut { get; }

	/// <summary>
	/// Host: the frame-end seam — take the armed cut. <paramref name="hostCharacter"/>
	/// is the host's character captured from the live body at this instant (null =
	/// fall back to the last reported snapshot); <paramref name="liveTransients"/>
	/// is the adapter's half of the transient observation (the game-side windows:
	/// a pending break, a trap drop hold, a drop flush). Returns null when nothing
	/// was armed; a <see cref="WorldCutResult.Deferred"/> result keeps the request
	/// armed for the next frame.
	/// </summary>
	WorldCutReport? TryCaptureArmedCut(
		CharacterDataMsg? hostCharacter,
		int frame,
		IReadOnlyList<WorldTransientCount>? liveTransients = null);

	/// <summary>
	/// Every finished cut attempt (captured or refused — a deferral is not a
	/// result). The command console renders the player-initiated ones and the log
	/// keeps the rest: a cut that could not be written, or one that had to leave
	/// in-flight state behind, must not be discoverable only by reading the log.
	/// </summary>
	event Action<WorldCutReport>? CutReported;

	/// <summary>
	/// Every RESOLVED Continue attempt, once: the click's own account of what it
	/// applied, what it lost and why it refused — raised by
	/// <see cref="TryContinue"/> as the attempt resolves, and again by
	/// <see cref="AbandonRestore"/> when an applied attempt no generation will
	/// consume. The log keeps the same lines, but §6 requires the restore's losses
	/// in-game (the count per domain, the reason, the affected content id, the
	/// backup a load fell back to), and a click that does nothing is exactly the
	/// failure a player cannot diagnose from a log they never open.
	/// </summary>
	event Action<WorldRestoreReport>? RestoreReported;

	/// <summary>The world this run writes into ("" before a run started); the Runtime owns it, the adapter only reports it in logs.</summary>
	string CurrentWorldId { get; }

	/// <summary>The world the Continue entry would open (null when none is openable); the adapter logs it and the tests pin the rule.</summary>
	string? ContinueWorldId { get; }

	/// <summary>True = the repository holds at least one world the Continue entry can open.</summary>
	bool HasRestorableWorld { get; }

	/// <summary>
	/// Host: the Continue entry was used. Loads the world the repository resolves
	/// as "the selected one", applies the kernel checkpoint and the stored
	/// characters, and returns whether the run may start. A refusal is never a
	/// silent fallback to the native regenerate path.
	///
	/// The restore's characters part in two: a stored key a PEER claims is bound
	/// into the character table the reconnect path already sends from, and the key
	/// the LOCAL player claims comes back in
	/// <see cref="WorldContinueOutcome.LocalCharacter"/> — only the adapter can put
	/// it on a body, and the body does not exist before the scene loads.
	/// </summary>
	bool TryContinue(out WorldContinueOutcome outcome);

	/// <summary>The characters of the last continuation, by player key — the raw material of S4's guest claims.</summary>
	IReadOnlyList<SavedCharacter> PendingCharacters { get; }

	/// <summary>
	/// The Continue attempt will never reach its world-entry seam (the restored run
	/// baseline could not be published, so no generation runs): every handover that
	/// click armed is released here — the restore's account, the Runtime world-fact
	/// tables, the kernel's restored per-entity arm, the adapter's native handover and
	/// the item reconcile — instead of waiting for the next run to cancel them.
	///
	/// The attempt gets its LAST word here: an APPLIED attempt that is still outstanding
	/// raises one more <see cref="RestoreReported"/> report with
	/// <see cref="WorldRestoreReport.Disposition.Abandoned"/> (the click already reported
	/// the application), because a run that does not start is exactly what the player
	/// must not have to read a log to learn. An attempt that is not outstanding — no
	/// click, a refused one, one already abandoned or superseded by a new run — releases
	/// the same handovers and reports NOTHING: a restore that never happened is not a
	/// restore that succeeded. <paramref name="reason"/> names why, in the log, in the
	/// abandonment report and in each release.
	/// </summary>
	void AbandonRestore(string reason);
}
