using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The WHEN of a local character restore: the queue, the position gate, the two-frame
/// wipe/restore rhythm, the native-field report, the favourite seed and the wearable
/// forward. The write half lives in <see cref="CharacterRestoreApplier"/> and
/// <see cref="WearableRestorer"/>; this class owns only the order and the one-shot rules
/// (the position lands once per body, the items apply after generation, the second pass
/// runs a frame after the wipe). <see cref="CharacterDataSync"/> keeps the 1 Hz report,
/// the capture and the clone table, and forwards the restore API to this class.
/// </summary>
internal sealed class LocalCharacterRestore(
	IWorldControl world,
	CharacterRestoreApplier applier,
	WearableRestorer wearables,
	LocalFavouriteMarkSync favouriteMarks,
	LocalCharacterRestoreQueue restore,
	ILogger log)
{
	private readonly RestorePositionGate _positionGate = new(); // the restore's position lands ONCE per body (a re-sent restore must not teleport it again)

	/// <summary>True = a character snapshot is waiting for (or is half-way onto) the local body.</summary>
	internal bool RestorePending => restore.HasPending || restore.WipePending;

	/// <summary>
	/// Queue a full restore for the LOCAL body from a run THIS client owns: its own CUO
	/// continue, or its own next-level auto-respawn. Both use the same two-frame
	/// wipe/restore path as a guest reconnect restore, so a role never gets a different
	/// flavour of "my character came back". The caller prepares <paramref name="data"/>
	/// with <c>Position = null</c> when the body must stay where the world placed it;
	/// this method deliberately does not reset the position gate (that only resets when
	/// the body leaves the world), so an in-world body is never teleported by this queue.
	///
	/// Queue it BEFORE the local body exists: while a restore is queued, the coordinator
	/// suppresses the 1 Hz report, which is what keeps the fresh body's live snapshot
	/// from overwriting the character being restored. Solo play has no session, so this
	/// is deliberately not session-gated — the callers own that decision.
	/// </summary>
	internal void QueueOwnRun(CharacterDataMsg data)
	{
		restore.Queue(data, ownRun: true);
		log.LogInformation("Queued this run's local character restore ({Items} items, position {Position}).",
			data.Items.Count, data.Position is { } pos ? $"({pos.X:F1},{pos.Y:F1})" : "<none>");
	}

	/// <summary>Queue a PEER's hand-over restore (the host sends it before the run announce, on the same reliable ordered channel).</summary>
	internal void QueuePeerHandover(CharacterDataMsg data) => restore.Queue(data, ownRun: false);

	/// <summary>The session ended or a new run started: nothing queued can reach a body any more.</summary>
	internal void Clear() => restore.Clear();

	/// <summary>
	/// The body that took the restore's FIRST pass is leaving: the second pass can never
	/// complete on it, and a later body must not receive HALF a restore (its items without
	/// the wipe, or its stats without its items). The respawn path queues its own restore
	/// when a respawn is what this is. Returns true when one was dropped.
	/// </summary>
	internal bool DropHalfRunRestore()
	{
		if (!restore.WipePending)
		{
			return false;
		}

		restore.Clear();
		log.LogInformation("Dropped a local character restore whose first pass had already run: the body it belonged to left the world.");
		return true;
	}

	/// <summary>The body left the world — the next body's restore position applies again (the gate reset lives HERE, not on restore arrival).</summary>
	internal void OnBodyLeft() => _positionGate.OnBodyLeft();

	/// <summary>
	/// A run THIS client starts on its own (its start click, its own continue) is taking over:
	/// nothing that waited here can belong to it, so everything goes — including a peer's
	/// hand-over this client never followed. A silent cancel is what would make a lost restore
	/// look like a working one, so the drop is logged.
	/// </summary>
	internal void CancelAllLocalRestores()
	{
		if (!restore.CancelAll())
		{
			return;
		}

		log.LogInformation("Dropped the character restore waiting from an earlier run: this client is starting its own run.");
	}

	/// <summary>
	/// This client is FOLLOWING a run someone else announced: the restore its OWN run queued
	/// cannot reach a body any more and goes; a PEER's hand-over is exactly the restore this
	/// follow is for and stays (the host sends it before the announce, on the same reliable
	/// ordered channel). See <see cref="LocalCharacterRestoreQueue"/>.
	/// </summary>
	internal void CancelOwnRunRestore()
	{
		if (!restore.CancelOwnRun())
		{
			return;
		}

		log.LogInformation("Cancelled the character restore queued by this client's own run: that run never reached a body.");
	}

	/// <summary>Pump entry used by the run coordinator: applies a pending host restore once the local body exists.</summary>
	internal void UpdateRestore(Body localBody)
	{
		if (restore.HasPending)
		{
			TryApply(localBody);
		}
	}

	/// <summary>
	/// Apply only the pending restore's position — the run coordinator calls this
	/// on the body's first frame, BEFORE reporting the scene state, so the host
	/// spawns the clone at the restored spot (ReportedSpawnPos) instead of the
	/// landing spot and then teleporting it (observed: the host saw the
	/// reconnecting guest's clone jump). The stats wipe + items stay on their own
	/// two-frame rhythm (<see cref="TryApply"/>); the position gate keeps this idempotent.
	/// </summary>
	internal void ApplyPendingPositionOnly(Body body)
	{
		if (restore.Pending is { } pending)
		{
			ApplyPendingPosition(body, pending);
		}
	}

	/// <summary>
	/// Restore one worn item onto the local body — the same write the restore's second
	/// pass performs, exposed for a cross-player interaction that hands the local player a
	/// wearable. The implementation lives in <see cref="WearableRestorer"/>, which both
	/// callers share.
	/// </summary>
	internal void RestoreWearable(CharacterItemMsg itemData, Body body) => wearables.RestoreWearable(itemData, body);

	private void TryApply(Body body)
	{
		if (restore.Pending is not { } pending)
		{
			return;
		}

		// The position applies on the body's FIRST frame — before the
		// generation guard, so a fresh spawn never visibly sits at the landing
		// spot and then jumps (observed: rejoin spawned at the landing spot,
		// then teleported to the disconnect spot when the full restore ran).
		ApplyPendingPosition(body, pending);

		// The ITEMS apply only once world generation finished: the game hands
		// out the starting supplies inside generation (WorldPlacePlayer), and
		// the restore wipes the slots first — applying during generation would
		// race that handout (observed: the default lantern ending up on the
		// ground instead of in the restored inventory).
		if (HarmonyTraverse.IsGenerating())
		{
			return;
		}

		if (restore.WipePending)
		{
			// Second pass (next frame): the wipe's Destroy ran at the end of
			// the previous frame, so the slots are actually empty now and
			// PickUpItem succeeds — it silently refuses a non-empty slot
			// (Body.cs:1388), which stranded the restored items on the ground.
			applier.ApplyItems(body, pending);

			// The native character fields are the LAST step, and they have to be: a
			// snapshot that carries none of them is reported here — by name — because
			// the game skips its own fresh roll when a run is continued
			// (PlayerCamera.cs:726-729), so those fields would otherwise stay at the
			// defaults the fresh body was created with and the player would never be
			// told (§6/§6.1).
			ReportNativeFields(pending, applier.ApplyNativeFields(body, pending));

			// The character's local favourite marks come back with it, on the same pass
			// the native fields land on.
			favouriteMarks.SeedFrom(pending);

			restore.Clear();
			return;
		}

		applier.ApplyStatsAndWipe(body, pending);
		restore.MarkWipePending();
	}

	/// <summary>
	/// The local account of a restore's native-field half: a snapshot without them
	/// is named here (the archive half of the same absence is named by the Runtime's
	/// restore summary — <c>CharacterNativeFieldPolicy.Missing</c>), and every value
	/// the live game refused to take is named too. Information, not a warning: both
	/// cases are expected for an old snapshot, and the loud half belongs to the
	/// restore report (§6).
	/// </summary>
	private void ReportNativeFields(CharacterDataMsg pending, IReadOnlyList<NativeFieldWrite> writes)
	{
		if (pending.NativeFields is null)
		{
			log.LogInformation(
				"Character restore carries no native character fields (lastHappiness, caloriesConsumed, WoundView.cInfo): this body keeps the live defaults for them.");
		}

		foreach (var write in writes.Where(write => !write.Applied))
		{
			log.LogWarning("Character restore did not write {Field}: {Refusal}.", write.Description, write.Refusal);
		}
	}

	/// <summary>
	/// Write the snapshot's position once per body. Zero velocity: the body must not
	/// keep the fresh spawn's momentum into the restored spot.
	/// </summary>
	private bool ApplyPendingPosition(Body body, CharacterDataMsg pending)
	{
		if (!_positionGate.ShouldApplyPosition || pending.Position is not { } pos)
		{
			return false;
		}

		_positionGate.MarkPositionApplied();

		// The comparison belongs HERE, not in the coordinator's receive path: a reconnecting
		// member's saved character is sent BEFORE the run-baseline checkpoint (the handshake's
		// own order), so at arrival this side has no current generation and the comparison
		// would answer "unknown". By the time the body exists, the baseline names the layer
		// this body was generated in.
		if (CharacterRestorePositionPolicy.IsFromAnotherLayer(pending.Generation, world.CurrentGeneration))
		{
			// The snapshot's position belongs to the layer it was captured in, and this client has
			// generated a new one since: those coordinates now describe terrain nobody stood on, so
			// applying them would land the member away from the group. The body stays where its own
			// generation placed it; the snapshot's other facts still restore.
			log.LogInformation(
				"[Character] the restore's saved position belongs to layer {Saved} while this client is in layer {Current} — it is not applied; the body stays at this layer's own spawn.",
				pending.Generation?.LayerIndex,
				world.CurrentGeneration?.LayerIndex);
			return false;
		}

		body.transform.position = new Vector3(pos.X, pos.Y, 0f);
		body.rb.velocity = Vector2.zero;
		log.LogInformation("Character restore position applied at spawn: ({X:F1},{Y:F1}).", pos.X, pos.Y);
		return true;
	}
}
