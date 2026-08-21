using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Session-scoped read-only cache of the latest remote players' vitals,
/// projected from the character-data stream (the same 1 Hz reports the clone
/// renderer uses). It exists for the Online UI's "view vitals" slice — the UI
/// must never reach into the Game Adapter's CloneFactTable or into Unity
/// objects. The cache is filled from the public character-data events and is
/// cleared when the session ends, so a stale player from a previous lobby can
/// never appear with a dead value on the next run.
/// </summary>
public sealed class RemoteVitalsService : IDisposable
{
	private readonly CharacterDataStore _characterData;
	private readonly SessionService _session;
	private readonly ILogger<RemoteVitalsService> _log;
	private readonly Dictionary<ulong, RemoteVitalsSnapshot> _vitals = [];

	public RemoteVitalsService(
		CharacterDataStore characterData,
		SessionService session,
		ILogger<RemoteVitalsService> log)
	{
		_characterData = characterData;
		_session = session;
		_log = log;
		_characterData.CharacterDataReceived += OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived += OnHostCharacterDataReceived;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += OnSessionEnded;
	}

	/// <summary>
	/// The latest known vitals for one remote player. Returns false when no
	/// character snapshot has arrived for that player yet (or the snapshot
	/// carried no health block).
	/// </summary>
	public bool TryGet(ulong steamId, out RemoteVitalsSnapshot snapshot) =>
		_vitals.TryGetValue(steamId, out snapshot!);

	/// <summary>Number of cached remote-player vitals entries — used by tests and diagnostics.</summary>
	public int Count => _vitals.Count;

	private void OnCharacterDataReceived(ulong sender, CharacterDataMsg data)
	{
		// Host: `sender` is the reporting guest. Guest: the host relays the
		// other guests with OwnerSteamId stamped; a zero OwnerSteamId here is the
		// local player's own restore, which is not a remote display target.
		var owner = _session.Role == SessionRole.Host
			? sender
			: data.OwnerSteamId;
		if (owner == 0 || owner == _session.LocalSteamId || data.Health is null)
		{
			return;
		}

		Update(owner, data.Health);
	}

	private void OnHostCharacterDataReceived(CharacterDataMsg data)
	{
		var host = _session.HostSteamId;
		if (host == 0 || host == _session.LocalSteamId || data.Health is null)
		{
			return;
		}

		Update(host, data.Health);
	}

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		// A remote left the world: drop its cached vitals so a re-enter can
		// never show stale health before the next character snapshot arrives.
		if (!inWorld)
		{
			_vitals.Remove(steamId);
		}
	}

	private void OnSessionEnded() => _vitals.Clear();

	private void Update(ulong steamId, CharacterHealthMsg health)
	{
		if (RemoteVitalsSnapshot.From(health) is not { } snapshot)
		{
			return;
		}

		_vitals[steamId] = snapshot;
		_log.LogDebug("[Vitals] cached {SteamId}: HP {Health}", steamId, (int)Math.Round(snapshot.BrainHealth));
	}

	public void Dispose()
	{
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived -= OnHostCharacterDataReceived;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
	}
}
