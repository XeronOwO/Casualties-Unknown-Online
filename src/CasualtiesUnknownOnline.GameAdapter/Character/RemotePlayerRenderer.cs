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
	/// The 1 Hz clone diagnostic's message template, shared by its Information
	/// form (a carry participant) and its routine Debug form, so the two can
	/// never drift apart.
	/// </summary>
	private const string CloneLine = "Clone {SteamId}: at ({PX:F1}, {PY:F1}), reported ({RX:F1}, {RY:F1}), active {Active}{CarryTag}";

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

	/// <summary>Pump: lazy per-member clone ensure, then the frame's READ-ONLY carry pass (role marks + drift readings, before any clone is written), then the state writes, the local-carrier follow and the 1 Hz diagnostics.</summary>
	internal void Update(Body? localBody)
	{
		// READ pass, taken before the frame's first STATE WRITE (SessionStatePump.Apply): ensure each member's
		// clone (a roster join can arrive before the member's world exists — the
		// menu scene has no "Experiment" template — and members can join
		// mid-session, so retrying every frame absorbs all ordering races), mark its
		// carry role, and take its drift reading. A drift anchor is ANOTHER clone's
		// transform (the carrier's), so every reading must be taken before the
		// frame's first state write: inside one per-clone loop a rider would be
		// measured against a carrier this same frame had already moved, which reads
		// as drift for a pair that never separated.
		var remotePlayers = _entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
			if (!_session.IsRemoteInWorld(remote.SteamId))
			{
				continue; // in a menu/loading — no clone
			}

			var clone = EnsureClone(remote);
			if (clone == null) // Unity object — ==
			{
				continue; // template unavailable — retry next frame
			}

			// Mark the clone's carry role BEFORE applying stream state, so
			// SessionStatePump can suppress the native sit replay in the same
			// frame — this is not limited to the local carrier's view, because a
			// third-party rider clone also rides and the attach pass below
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
		}

		// WRITE pass: the state stream, now that every reading has been taken.
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
			if (!_session.IsRemoteInWorld(remote.SteamId))
			{
				continue; // in a menu/loading — its clone is not rendered
			}

			if (_remoteClones.TryGetValue(remote.SteamId, out var clone) && clone != null) // Unity object — ==
			{
				SessionStatePump.Apply(remote, clone);
			}
		}

		// Third pass: after every clone has been placed by SessionStatePump,
		// pin every carried rider clone to its carrier's VISUAL position. This
		// covers the local-carrier view and every third-party view alike, so
		// independent per-clone interpolation can never make the pair appear
		// detached. The rider's own local body uses the same rule later in
		// GameAdapter.Update.
		_carriedRider.AttachAll(localBody, _remoteClones);

		LogClonePosition();
	}

	/// <summary>
	/// The per-member render clone, created on first sight and rebuilt after a
	/// scene reload destroyed it. Retrying every frame is what absorbs the
	/// roster-join/world-creation ordering races.
	/// </summary>
	private Body? EnsureClone(PlayerEntity remote)
	{
		// == null on Unity objects — a scene reload destroys the clone and
		// reference-comparison would miss it; retry creation next frame.
		if (_remoteClones.TryGetValue(remote.SteamId, out var clone) && clone != null) // Unity object — ==
		{
			return clone;
		}

		clone = RemoteBodyFactory.CreateRemoteBody(remote, AnchorFor(remote), _log);
		if (clone == null) // Unity object — ==
		{
			return null; // template unavailable — retry next frame
		}

		_remoteClones[remote.SteamId] = clone;
		_log.LogInformation("Remote body created for {SteamId}.", remote.SteamId);
		// Render its carried items + limb presentation from the latest
		// snapshot (a fresh report follows within 1 s at the latest).
		if (_characterData.CloneData.TryGetValue(remote.SteamId, out var data))
		{
			_characterData.ApplyCloneInventory(clone, data, remote.SteamId);
			_limbRenderer.ApplyCloneLimbs(clone, data);
			RemoteCharacterDisplayProjection.ApplyRenderClone(clone, RemoteCharacterPresentation.State.From(data));
		}

		return clone;
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
