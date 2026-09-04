using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
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
/// snapshot cache for the clones' carried-item rendering.
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

	private readonly Dictionary<ulong, Body> _remoteClones = [];
	private long _nextCloneLogMs;

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
			_characterData.ApplyCloneInventory(clone, data);
			_limbRenderer.ApplyCloneLimbs(clone, data);
			CloneFacePresentation.Apply(clone, data.Health);
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

	/// <summary>Pump: lazy per-member clone ensure + state application + local-carrier follow + 1 Hz diagnostics.</summary>
	internal void Update(Body? localBody)
	{
		// Lazy per-member ensure: a roster join can arrive before the member's
		// world exists (the menu scene has no "Experiment" template), and members
		// can join mid-session — retrying every frame absorbs all ordering races.
		foreach (var remote in _entities.RemotePlayers)
		{
			if (!_session.IsRemoteInWorld(remote.SteamId))
			{
				continue; // in a menu/loading — no clone
			}

			// == null on Unity objects — a scene reload destroys the clone and
			// reference-comparison would miss it; retry creation next frame.
			if (!_remoteClones.TryGetValue(remote.SteamId, out var clone) || clone == null)
			{
				clone = RemoteBodyFactory.CreateRemoteBody(remote, AnchorFor(remote), _log);
				if (clone == null)
				{
					continue; // template unavailable — retry next frame
				}

				_remoteClones[remote.SteamId] = clone;
				_log.LogInformation("Remote body created for {SteamId}.", remote.SteamId);
				// Render its carried items + limb presentation from the latest
				// snapshot (a fresh report follows within 1 s at the latest).
				if (_characterData.CloneData.TryGetValue(remote.SteamId, out var data))
				{
					_characterData.ApplyCloneInventory(clone, data);
					_limbRenderer.ApplyCloneLimbs(clone, data);
					CloneFacePresentation.Apply(clone, data.Health);
				}
			}

			// Mark any remote clone that is a carried rider BEFORE applying
			// stream state so SessionStatePump can suppress the native sit
			// replay in the same frame. This is not limited to the local
			// carrier's view: a third-party rider clone also rides, and the
			// second-pass attach below forces its visible position anyway.
			var isCarriedRider = _playerInteraction.TryGetCarrier(remote.SteamId, out var carrierId)
				&& carrierId != 0;
			if (clone.TryGetComponent<RemoteBodyDriver>(out var cloneDriver))
			{
				cloneDriver.IsCarriedRider = isCarriedRider;
			}

			SessionStatePump.Apply(remote, clone);
		}

		// Second pass: after every clone has been placed by SessionStatePump,
		// pin every carried rider clone to its carrier's VISUAL position. This
		// covers the local-carrier view and every third-party view alike, so
		// independent per-clone interpolation can never make the pair appear
		// detached. The rider's own local body uses the same rule later in
		// GameAdapter.Update.
		ApplyRemoteCarrierAttachAll(localBody);

		LogClonePosition();
	}

	/// <summary>
	/// Pins every remote clone that is currently a carried rider to its
	/// carrier's visual position after all clones have been interpolated this
	/// frame. For a local carrier the anchor is the local body; for any other
	/// carrier (third-party view) the anchor is that carrier's already-smoothed
	/// render clone. This keeps the carry pair visually rigid on every side —
	/// the per-entity interpolator may lag, but the rider always rides the same
	/// displayed carrier, never an independent smoothed point.
	/// </summary>
	private void ApplyRemoteCarrierAttachAll(Body? localBody)
	{
		foreach (var entry in _remoteClones)
		{
			var riderSteamId = entry.Key;
			var riderClone = entry.Value;
			// == null on Unity clones — a scene reload can destroy one between
			// the first pass and this diagnostic/second pass.
			if (riderClone == null)
			{
				continue;
			}

			if (!_playerInteraction.TryGetCarrier(riderSteamId, out var carrierSteamId)
				|| carrierSteamId == 0)
			{
				continue;
			}

			if (carrierSteamId == _session.LocalSteamId)
			{
				if (localBody == null || localBody == riderClone) // Unity objects — ==
				{
					continue;
				}

				CarriedBodyPlacement.ApplyRidePose(
					riderClone,
					localBody.transform.position,
					localBody.isRight,
					localBody.crouching,
					localBody.rb.velocity,
					localBody.targetLookPos);
				continue;
			}

			if (_remoteClones.TryGetValue(carrierSteamId, out var carrierClone)
				&& carrierClone != null) // Unity object — ==
			{
				CarriedBodyPlacement.ApplyRidePose(
					riderClone,
					carrierClone.transform.position,
					carrierClone.isRight,
					carrierClone.crouching,
					carrierClone.rb.velocity,
					carrierClone.targetLookPos);
			}

			// No carrier clone yet (still creating or in a menu scene): keep
			// the ordinary SessionStatePump fallback until the carrier exists.
		}
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
			var carryTag = isRiderClone ? ", carried-rider-clone" : isCarrierClone ? ", carrier-clone" : "";
			_log.LogDebug("Clone {SteamId}: at ({PX:F1}, {PY:F1}), reported ({RX:F1}, {RY:F1}), active {Active}{CarryTag}",
				steamId, pos.x, pos.y, reported.x, reported.y, clone != null && clone.gameObject.activeInHierarchy, carryTag);
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
