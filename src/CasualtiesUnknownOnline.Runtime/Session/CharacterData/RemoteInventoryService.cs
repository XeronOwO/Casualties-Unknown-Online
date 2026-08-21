using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Session-scoped read-only cache of the latest remote players' carried
/// inventories, projected from the character-data stream (the same 1 Hz
/// snapshots the clone renderer uses). It exists for the Online UI's
/// "view items" slice — the UI must never reach into the Game Adapter's
/// CloneFactTable or into Unity objects. The cache is filled from the public
/// character-data events and is cleared when the session ends, so a stale
/// player from a previous lobby can never appear with another run's items.
/// </summary>
public sealed class RemoteInventoryService : IDisposable
{
	private readonly CharacterDataStore _characterData;
	private readonly SessionService _session;
	private readonly ILogger<RemoteInventoryService> _log;
	private readonly Dictionary<ulong, RemoteInventorySnapshot> _inventories = [];

	public RemoteInventoryService(
		CharacterDataStore characterData,
		SessionService session,
		ILogger<RemoteInventoryService> log)
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
	/// The latest known carried inventory for one remote player. Returns false
	/// when no character snapshot has arrived for that player yet.
	/// </summary>
	public bool TryGet(ulong steamId, out RemoteInventorySnapshot snapshot) =>
		_inventories.TryGetValue(steamId, out snapshot!);

	/// <summary>Number of cached remote-player inventories — used by tests and diagnostics.</summary>
	public int Count => _inventories.Count;

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
		// A remote left the world: drop its cached inventory so a re-enter can
		// never show stale items before the next character snapshot arrives.
		if (!inWorld)
		{
			_inventories.Remove(steamId);
		}
	}

	private void OnSessionEnded() => _inventories.Clear();

	private void Update(ulong steamId, CharacterDataMsg data)
	{
		if (RemoteInventorySnapshot.From(data) is not { } snapshot)
		{
			return;
		}

		_inventories[steamId] = snapshot;
		_log.LogDebug("[Inventory] cached {SteamId}: {Count} items", steamId, snapshot.Count);
	}

	public void Dispose()
	{
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived -= OnHostCharacterDataReceived;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
	}
}
