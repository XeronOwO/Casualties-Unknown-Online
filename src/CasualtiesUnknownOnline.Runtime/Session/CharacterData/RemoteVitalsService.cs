using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Session-scoped read-only cache of the latest remote players' character
/// data, projected from the character-data stream (the same 1 Hz reports the
/// clone renderer uses). It exists for the Online UI's "view vitals" and
/// "view medical" slices — the UI must never reach into the Game Adapter's
/// CloneFactTable or into Unity objects. The cache is filled from the public
/// character-data events and is cleared when the session ends, so a stale
/// player from a previous lobby can never appear with a dead value on the next
/// run.
/// </summary>
public sealed class RemoteVitalsService : IDisposable
{
	private readonly CharacterDataStore _characterData;
	private readonly SessionService _session;
	private readonly ILogger<RemoteVitalsService> _log;
	private readonly Dictionary<ulong, CacheEntry> _cache = [];

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
	/// The latest known compact vitals for one remote player. Returns false when
	/// no character snapshot has arrived for that player yet (or the snapshot
	/// carried no health block).
	/// </summary>
	public bool TryGet(ulong steamId, out RemoteVitalsSnapshot snapshot)
	{
		if (_cache.TryGetValue(steamId, out var entry))
		{
			snapshot = entry.Vitals;
			return true;
		}

		snapshot = null!;
		return false;
	}

	/// <summary>
	/// The latest known full medical view for one remote player. Returns false
	/// when no character snapshot with a health block has arrived yet.
	/// </summary>
	public bool TryGetMedical(ulong steamId, out RemoteMedicalSnapshot snapshot)
	{
		if (_cache.TryGetValue(steamId, out var entry))
		{
			snapshot = entry.Medical;
			return true;
		}

		snapshot = null!;
		return false;
	}

	/// <summary>Number of cached remote-player entries — used by tests and diagnostics.</summary>
	public int Count => _cache.Count;

	private void OnCharacterDataReceived(ulong sender, CharacterDataMsg data)
	{
		// Host: `sender` is the reporting guest. Guest: the host relays the
		// other guests with OwnerSteamId stamped; a zero OwnerSteamId here is the
		// local player's own restore, which is not a remote display target.
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
		// A remote left the world: drop its cached entry so a re-enter can
		// never show stale health before the next character snapshot arrives.
		if (!inWorld)
		{
			_cache.Remove(steamId);
		}
	}

	private void OnSessionEnded() => _cache.Clear();

	private void Update(ulong steamId, CharacterDataMsg data)
	{
		var vitals = RemoteVitalsSnapshot.From(data.Health);
		var medical = RemoteMedicalSnapshot.From(data);
		if (vitals is null || medical is null)
		{
			return;
		}

		_cache[steamId] = new CacheEntry(vitals, medical);
		_log.LogDebug("[Vitals] cached {SteamId}: HP {Health}", steamId, (int)Math.Round(vitals.BrainHealth));
	}

	public void Dispose()
	{
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived -= OnHostCharacterDataReceived;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
	}

	private sealed class CacheEntry
	{
		internal CacheEntry(RemoteVitalsSnapshot vitals, RemoteMedicalSnapshot medical)
		{
			Vitals = vitals;
			Medical = medical;
		}

		internal RemoteVitalsSnapshot Vitals { get; }

		internal RemoteMedicalSnapshot Medical { get; }
	}
}
