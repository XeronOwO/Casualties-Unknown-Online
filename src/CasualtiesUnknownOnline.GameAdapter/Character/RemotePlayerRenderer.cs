using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

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

	/// <summary>Session/entity ended — destroy every render clone.</summary>
	internal void DestroyAllClones()
	{
		// == null on the Unity clones (is null would miss scene-reload-destroyed objects).
		foreach (var clone in _remoteClones.Values)
		{
			if (clone != null)
			{
				UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
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

			SessionStatePump.Apply(remote, clone);
			ApplyLocalCarrierFollow(localBody, remote, clone);
		}

		LogClonePosition();
	}

	/// <summary>
	/// Carrier-side presentation: when the LOCAL player is the carrier of this
	/// remote, pin that remote's render clone directly to the local body instead
	/// of waiting for the rider's 20 Hz state stream. This is presentation-only;
	/// the rider's own client still reports its authoritative position through
	/// the ordinary stream for every other peer.
	/// </summary>
	private void ApplyLocalCarrierFollow(Body? localBody, PlayerEntity remote, Body clone)
	{
		if (localBody == null // Unity object — ==
			|| localBody == clone // Unity objects — ==
			|| !_playerInteraction.TryGetCarried(_session.LocalSteamId, out var carriedId)
			|| carriedId != remote.SteamId)
		{
			return;
		}

		var position = CarriedBodyPlacement.BackOffset(
			localBody.transform.position,
			localBody.isRight,
			localBody.crouching);
		clone.transform.position = position;
		clone.rb.velocity = localBody.rb.velocity;
		clone.isRight = localBody.isRight;
		BodyFacing.Apply(clone);
		clone.standing = false;
		clone.moveDir = Vector2.zero;
		clone.targetLookPos = localBody.targetLookPos;
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
			_log.LogDebug("Clone {SteamId}: at ({PX:F1}, {PY:F1}), reported ({RX:F1}, {RY:F1}), active {Active}",
				steamId, pos.x, pos.y, reported.x, reported.y, clone != null && clone.gameObject.activeInHierarchy);
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
			UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
			_remoteClones.Remove(steamId);
		}

		_log.LogInformation(inWorld
			? "Remote entered the world — clone rebuilt on rejoin."
			: "Remote not in world (menu or disconnected) — clone destroyed.");
	}
}
