using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Remote-player rendering: the per-member render clones (physics off,
/// animations on, fed by the state stream) and the local body's state
/// publishing (the stream's source side). NO remote-side simulation anywhere —
/// each player simulates only its own body. Reads the character-data domain's
/// snapshot cache for the clones' carried-item rendering. What the carry
/// relation does to those clones — the local-carrier mount, the per-frame pin
/// and its read-only drift reading — belongs to
/// <see cref="CarriedRiderPresenter"/>.
/// </summary>
internal sealed class RemotePlayerRenderer(
	ISessionControl session,
	IEntitySyncControl entities,
	CharacterDataSync characterData,
	CloneLimbRenderer limbRenderer,
	IPlayerInteractionControl playerInteraction,
	ILogger<RemotePlayerRenderer> log)
{
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly CloneLimbRenderer _limbRenderer = limbRenderer;
	private readonly IPlayerInteractionControl _playerInteraction = playerInteraction;
	private readonly ILogger<RemotePlayerRenderer> _log = log;
	private readonly CarriedRiderPresenter _carriedRider = new(session, playerInteraction);

	private readonly Dictionary<ulong, Body> _remoteClones = [];
	private long _nextCloneLogMs;

	/// <summary>
	/// The repetition window every clone-creation failure asks before it writes its line. The ensure below
	/// retries once per member PER FRAME, and a member out of the world never resolves the failure, so
	/// without this a three-client session fills a rolling log at the rate batch `20261007-a` measured:
	/// 58,148 identical lines / 8.796 MB in ~90 s on one client, still climbing at close. Three lines per
	/// (member, failure) plus one summary per run that ends, and the capacity covers 32 of those subjects
	/// (two per member) generously: past it the OLDEST is evicted, and an ask order cycling through more live
	/// subjects than the window holds would evict the ones its own frame is about to ask.
	/// </summary>
	private readonly LogRepetitionGuard _cloneFailures = new(suppressAfter: 3, capacity: 64);

	/// <summary>
	/// The clone diagnostic's message template, shared by its Information
	/// form (a carry participant) and its routine Debug form, so the two can
	/// never drift apart.
	/// </summary>
	private const string CloneLine = "Clone {SteamId}: at ({PX:F1}, {PY:F1}), reported ({RX:F1}, {RY:F1}), active {Active}{CarryTag}";

	/// <summary>
	/// The summary of a clone-creation failure run that ended, written where the member starts drawing a
	/// clone again (or leaves, or the session does): the window's own lines are the diagnostic and this is
	/// its size, so the bound never makes a standing failure's cost untold.
	/// </summary>
	private const string CloneFailureSuppressedLine = "Remote body: {Why} for {SteamId} — {Count} identical line(s) suppressed while the clone could not be built.";

	internal void BindToSession()
	{
		_entities.RemoteJoined += OnRemoteJoined;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_characterData.CloneSnapshotUpdated += OnCloneSnapshotUpdated;
	}

	internal void Unbind()
	{
		_entities.RemoteJoined -= OnRemoteJoined;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_characterData.CloneSnapshotUpdated -= OnCloneSnapshotUpdated;
	}

	/// <summary>
	/// A clone's snapshot cache updated — re-render its carried items (the
	/// clone only renders at creation otherwise; a snapshot update never
	/// touched it, so carried-item changes arrived but never showed).
	/// </summary>
	private void OnCloneSnapshotUpdated(ulong steamId)
	{
		// == null on Unity clones — a scene reload destroys the clone and
		// reference-comparison would miss it (the lazy ensure rebuilds it).
		if (_remoteClones.TryGetValue(steamId, out var clone) && clone != null
			&& _characterData.CloneData.TryGetValue(steamId, out var data))
		{
			_characterData.ApplyCloneInventory(clone, data, steamId);
			_limbRenderer.ApplyCloneLimbs(clone, data);
			RemoteCharacterDisplayProjection.ApplyRenderClone(clone, RemoteCharacterPresentation.State.From(data));
		}
	}

	/// <summary>
	/// The live render clone for one remote member, or null. Unity object — the
	/// caller must use == null (a scene reload destroys the clone and a managed
	/// reference would miss it). The enemy combat director uses the clone to
	/// resolve which limb a host-ordered attack should hit.
	/// </summary>
	internal bool TryGetRemoteBody(ulong steamId, out Body body)
	{
		if (_remoteClones.TryGetValue(steamId, out var clone) && clone != null)
		{
			body = clone;
			return true;
		}

		body = null!;
		return false;
	}

	/// <summary>
	/// Gets the current world-space head position of one remote render clone
	/// (Body.limbs[0] is the head — see NetBody.GetHeadPos). Used by the Online
	/// UI so nameplates and off-screen arrows point at the visible head rather
	/// than the body-root/center.
	/// </summary>
	internal bool TryGetRemoteHeadPosition(ulong steamId, out Vector2 headPosition)
	{
		if (_remoteClones.TryGetValue(steamId, out var clone) && clone != null // Unity object — ==
			&& clone.limbs.Length > 0
			&& clone.limbs[0] != null) // Unity object — ==
		{
			headPosition = clone.limbs[0].transform.position;
			return true;
		}

		headPosition = default;
		return false;
	}

	/// <summary>Session/entity ended — destroy every render clone.</summary>
	internal void DestroyAllClones()
	{
		// The runs that outlive the session report their size before the window is
		// forgotten: this is the storm's own case (its condition never resolved).
		FlushCloneFailures();

		// == null on the Unity clones (is null would miss scene-reload-destroyed objects).
		foreach (var clone in _remoteClones.Values)
		{
			if (clone != null)
			{
				Object.Destroy(clone.transform.parent.gameObject);
			}
		}

		_remoteClones.Clear();
	}

	/// <summary>Pump: lazy per-member clone ensure + state application + local-carrier follow + 1 Hz diagnostics.</summary>
	internal void Update(Body? localBody)
	{
		// Lazy per-member ensure: a roster join can arrive before the member's
		// world exists (the menu scene has no "Experiment" template), and members
		// can join mid-session — retrying every frame absorbs all ordering races.
		var remotePlayers = _entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
			if (!_session.IsRemoteInWorld(remote.SteamId))
			{
				continue; // in a menu/loading — no clone
			}

			// == null on Unity objects — a scene reload destroys the clone and
			// reference-comparison would miss it; retry creation next frame.
			if (!_remoteClones.TryGetValue(remote.SteamId, out var clone) || clone == null)
			{
				clone = RemoteBodyFactory.CreateRemoteBody(remote, AnchorFor(remote), _cloneFailures, _log);
				if (clone == null)
				{
					continue; // template unavailable — retry next frame
				}

				_remoteClones[remote.SteamId] = clone;
				EndCloneFailureRuns(remote.SteamId); // the member builds a clone again: its failure run is over
				_log.LogInformation("Remote body created for {SteamId}.", remote.SteamId);
				// Render its carried items + limb presentation from the latest
				// snapshot (a fresh report follows within 1 s at the latest).
				if (_characterData.CloneData.TryGetValue(remote.SteamId, out var data))
				{
					_characterData.ApplyCloneInventory(clone, data, remote.SteamId);
					_limbRenderer.ApplyCloneLimbs(clone, data);
					RemoteCharacterDisplayProjection.ApplyRenderClone(clone, RemoteCharacterPresentation.State.From(data));
				}
			}

			// Mark the clone's carry role BEFORE applying stream state, so
			// SessionStatePump can suppress the native sit replay in the same
			// frame — this is not limited to the local carrier's view, because a
			// third-party rider clone also rides and the second-pass attach below
			// forces its visible position anyway; a carrier participates in the
			// same whole-family sit suppression. Then take the drift reading: the
			// frame that rendered is still in these transforms, since the state
			// write below overwrites the clone and the attach pass re-pins a
			// carried rider only after that, so reading the pin's own placement
			// HERE is the one point where "where was the rider drawn" can be
			// compared with "where the carry pin asked for it".
			clone.TryGetComponent<RemoteBodyDriver>(out var cloneDriver);
			_carriedRider.MarkCarryRole(remote.SteamId, cloneDriver);
			_carriedRider.MeasurePinDrift(localBody, remote.SteamId, clone, cloneDriver, _remoteClones);

			SessionStatePump.Apply(remote, clone);
		}

		// Second pass: after every clone has been placed by SessionStatePump,
		// pin every carried rider clone to its carrier's VISUAL position. This
		// covers the local-carrier view and every third-party view alike, so
		// independent per-clone interpolation can never make the pair appear
		// detached. The rider's own local body uses the same rule later in
		// GameAdapter.Update.
		_carriedRider.AttachAll(localBody, _remoteClones);

		LogClonePosition();
	}

	/// <summary>
	/// Re-runs the rider-clone attach pass after the local carrier's native
	/// Body.Update completed. CUO's own update pump may run before the game
	/// moves the local carrier in the same frame; without this second pass the
	/// carrier's own view can pin the rider to the previous-frame transform and
	/// show a one-frame lag/teleport while moving.
	/// </summary>
	internal void RefreshLocalCarrierAttach(Body? localBody)
	{
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		_carriedRider.AttachAll(localBody, _remoteClones);
	}

	private Vector2 AnchorFor(PlayerEntity remote) =>
		_session.Role == SessionRole.Host
			? new Vector2(_session.GetRemoteSpawnPos(remote.SteamId).X, _session.GetRemoteSpawnPos(remote.SteamId).Y)
			: new Vector2(remote.Position.X, remote.Position.Y);

	/// <summary>
	/// One member's clone-creation failure runs all ended — its clone is built again, or it left the world
	/// and no attempt is made for it any more: each subject reports what its window swallowed, so the bound
	/// never makes a standing failure's size untold. Silent for a subject that cost nothing, and a failure
	/// that comes back after this reports its first line again.
	/// </summary>
	private void EndCloneFailureRuns(ulong steamId)
	{
		ReportCloneFailureRun(new(RemoteBodyFactory.NoTemplateWhy, steamId));
		ReportCloneFailureRun(new(RemoteBodyFactory.NoBodyComponentWhy, steamId));
	}

	/// <summary>
	/// The session is over with failure runs still open, which is the storm's own case (its condition never
	/// resolved before the client closed): report every subject the window is still holding.
	/// </summary>
	private void FlushCloneFailures()
	{
		// A copy of the tracked subjects: TryFlush drops each one as it reports it.
		var subjects = _cloneFailures.Subjects;
		for (var i = 0; i < subjects.Count; i++)
		{
			if (subjects[i] is RemoteCloneFailureKey subject)
			{
				ReportCloneFailureRun(subject);
			}
		}
	}

	/// <summary>How many identical lines this subject's window refused — the run's own size, once.</summary>
	private void ReportCloneFailureRun(RemoteCloneFailureKey subject)
	{
		if (_cloneFailures.TryFlush(subject, out var suppressed))
		{
			_log.LogWarning(CloneFailureSuppressedLine, subject.Why, subject.SteamId, suppressed);
		}
	}

	/// <summary>Periodic clone diagnostics (1 Hz) — where the remote proxies actually are.</summary>
	private void LogClonePosition()
	{
		var nowMs = Environment.TickCount;
		if (nowMs < _nextCloneLogMs)
		{
			return;
		}

		_nextCloneLogMs = nowMs + 1000;
		if (_remoteClones.Count == 0)
		{
			return;
		}

		// KeyValuePair has no Deconstruct on net48 — iterate entries explicitly.
		foreach (var entry in _remoteClones)
		{
			var steamId = entry.Key;
			var clone = entry.Value;
			// == null on the Unity clone: a scene reload destroys it and
			// reference-comparison (?.) would throw on access.
			var pos = clone != null ? clone.transform.position : Vector3.zero;
			var remote = _entities.GetRemotePlayer(steamId);
			var reported = remote is not null
				? new Vector2(remote.Position.X, remote.Position.Y)
				: Vector2.zero;
			var isRiderClone = _playerInteraction.TryGetCarried(_session.LocalSteamId, out var carriedId)
				&& carriedId == steamId;
			var isCarrierClone = _playerInteraction.TryGetCarrier(_session.LocalSteamId, out var carrierId)
				&& carrierId == steamId;
			var isMountedToLocalCarrier = _carriedRider.IsMountedToLocalCarrier(clone);
			var carryTag = isRiderClone ? ", carried-rider-clone" : isCarrierClone ? ", carrier-clone" : "";
			if (isMountedToLocalCarrier)
			{
				carryTag += ", mounted-to-local-carrier";
			}

			// The carry readings of this window — whether a pin was in force at
			// all, the limb separation and the pin drift — printed for every clone
			// they apply to with zero included, plus the anomaly reports that must
			// not wait for a raised log level. Read and reset here, so "measured
			// zero" and "not measured" cannot be confused.
			var poseDriver = clone != null ? clone.GetComponent<RemoteBodyDriver>() : null;
			var pinnedInWindow = false;
			if (poseDriver != null) // Unity object — ==
			{
				pinnedInWindow = poseDriver.PinCountInWindow > 0;
				carryTag += CarryPresentationProbe.Describe(poseDriver);
				CarryPresentationProbe.Report(_log, steamId, poseDriver);
				poseDriver.LimbSeparationWindowMax = 0f;
				poseDriver.PinDriftWindowMax = 0f;
				poseDriver.PinCountInWindow = 0;
			}

			// A clone that participates in a carry relation is the one family
			// whose presentation is under an open defect, so its 1 Hz line is
			// INFORMATION: a default session must be able to answer "was the
			// rider pinned, and did the readings stay zero" without raising
			// Logging.MinimumLevel, and a Debug firehose is not a representative
			// frame to judge a motion artifact in. Every other clone stays on the
			// routine Debug position line. Who counts as a participant is a
			// Runtime fact, so the decision is testable without a game.
			var atDefaultLevel = CarryPresentationReading.IsCarryParticipant(
				isLocalRiderClone: isRiderClone,
				isLocalCarrierClone: isCarrierClone,
				isRemoteRider: poseDriver != null && poseDriver.IsCarriedRider,
				isRemoteCarrier: poseDriver != null && poseDriver.IsCarrier,
				pinnedInWindow: pinnedInWindow);
			LogCloneLine(atDefaultLevel, steamId, pos, reported, clone, carryTag);
		}
	}

	/// <summary>
	/// Writes one clone's 1 Hz diagnostic: an Information line for a carry
	/// participant, the routine Debug line for every other clone. One template
	/// for both, so the level is the only difference between them.
	/// </summary>
	private void LogCloneLine(bool atDefaultLevel, ulong steamId, Vector3 pos, Vector2 reported, Body? clone, string carryTag)
	{
		var active = clone != null && clone.gameObject.activeInHierarchy; // Unity object — ==
		if (atDefaultLevel)
		{
			_log.LogInformation(CloneLine, steamId, pos.x, pos.y, reported.x, reported.y, active, carryTag);
		}
		else
		{
			_log.LogDebug(CloneLine, steamId, pos.x, pos.y, reported.x, reported.y, active, carryTag);
		}
	}

	private void OnRemoteJoined(PlayerEntity remote) =>
		// Clone creation is handled by the per-frame lazy ensure in Update —
		// the roster join can arrive before the member's world exists (the menu
		// scene has no "Experiment" template), so event-driven creation would
		// race. Log only; the pump creates and the anchor for host/guest differs.
		_log.LogInformation("Remote joined (clone ensured by the Update pump): {SteamId}.", remote.SteamId);

	/// <summary>
	/// A member's in-world state flipped: leave → destroy its render clone (it
	/// carries no state; the Update pump rebuilds it on re-entry). The host
	/// leaving the world also ends the world itself — the run coordinator
	/// handles pulling a guest back to the menu.
	/// </summary>
	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (!inWorld)
		{
			// No clone is ensured for a member out of the world, so its failure runs
			// end here — and with them their windows, so a failure that comes back
			// when it re-enters reports its first line instead of being refused.
			EndCloneFailureRuns(steamId);
		}

		if (!inWorld && _remoteClones.TryGetValue(steamId, out var clone) && clone != null) // Unity object — ==
		{
			Object.Destroy(clone.transform.parent.gameObject);
			_remoteClones.Remove(steamId);
		}

		_log.LogInformation(inWorld
			? "Remote entered the world — clone rebuilt on rejoin."
			: "Remote not in world (menu or disconnected) — clone destroyed.");
	}
}
