using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Character-data domain: the session-scoped save/restore (host stores the
/// guests' 1 Hz snapshots per SteamID; a reconnect or a death → re-enter cycle
/// gets the saved state back) and the remote clones' inventory rendering
/// source (the latest snapshot per SteamID). No reentry guards of its own —
/// nothing here mutates game items in a way that fires the item hooks.
/// </summary>
internal sealed class CharacterDataSync(
	ISessionControl session,
	ICharacterDataControl characterData,
	IMapper mapper,
	CloneInventoryRenderer inventoryRenderer,
	CloneFactTable factTable,
	CharacterRestoreApplier restoreApplier,
	WearableRestorer wearables,
	ICharacterNativeSystem nativeSystem,
	ILogger<CharacterDataSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly IMapper _mapper = mapper;
	private readonly CloneInventoryRenderer _inventoryRenderer = inventoryRenderer;
	private readonly CloneFactTable _factTable = factTable;
	private readonly CharacterRestoreApplier _restoreApplier = restoreApplier;
	private readonly WearableRestorer _wearables = wearables;
	private readonly ICharacterNativeSystem _nativeSystem = nativeSystem;
	private readonly ILogger<CharacterDataSync> _log = log;

	/// <summary>A clone's snapshot cache updated (SteamId) — the renderer re-renders that clone's carried items. Without this, the clone only rendered once at creation ("after the starting supplies, the peer never sees carried-item updates").</summary>
	public event Action<ulong>? CloneSnapshotUpdated
	{
		add => _factTable.CloneSnapshotUpdated += value;
		remove => _factTable.CloneSnapshotUpdated -= value;
	}

	private readonly LocalCharacterRestoreQueue _restore = new(); // the local body's pending restore (which snapshot, whose run, which apply phase)
	private readonly RestorePositionGate _positionGate = new(); // the restore's position lands ONCE per body (a re-sent restore must not teleport it again)
	private const float CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)
	private long _nextCharacterReportMs;

	/// <summary>Read-only view for the clone renderer: latest snapshot per SteamId.</summary>
	internal IReadOnlyDictionary<ulong, CharacterDataMsg> CloneData => _factTable.CloneData;

	/// <summary>Carried-fact event (the owner's fact-table entry updates and the clone re-renders immediately) — the fact table lives in CloneFactTable.</summary>
	internal void ApplyCarriedSync(ulong owner, CharacterItemMsg item, bool slotKnown) => _factTable.ApplyCarriedSync(owner, item, slotKnown);

	/// <summary>The owner's starting supplies with self-assigned ids arrived — merged into its fact table (clone render + snapshot divergence baseline).</summary>
	internal void ApplyCarriedInventory(ulong owner, IReadOnlyList<CharacterItemMsg> items) => _factTable.ApplyCarriedInventory(owner, items);

	/// <summary>A carried item left into the world — it leaves the owner's fact table (top-level or nested in a container's contents).</summary>
	internal void RemoveCarriedItem(ulong itemId) => _factTable.RemoveCarriedItem(itemId);

	/// <summary>An enemy bite arrived (report or relay) — update the victim's fact-table entry (limb + body fields) and re-render its clone.</summary>
	internal void ApplyEnemyBite(EnemyBiteMsg msg) => _factTable.ApplyEnemyBite(msg);

	/// <summary>A crystal lunge arrived (report or relay) — update the victim's fact-table entry (limb + adrenaline/stamina) and re-render its clone.</summary>
	internal void ApplyEnemyLunge(EnemyLungeMsg msg) => _factTable.ApplyEnemyLunge(msg);

	/// <summary>An enemy-proximity side effect arrived (report or relay) — update the victim's fact-table entry and re-render its clone.</summary>
	internal void ApplyEnemyEffect(EnemyEffectMsg msg) => _factTable.ApplyEnemyEffect(msg);

	internal void BindToSession()
	{
		_characterData.CharacterDataReceived += OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived += OnHostCharacterDataReceived;
		_characterData.LimbStateEventReceived += OnLimbStateEventReceived;
	}

	internal void Unbind()
	{
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived -= OnHostCharacterDataReceived;
		_characterData.LimbStateEventReceived -= OnLimbStateEventReceived;
	}

	/// <summary>A limb-latch event arrived (report or relay) — update the owner's fact-table entry and re-render its clone (the dedicated event is the trigger; the 1 Hz snapshot is the fallback).</summary>
	private void OnLimbStateEventReceived(ulong sender, LimbStateEventMsg msg) =>
		_factTable.ApplyLimbStateEvent(msg.OwnerSteamId, msg);

	/// <summary>
	/// The local body's limb latch changed (BreakBone/MendBone/Dislocate/
	/// UnDislocate/Dismember — the patch verified the write): capture the
	/// body's FULL post-event terminal state (every limb + the body health —
	/// Dismember also deactivates the lower limbs and mutates the connected
	/// limbs, Limb.cs:91-145) and report it (guest → host) or broadcast it
	/// (host) as the dedicated LimbStateEvent event. The limb indices into
	/// Body.limbs ride the message (the same index space the 1 Hz snapshot
	/// uses).
	/// </summary>
	internal void ReportLimbStateEvent(Limb limb)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var body = limb.body;
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[LimbEvent] limb has no body — not reported.");
			return;
		}

		for (var i = 0; i < body.limbs.Length; i++)
		{
			if (body.limbs[i] == limb) // Unity objects — ==
			{
				_characterData.SendLimbStateEvent(CaptureLimbStateEvent(body));
				_log.LogInformation("[LimbEvent] reported local limb {Limb}.", i);
				return;
			}
		}

		_log.LogWarning("[LimbEvent] limb not found in its body's limbs array — not reported.");
	}

	private LimbStateEventMsg CaptureLimbStateEvent(Body body)
	{
		var health = _mapper.Map<CharacterHealthMsg>(body);
		// Unified capture: face latches/mouth, component state and the
		// leg-speed pose multiplier now ride the same dedicated event as the
		// 1 Hz snapshot, so a limb-latch update also refreshes the full remote
		// presentation without waiting for the next snapshot.
		RemoteCharacterDisplayProjection.Capture(body, health);

		var msg = new LimbStateEventMsg
		{
			OwnerSteamId = _session.LocalSteamId,
			Health = health,
		};

		// Limb has no Index field — Mapster maps the rest, the loop assigns it
		// (the same capture shape as the 1 Hz character snapshot).
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limbMsg = _mapper.Map<CharacterLimbMsg>(body.limbs[i]);
			limbMsg.Index = i;
			msg.Limbs.Add(limbMsg);
		}

		return msg;
	}

	private void OnCharacterDataReceived(ulong sender, CharacterDataMsg data)
	{
		if (_session.Role == SessionRole.Host)
		{
			// A guest's 1 Hz report — render its clone's inventory (the slots
			// show what it is carrying; the new body renders on creation from
			// this cache).
			_factTable.ApplySnapshot(sender, data);
			_log.LogDebug("[CloneRender] host: char data from {Sender} ({Count} items).", sender, data.Items.Count);
			return;
		}

		// The host relays the OTHER guests' reports (OwnerSteamId stamped) —
		// render that guest's clone inventory on this side too; without the
		// relay a guest could never see what another guest carries/wears.
		if (data.OwnerSteamId != 0 && data.OwnerSteamId != _session.LocalSteamId)
		{
			_factTable.ApplySnapshot(data.OwnerSteamId, data);
			_log.LogDebug("[CloneRender] guest: char data relay of {Owner} ({Count} items).", data.OwnerSteamId, data.Items.Count);
			return;
		}

		// Our own report echoed back by the host (restore path) — may arrive
		// before the local body exists (still loading the run); apply once the
		// game has spawned it (TryApplyCharacterRestore). It is a PEER's hand-over,
		// not this client's own run: the host sends a reconnecting player its
		// character BEFORE the WorldJoin instruction that starts the follow, and
		// that restore belongs to exactly the run the follow is starting — so a
		// run-start cancel must never touch it.
		_restore.Queue(data, ownRun: false);
		// The position applies on the body's first frame (TryApplyCharacterRestore)
		// — the gate is NOT reset here: a re-sent restore (the handshake and the
		// InWorld edge both send the saved character) must not reapply the position
		// to the same body (observed live: a 0.5 s double teleport).
		_log.LogInformation("Received character restore ({Items} items).", data.Items.Count);
	}
	/// <summary>Guest side: the host's own 1 Hz snapshot — render its clone's inventory (never applied to the local body).</summary>
	private void OnHostCharacterDataReceived(CharacterDataMsg data)
	{
		_factTable.ApplySnapshot(_session.HostSteamId, data);
		_log.LogDebug("[CloneRender] guest: host char data ({Count} items).", data.Items.Count);
	}

	/// <summary>Render a remote clone's carried state from its owner's character
	/// snapshot — pure display (the renderer's single entry; matching items
	/// stay and their component state refreshes, changed ones swap, the emptied
	/// disappear). Called when a clone appears and when a snapshot updates.</summary>
	internal void ApplyCloneInventory(Body clone, CharacterDataMsg data, ulong ownerSteamId) =>
		_inventoryRenderer.ApplyCloneInventory(clone, data, ownerSteamId);

	/// <summary>Pump: re-report the character snapshot on the 1 Hz interval (only when the body exists).</summary>
	internal void Update(Body? localBody)
	{
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		if (_restore.HasPending || _restore.WipePending)
		{
			return; // restoring: a fresh-run snapshot would overwrite the host's saved character data
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextCharacterReportMs)
		{
			return;
		}

		_nextCharacterReportMs = nowMs + (long)(CharacterReportInterval * 1000f);
		ReportCharacterData(CharacterDataCapture.Capture(_mapper, localBody, out var nativeFailure, _nativeSystem), throttled: true, nativeFailure);
	}

	/// <summary>An inventory-internal move finished (SwapSlots/SwitchHands) — re-report right away (the 1 Hz throttle alone reads as a 1-2 s delay on the peer's clone).</summary>
	internal void ReportInventoryChanged(Body? localBody)
	{
		if (localBody != null && _session.SessionActive && !_restore.HasPending && !_restore.WipePending) // Unity object — ==
		{
			_log.LogInformation("[CloneRender] inventory changed — immediate re-report.");
			ReportCharacterData(CharacterDataCapture.Capture(_mapper, localBody, out var nativeFailure, _nativeSystem), throttled: false, nativeFailure);
		}
	}

	private void ReportCharacterData(CharacterDataMsg data, bool throttled, string? nativeFailure = null)
	{
		if (_session.Role == SessionRole.Host)
		{
			// Host → guests: their clones of the host render its carried items.
			if (throttled)
			{
				_log.LogDebug("[CloneRender] host broadcasting char data ({Count} items).", data.Items.Count);
				LogMissingNativeFields(nativeFailure);
			}

			_characterData.BroadcastHostCharacterData(data);
		}
		else
		{
			if (throttled)
			{
				_log.LogDebug("[CloneRender] guest reporting char data ({Count} items).", data.Items.Count);
				LogMissingNativeFields(nativeFailure);
			}

			_characterData.ReportCharacterData(data);
		}
	}

	/// <summary>
	/// The snapshot this report just built carries no native character fields: say
	/// WHY, because the reason decides what the player loses — a missing camera or
	/// wound window is a live-scene problem, while a body without its happiness
	/// window is a data problem. Only the throttled (1 Hz) reports log it, so a
	/// scene that cannot be read does not flood the log from every inventory move.
	/// </summary>
	private void LogMissingNativeFields(string? nativeFailure)
	{
		if (nativeFailure is not null)
		{
			_log.LogWarning("Character snapshot without native character fields ({Missing}); the snapshot still reconnects the character, but a restore of it keeps the game's defaults for those fields.",
				nativeFailure);
		}
	}

	/// <summary>Capture the LOCAL body's character snapshot right now — the save system's cut needs the state at the instant the host leaves the world, not the last 1 Hz report.</summary>
	internal CharacterDataMsg CaptureLocal(Body body)
	{
		var captured = CharacterDataCapture.Capture(_mapper, body, out var nativeFailure, _nativeSystem);
		if (nativeFailure is not null)
		{
			// The cut's own snapshot: this is the one a later continue restores, so its
			// native-field gap is worth a line at the instant it is taken.
			_log.LogWarning("The cut's character snapshot carries no native character fields ({Missing}); a continue of it keeps the game's defaults for those fields.", nativeFailure);
		}

		return captured;
	}

	/// <summary>Host side: a NEW run started (the host clicked start) — the previous run's saved characters are void (see ICharacterDataControl.ClearSavedCharacters).</summary>
	internal void ClearSavedCharacters() => _characterData.ClearSavedCharacters();

	/// <summary>
	/// Session ended: a pending restore belongs to the dead session and must
	/// never apply to the next lobby's body; the clone fact table is likewise
	/// session-scoped. The position gate resets when the current body leaves
	/// (death/menu), so it is not touched here.
	/// </summary>
	internal void ResetSessionState()
	{
		_restore.Clear();
		_factTable.Clear();
	}

	/// <summary>Leaving the world (death, menu) — push a final snapshot so the host's save carries the state at the moment of leaving, not the last 1 Hz report (a death → re-enter cycle would otherwise restore the pre-death state).</summary>
	internal void NotifyBodyLeft(Body prevBody)
	{
		if (_restore.WipePending)
		{
			// The body that took the restore's FIRST pass is leaving: the second pass can never
			// complete on it, and a later body must not receive HALF a restore (its items without
			// the wipe, or its stats without its items). The respawn path queues its own restore
			// when a respawn is what this is.
			_restore.Clear();
			_log.LogInformation("Dropped a local character restore whose first pass had already run: the body it belonged to left the world.");
		}
		else if (!_restore.HasPending)
		{
			_characterData.ReportCharacterData(CharacterDataCapture.Capture(_mapper, prevBody, out _, _nativeSystem));
		}

		// The body left the world — the next body's restore position applies
		// again (the gate reset lives HERE, not on restore arrival).
		_positionGate.OnBodyLeft();
	}

	private void TryApplyCharacterRestore(Body body)
	{
		if (_restore.Pending is not { } pending)
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

		if (_restore.WipePending)
		{
			// Second pass (next frame): the wipe's Destroy ran at the end of
			// the previous frame, so the slots are actually empty now and
			// PickUpItem succeeds — it silently refuses a non-empty slot
			// (Body.cs:1388), which stranded the restored items on the ground.
			_restoreApplier.ApplyItems(body, pending);

			// The native character fields are the LAST step, and they have to be: a
			// snapshot that carries none of them is reported here — by name — because
			// the game skips its own fresh roll when a run is continued
			// (PlayerCamera.cs:726-729), so those fields would otherwise stay at the
			// defaults the fresh body was created with and the player would never be
			// told (§6/§6.1).
			ReportNativeFields(pending, _restoreApplier.ApplyNativeFields(body, pending));

			_restore.Clear();
			return;
		}

		_restoreApplier.ApplyStatsAndWipe(body, pending);
		_restore.MarkWipePending();
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
			_log.LogInformation(
				"Character restore carries no native character fields (lastHappiness, caloriesConsumed, WoundView.cInfo): this body keeps the live defaults for them.");
		}

		foreach (var write in writes.Where(write => !write.Applied))
		{
			_log.LogWarning("Character restore did not write {Field}: {Refusal}.", write.Description, write.Refusal);
		}
	}

	/// <summary>Pump entry used by the run coordinator: applies a pending host restore once the local body exists.</summary>
	internal void UpdateRestore(Body localBody)
	{
		if (_restore.HasPending)
		{
			TryApplyCharacterRestore(localBody);
		}
	}

	/// <summary>
	/// Queue a full restore for the LOCAL body from a run THIS client owns: its own CUO
	/// continue, or its own next-level auto-respawn. Both use the same two-frame
	/// wipe/restore path as a guest reconnect restore, so a role never gets a different
	/// flavour of "my character came back". The caller prepares <paramref name="data"/>
	/// with <c>Position = null</c> when the body must stay where the world placed it;
	/// this method deliberately does not reset the position gate (that only resets when
	/// the body leaves the world), so an in-world body is never teleported by this queue.
	///
	/// Queue it BEFORE the local body exists: while a restore is queued, <see cref="Update"/>
	/// suppresses the 1 Hz report, which is what keeps the fresh body's live snapshot
	/// from overwriting the character being restored. Solo play has no session, so this
	/// is deliberately not session-gated — the callers own that decision.
	/// </summary>
	internal void QueueLocalRestore(CharacterDataMsg data)
	{
		_restore.Queue(data, ownRun: true);
		_log.LogInformation("Queued this run's local character restore ({Items} items, position {Position}).",
			data.Items.Count, data.Position is { } pos ? $"({pos.X:F1},{pos.Y:F1})" : "<none>");
	}

	/// <summary>
	/// A run THIS client starts on its own (its start click, its own continue) is taking over:
	/// nothing that waited here can belong to it, so everything goes — including a peer's
	/// hand-over this client never followed. A silent cancel is what would make a lost restore
	/// look like a working one, so the drop is logged.
	/// </summary>
	internal void CancelAllLocalRestores()
	{
		if (!_restore.CancelAll())
		{
			return;
		}

		_log.LogInformation("Dropped the character restore waiting from an earlier run: this client is starting its own run.");
	}

	/// <summary>
	/// This client is FOLLOWING a run someone else announced: the restore its OWN run queued
	/// cannot reach a body any more and goes; a PEER's hand-over is exactly the restore this
	/// follow is for and stays (the host sends it before the announce, on the same reliable
	/// ordered channel). See <see cref="LocalCharacterRestoreQueue"/>.
	/// </summary>
	internal void CancelOwnRunRestore()
	{
		if (!_restore.CancelOwnRun())
		{
			return;
		}

		_log.LogInformation("Cancelled the character restore queued by this client's own run: that run never reached a body.");
	}

	/// <summary>
	/// Apply only the pending restore's position — the run coordinator calls this
	/// on the body's first frame, BEFORE reporting the scene state, so the host
	/// spawns the clone at the restored spot (ReportedSpawnPos) instead of the
	/// landing spot and then teleporting it (observed: the host saw the
	/// reconnecting guest's clone jump). The stats wipe + items stay on their own
	/// two-frame rhythm (TryApplyCharacterRestore); the position gate keeps this
	/// idempotent.
	/// </summary>
	internal void ApplyPendingPositionOnly(Body body)
	{
		if (_restore.Pending is { } pending)
		{
			ApplyPendingPosition(body, pending);
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
		body.transform.position = new Vector3(pos.X, pos.Y, 0f);
		body.rb.velocity = Vector2.zero;
		_log.LogInformation("Character restore position applied at spawn: ({X:F1},{Y:F1}).", pos.X, pos.Y);
		return true;
	}

	/// <summary>
	/// Store an authoritative medical-operation progress/terminal snapshot in
	/// the fact table (not the local body) so the remote WoundView and clone
	/// renderer read the live state instead of the last 1 Hz snapshot.
	/// </summary>
	internal void ApplyMedicalState(ulong owner, CharacterHealthMsg? health, IReadOnlyList<CharacterLimbMsg>? limbs) =>
		_factTable.ApplyMedicalState(owner, health, limbs);

	/// <summary>
	/// Apply a host-authoritative cross-player heal result to the LOCAL body:
	/// map the post-heal body health and limb state directly, without touching
	/// inventory or running the restore wipe. Called inside a RemoteApply scope;
	/// the caller re-reports the full character snapshot immediately afterwards.
	/// </summary>
	internal void ApplyHealState(Body body, CharacterHealthMsg health, IReadOnlyList<CharacterLimbMsg> limbs)
	{
		_mapper.Map(health, body);
		CharacterComponentSync.Apply(body, health);
		foreach (var limbData in limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= body.limbs.Length)
			{
				continue;
			}

			_mapper.Map(limbData, body.limbs[limbData.Index]);
			LimbComponentStateCodec.Apply(body.limbs[limbData.Index], limbData.Components);
		}
	}

	/// <summary>
	/// Restore one worn item onto the local body — the same write the restore's
	/// second pass performs, exposed for a cross-player interaction that hands the
	/// local player a wearable. The implementation lives in
	/// <see cref="WearableRestorer"/>, which both callers share.
	/// </summary>
	internal void RestoreWearable(CharacterItemMsg itemData, Body body) => _wearables.RestoreWearable(itemData, body);
}
