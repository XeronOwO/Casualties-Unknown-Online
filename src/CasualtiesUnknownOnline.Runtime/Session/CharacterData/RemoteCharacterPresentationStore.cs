using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Session-scoped, registry-backed read model for the unified remote character
/// presentation. It listens to the same character-data stream events that feed
/// the clone renderer and the Online UI caches, stores the latest authoritative
/// source snapshot per remote player, and exposes the typed
/// <see cref="RemoteCharacterPresentation.State"/> produced by
/// <see cref="RemoteCharacterPresentation.State.From(CharacterDataMsg)"/>.
///
/// Registration through <see cref="IProjectionDomain"/> makes this domain
/// visible in <see cref="ProjectionHealthCoordinator.Snapshot"/> and gives it
/// the same dirty/failure/rebuild containment as every other projection domain:
/// a failed presentation projection is marked dirty and rebuilt from the stored
/// source snapshots on the main-thread pump. Unlike kernel-backed domains, the
/// authoritative source of this stream-driven read model is the latest
/// character-data snapshot received for each remote player, so the store keeps
/// deep copies of those source snapshots as its rebuild baseline.
/// </summary>
public sealed class RemoteCharacterPresentationStore : IDisposable
{
	private readonly CharacterDataStore _characterData;
	private readonly SessionService _session;
	private readonly ProjectionHealthCoordinator _projectionHealth;
	private readonly ILogger<RemoteCharacterPresentationStore> _log;
	private readonly Dictionary<ulong, CharacterDataMsg> _sources = [];
	private readonly Dictionary<ulong, RemoteCharacterPresentation.State> _presentations = [];
	private ulong _revision;

	public RemoteCharacterPresentationStore(
		CharacterDataStore characterData,
		SessionService session,
		ProjectionHealthCoordinator projectionHealth,
		ILogger<RemoteCharacterPresentationStore> log)
	{
		_characterData = characterData;
		_session = session;
		_projectionHealth = projectionHealth;
		_log = log;
		_projectionHealth.Register(new ProjectionDomain(
			"remote-character-presentation",
			Rebuild,
			() => _revision));
		_characterData.CharacterDataReceived += OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived += OnHostCharacterDataReceived;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += OnSessionEnded;
	}

	/// <summary>
	/// The latest derived presentation for one remote player. Returns false when
	/// no character-data report has arrived for that player yet. Internal because
	/// the typed presentation model is an implementation detail of the runtime
	/// projection layer; the public API surface remains the UI snapshot services.
	/// </summary>
	internal bool TryGet(ulong steamId, out RemoteCharacterPresentation.State presentation) =>
		_presentations.TryGetValue(steamId, out presentation!);

	/// <summary>Number of cached remote-character presentations — used by tests and diagnostics.</summary>
	public int Count => _presentations.Count;

	/// <summary>Current local revision of the presentation cache (incremented on every source mutation).</summary>
	public ulong CurrentRevision => _revision;

	/// <summary>Raised after a failure-recovery rebuild recomputes every cached presentation.</summary>
	internal event Action<ulong>? PresentationsRebuilt;

	private void OnCharacterDataReceived(ulong sender, CharacterDataMsg data)
	{
		var owner = _session.Role == SessionRole.Host
			? sender
			: data.OwnerSteamId;
		if (owner == 0 || owner == _session.LocalSteamId)
		{
			return;
		}

		Update(owner, data);
	}

	private void OnHostCharacterDataReceived(CharacterDataMsg data)
	{
		var host = _session.HostSteamId;
		if (host == 0 || host == _session.LocalSteamId)
		{
			return;
		}

		Update(host, data);
	}

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (!inWorld)
		{
			Remove(steamId);
		}
	}

	private void OnSessionEnded() => Clear();

	private void Update(ulong steamId, CharacterDataMsg data)
	{
		var nextRevision = unchecked(_revision + 1);
		_projectionHealth.Run("remote-character-presentation", nextRevision, () =>
		{
			// Deep-copy the wire snapshot before caching. The character-data
			// source objects are shared with the host save path and may be
			// mutated in place by later terminal events; a projection must never
			// retain an alias that lets a reader pollute authority or observe a
			// mix of old derived values with a new source object. The store
			// treats each report as a full character-data snapshot, matching the
			// production capture path.
			var source = Serializer.DeepClone(data);
			_sources[steamId] = source;
			_presentations[steamId] = RemoteCharacterPresentation.State.From(source);
			_revision = nextRevision;
			_log.LogDebug("[RemotePresentation] cached {SteamId} at revision {Revision}.", steamId, nextRevision);
		});
	}

	private void Remove(ulong steamId)
	{
		var nextRevision = unchecked(_revision + 1);
		_projectionHealth.Run("remote-character-presentation", nextRevision, () =>
		{
			_sources.Remove(steamId);
			_presentations.Remove(steamId);
			_revision = nextRevision;
			_log.LogDebug("[RemotePresentation] removed {SteamId} at revision {Revision}.", steamId, nextRevision);
		});
	}

	private void Clear()
	{
		var nextRevision = unchecked(_revision + 1);
		_projectionHealth.Run("remote-character-presentation", nextRevision, () =>
		{
			_sources.Clear();
			_presentations.Clear();
			_revision = nextRevision;
			_log.LogDebug("[RemotePresentation] cleared cache at revision {Revision}.", nextRevision);
		});
	}

	private void Rebuild()
	{
		_presentations.Clear();
		foreach (var pair in _sources)
		{
			_presentations[pair.Key] = RemoteCharacterPresentation.State.From(pair.Value);
		}

		_log.LogInformation(
			"[RemotePresentation] rebuilt {Count} presentation(s) from source snapshots at revision {Revision}.",
			_presentations.Count,
			_revision);
		PresentationsRebuilt?.Invoke(_revision);
	}

	public void Dispose()
	{
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived -= OnHostCharacterDataReceived;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
	}
}
