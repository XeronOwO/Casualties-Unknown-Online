using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Run;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.World;
using MapsterMapper;
using Microsoft.Extensions.Logging;

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
	IWorldControl world,
	ICharacterDataControl characterData,
	IMapper mapper,
	CloneInventoryRenderer inventoryRenderer,
	CloneFactTable factTable,
	CharacterRestoreApplier restoreApplier,
	WearableRestorer wearables,
	ICharacterNativeSystem nativeSystem,
	LocalCharacterRestoreQueue restore,
	HealResetReportRetry healResetReports,
	DescentBodyPreservation descentBody,
	ILogger<CharacterDataSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly IMapper _mapper = mapper;
	private readonly CloneInventoryRenderer _inventoryRenderer = inventoryRenderer;
	private readonly CloneFactTable _factTable = factTable;
	private readonly ILogger<CharacterDataSync> _log = log;

	/// <summary>The descent follow's one-shot handback fact, shared with the run coordinator that notes the body.</summary>
	private readonly DescentBodyPreservation _descentBody = descentBody;

	/// <summary>The local body the 1 Hz pump last saw — the handback guard compares it to the descent's preserved body.</summary>
	private int _localBodyId;

	/// <summary>The reset result's retry (the transport can suppress the one event that carries the host-owned values).</summary>
	private readonly HealResetReportRetry _healResetReports = healResetReports;

	/// <summary>The local favourite marks this client's own character record carries (its own responsibility, split out when this coordinator reached the architecture line's watchlist).</summary>
	private readonly LocalFavouriteMarkSync _favouriteMarks = new(mapper, nativeSystem, log);

	/// <summary>A clone's snapshot cache updated (SteamId) — the renderer re-renders that clone's carried items. Without this, the clone only rendered once at creation ("after the starting supplies, the peer never sees carried-item updates").</summary>
	public event Action<ulong>? CloneSnapshotUpdated
	{
		add => _factTable.CloneSnapshotUpdated += value;
		remove => _factTable.CloneSnapshotUpdated -= value;
	}

	/// <summary>
	/// The restore's WHEN: the queue, the position gate, the two-frame rhythm, the
	/// native-field report and the wearable forward. The queue is injected rather than
	/// owned — it is also the world's answer to "does this world have a character for
	/// me?", which the starting-supplies decision reads at the moment a body appears
	/// (<see cref="StartingSupplyCoordinator"/>). One queue, two readers — a second
	/// instance would let the two disagree about whether this player is new, which is
	/// exactly the confusion a grant must not have. Built on first use: it needs the
	/// favourite-mark sync this class creates for itself.
	/// </summary>
	private LocalCharacterRestore? _restoreRunner;

	private LocalCharacterRestore RestoreRunner =>
		_restoreRunner ??= new(_world, restoreApplier, wearables, _favouriteMarks, restore, _log);
	private const float CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)

	/// <summary>The owner-state event: the limb latch, the injury rise, and the payload both send. Built on first use</summary>
	/// <remarks>because it needs this object's own capture helpers, which the 1 Hz report and the heal reset share — the same capture, so the event cannot carry a
	/// field the report drops.</remarks>
	private OwnerStateEventReporter? _ownerState;

	internal void ReportLimbStateEvent(Limb limb) => OwnerState.ReportLimbStateEvent(limb);

	/// <summary>
	/// Report the local owner's whole body state after a console verb wrote it — the same owner-state
	/// event the limb latch uses, so the host and every peer converge on the change without a second
	/// channel. The <c>/kill</c> cheat verb writes one field (brain health to zero) and calls this.
	/// </summary>
	internal void ReportBodyStateEvent(Body body, string reason) => OwnerState.ReportOwnerStateEvent(body, reason);

	private OwnerStateEventReporter OwnerState =>
		_ownerState ??= new(_session, _characterData, CaptureHealth, CaptureLimbs, _log);

	private long _nextCharacterReportMs;

	/// <summary>Read-only view for the clone renderer: latest snapshot per SteamId.</summary>
	internal IReadOnlyDictionary<ulong, CharacterDataMsg> CloneData => _factTable.CloneData;

	/// <summary>
	/// True = a character snapshot is waiting for (or is half-way onto) the local body.
	/// A queued restore IS this player's character, so a body that has one must not be
	/// supplied with starting supplies on top of it: the restore's own first pass wipes
	/// the slots a frame later, so an extra grant would be created, announced and then
	/// destroyed.
	/// </summary>
	internal bool RestorePending => RestoreRunner.RestorePending;

	/// <summary>Carried-fact event (the owner's fact-table entry updates and the clone re-renders immediately) — the fact table lives in CloneFactTable.</summary>
	internal void ApplyCarriedSync(ulong owner, CharacterItemMsg item, bool slotKnown) => _factTable.ApplyCarriedSync(owner, item, slotKnown);

	/// <summary>The owner's starting supplies with self-assigned ids arrived — merged into its fact table (clone render + snapshot divergence baseline).</summary>
	internal void ApplyCarriedInventory(ulong owner, IReadOnlyList<CharacterItemMsg> items) => _factTable.ApplyCarriedInventory(owner, items);

	/// <summary>A carried item left into the world — it leaves the owner's fact table (top-level or nested in a container's contents).</summary>
	internal void RemoveCarriedItem(ulong itemId) => _factTable.RemoveCarriedItem(itemId);

	/// <summary>An operation's report destroyed one owner's carried row (a craft's Destroyed entry) — retire it from THAT owner's fact table before the next snapshot announces the disappearance.</summary>
	internal void RetireCarriedItem(ulong owner, ulong itemId) => _factTable.RetireCarriedItem(owner, itemId);

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
		_characterData.PlayerVitalsReceived += _vitals.Apply;
	}

	internal void Unbind()
	{
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived -= OnHostCharacterDataReceived;
		_characterData.LimbStateEventReceived -= OnLimbStateEventReceived;
		_characterData.PlayerVitalsReceived -= _vitals.Apply;
	}

	/// <summary>A limb-latch event arrived (report or relay) — update the owner's fact-table entry and re-render its clone (the dedicated event is the trigger; the 1 Hz snapshot is the fallback).</summary>
	private void OnLimbStateEventReceived(ulong sender, LimbStateEventMsg msg) =>
		_factTable.ApplyLimbStateEvent(msg.OwnerSteamId, msg);


	/// <summary>
	/// Capture the LOCAL body's health message — the same capture the character snapshot, the limb
	/// event and the heal reset use, so the presentation/component fields Mapster cannot see are
	/// filled in exactly one place.
	/// </summary>
	internal CharacterHealthMsg CaptureHealth(Body body)
	{
		var health = _mapper.Map<CharacterHealthMsg>(body);
		// Unified capture: face latches/mouth, component state and the
		// leg-speed pose multiplier now ride the same dedicated event as the
		// 1 Hz snapshot, so a limb-latch update also refreshes the full remote
		// presentation without waiting for the next snapshot.
		RemoteCharacterDisplayProjection.Capture(body, health);
		return health;
	}

	/// <summary>Capture every limb as a wire row. The record itself is <see cref="CharacterDataCapture.CaptureLimbs"/>'s — the one capture the 1 Hz report, the limb event and the heal reset share, so none of them can drop a field the others carry.</summary>
	internal IReadOnlyList<CharacterLimbMsg> CaptureLimbs(Body body) => CharacterDataCapture.CaptureLimbs(_mapper, body);

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
		if (_descentBody.TryConsume(_localBodyId, data))
		{
			// This body never left the world: it came through the host's own layer descent, which kept
			// it and its carried items. The handback is for a member that left — applying it would move
			// the body to old coordinates and wipe/reapply the items it already holds. The consumption is
			// one-shot, and a respawn handback (the host's next-level revive) is never consumed.
			_log.LogInformation("[LayerDescent] dropped the saved-character handback: this body came through the descent and never left.");
			return;
		}

		RestoreRunner.QueuePeerHandover(data);
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
		// The applied reset's authority event retries on this cadence while this body exists: it is
		// the only carrier of the three host-owned values, and the transport may not have accepted it.
		_healResetReports.Tick();
		_localBodyId = localBody != null ? localBody.GetInstanceID() : 0; // Unity object — ==

		if (localBody == null) // Unity object — ==
		{
			return;
		}

		// The host-computed vitals row arrives outside the pump, so the pump hands the local
		// body over once per frame — the row can then be applied the moment it lands instead
		// of waiting for the next frame's capture.
		_vitals.TrackLocalBody(localBody);

		if (RestoreRunner.RestorePending)
		{
			// Restoring: a fresh-run snapshot would overwrite the host's saved character data. The
			// injury baseline is dropped too — the restore writes limb state, and a rise it causes is
			// not an owner-judged wound.
			OwnerState.ResetInjury();
			return;
		}

		if (OwnerState.TrackInjury(localBody, _vitals.AppliedCount))
		{
			OwnerState.ReportOwnerStateEvent(localBody, "injury");
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextCharacterReportMs)
		{
			return;
		}

		_nextCharacterReportMs = nowMs + (long)(CharacterReportInterval * 1000f);
		Reports.Send(_favouriteMarks.Capture(localBody, out var nativeFailure), throttled: true, nativeFailure);
	}


	/// <summary>An inventory-internal move finished (SwapSlots/SwitchHands) — re-report right away (the 1 Hz throttle alone reads as a 1-2 s delay on the peer's clone).</summary>
	internal void ReportInventoryChanged(Body? localBody)
	{
		if (localBody != null && _session.SessionActive && !RestoreRunner.RestorePending) // Unity object — ==
		{
			_log.LogInformation("[CloneRender] local body changed — immediate re-report.");
			Reports.Send(_favouriteMarks.Capture(localBody, out var nativeFailure), throttled: false, nativeFailure);
		}
	}

	/// <summary>Capture the LOCAL body's character snapshot right now — the save system's cut needs the state at the instant the host leaves the world, not the last 1 Hz report.</summary>
	internal CharacterDataMsg CaptureLocal(Body body)
	{
		var captured = Reports.Stamp(_favouriteMarks.Capture(body, out var nativeFailure));
		if (nativeFailure is not null)
		{
			// The cut's own snapshot: this is the one a later continue restores, so its
			// native-field gap is worth a line at the instant it is taken.
			_log.LogWarning("The cut's character snapshot carries no native character fields ({Missing}); a continue of it keeps the game's defaults for those fields.", nativeFailure);
		}

		return captured;
	}

	/// <summary>Host side: a NEW run started (the host clicked start) — the previous run's saved characters are void (see ICharacterDataControl.ClearSavedCharacters), and so are the local favourite marks they carried: the marks are world-scoped, and the next world's character record is what seeds the next set.</summary>
	internal void ClearSavedCharacters()
	{
		_characterData.ClearSavedCharacters();
		_favouriteMarks.Reset();
	}

	/// <summary>
	/// Session ended: a pending restore belongs to the dead session and must
	/// never apply to the next lobby's body; the clone fact table is likewise
	/// session-scoped. The position gate resets when the current body leaves
	/// (death/menu), so it is not touched here. The local favourite marks go with
	/// the session for the same reason: they are that world's, and the next world
	/// hands this player its own character record back.
	/// </summary>
	internal void ResetSessionState()
	{
		_healResetReports.Clear();
		RestoreRunner.Clear();
		_factTable.Clear();
		_favouriteMarks.Reset();
	}

	/// <summary>Leaving the world (death, menu) — push a final snapshot so the host's save carries the state at the moment of leaving, not the last 1 Hz report (a death → re-enter cycle would otherwise restore the pre-death state).</summary>
	internal void NotifyBodyLeft(Body prevBody)
	{
		// The result belongs to the body that left: re-offering it on a later body would write a
		// stale health/limb set onto the new one.
		_healResetReports.Clear();
		_localBodyId = 0; // the descent's preserved-body compare must not survive the body itself

		// A body that took the restore's first pass is leaving: the second pass can never complete
		// on it, and a later body must not receive HALF a restore. When nothing was dropped and
		// nothing is waiting, the body's own final state is what the host needs.
		if (!RestoreRunner.DropHalfRunRestore() && !RestoreRunner.RestorePending)
		{
			Reports.Send(_favouriteMarks.Capture(prevBody, out _), throttled: false);
		}

		RestoreRunner.OnBodyLeft();
	}

	/// <summary>Pump entry used by the run coordinator: applies a pending host restore once the local body exists (the rhythm lives in <see cref="LocalCharacterRestore"/>).</summary>
	internal void UpdateRestore(Body localBody) => RestoreRunner.UpdateRestore(localBody);

	/// <summary>Queue a full restore for the LOCAL body from a run THIS client owns (its continue or its own respawn); the queue, the position gate and the two-frame rhythm live in <see cref="LocalCharacterRestore.QueueOwnRun"/>.</summary>
	internal void QueueLocalRestore(CharacterDataMsg data) => RestoreRunner.QueueOwnRun(data);

	/// <summary>A run THIS client starts on its own is taking over: everything queued from an earlier one goes (see <see cref="LocalCharacterRestore.CancelAllLocalRestores"/>).</summary>
	internal void CancelAllLocalRestores() => RestoreRunner.CancelAllLocalRestores();

	/// <summary>This client follows a run someone else announced: only the restore its OWN run queued goes (see <see cref="LocalCharacterRestore.CancelOwnRunRestore"/>).</summary>
	internal void CancelOwnRunRestore() => RestoreRunner.CancelOwnRunRestore();

	/// <summary>Apply only the pending restore's position — the body's first frame, before the scene report (see <see cref="LocalCharacterRestore.ApplyPendingPositionOnly"/>).</summary>
	internal void ApplyPendingPositionOnly(Body body) => RestoreRunner.ApplyPendingPositionOnly(body);

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
			LimbComponentStateCodec.ApplyAuthoritative(body.limbs[limbData.Index], limbData.Components);
		}
	}

	/// <summary>
	/// Restore one worn item onto the local body — the same write the restore's
	/// second pass performs, exposed for a cross-player interaction that hands the
	/// local player a wearable. The implementation lives in
	/// <see cref="WearableRestorer"/>, which both callers share.
	/// </summary>
	internal void RestoreWearable(CharacterItemMsg itemData, Body body) => RestoreRunner.RestoreWearable(itemData, body);

	/// <summary>The host-computed vitals row's apply target (its own logic lives in <see cref="LocalVitalsApplier"/>).</summary>
	private readonly LocalVitalsApplier _vitals = new(session);

	/// <summary>The wire half of a snapshot (report on a guest, broadcast on the host, stamped with the layer it belongs to). Built on first use: it needs the same world and session this sync holds.</summary>
	private CharacterReportSender Reports =>
		_reports ??= new(_session, _world, _characterData, _log);

	private CharacterReportSender? _reports;
}
