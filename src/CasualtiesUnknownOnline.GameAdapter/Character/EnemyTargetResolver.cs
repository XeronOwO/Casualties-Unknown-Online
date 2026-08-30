using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Resolves the host-side enemy AI target set: builds the in-world player
/// candidate list (local body + remote entity-stream positions), finds a
/// selected fact back to its render candidate, and picks the limb index for a
/// host-ordered attack. Extracted from <see cref="EnemyCombatDirector"/> so the
/// director owns ordering/reporting while this class owns the target view.
/// </summary>
internal sealed class EnemyTargetResolver(
	ISessionControl session,
	IEntitySyncControl entities,
	RemotePlayerRenderer renderer)
{
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly List<EnemyTarget> _candidates = [];
	private int _candidateFrame = -1;

	internal EnemyTarget? Find(EnemyTargetFact? fact)
	{
		if (fact is not { } selected)
		{
			return null;
		}

		foreach (var candidate in BuildCandidates())
		{
			if (candidate.SteamId == selected.SteamId)
			{
				return candidate;
			}
		}

		return null;
	}

	internal IEnumerable<EnemyTargetFact> Facts() => BuildCandidates().Select(c => c.ToFact());

	internal int SelectLimbIndex(EnemyTarget target, Vector2 from)
	{
		var body = target.SteamId == _session.LocalSteamId
			? LocalBody()
			: (_renderer.TryGetRemoteBody(target.SteamId, out var remoteBody) ? remoteBody : null);
		return body != null ? BodyLimbIndex(body, from) : -1; // Unity object — ==; -1 = the victim picks its closest limb
	}

	internal Body? LocalBody()
	{
		var playerCamera = PlayerCamera.main;
		return playerCamera != null ? playerCamera.body : null; // Unity objects — ==
	}

	internal List<EnemyTarget> BuildCandidates()
	{
		if (_candidateFrame == Time.frameCount)
		{
			return _candidates;
		}

		_candidates.Clear();
		var localBody = LocalBody();
		if (localBody != null) // Unity object — ==
		{
			_candidates.Add(new EnemyTarget(
				_session.LocalSteamId,
				new Vector2(localBody.transform.position.x, localBody.transform.position.y),
				localBody));
		}

		foreach (var remote in _entities.RemotePlayers)
		{
			// StateReceivedMs < 0 = no report yet; the (0,0) buffer default would
			// drag enemies to the world origin.
			if (remote.StateReceivedMs < 0 || !_session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			_renderer.TryGetRemoteBody(remote.SteamId, out var remoteBody);
			_candidates.Add(new EnemyTarget(
				remote.SteamId,
				new Vector2(remote.Position.X, remote.Position.Y),
				remoteBody));
		}

		_candidateFrame = Time.frameCount;
		return _candidates;
	}

	private static int BodyLimbIndex(Body body, Vector2 from)
	{
		var limb = body.GetClosestLimb(from);
		for (var i = 0; i < body.limbs.Length; i++)
		{
			if (body.limbs[i] == limb) // Unity object — ==
			{
				return i;
			}
		}

		return -1;
	}
}
