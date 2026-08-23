using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Host-only ban path (second admin slice): sends the dedicated
/// <see cref="NetMsg.Banned"/> to the target, removes it through the existing
/// session control member-removal surface, and persists the SteamID so the
/// host rejects the player's later handshakes. Kept as a separate top-level
/// collaborator so <see cref="SessionService"/> stays under the architecture
/// gate — the ban is a session-control-plane behavior with its own state
/// (the persisted list), not session state.
/// </summary>
public sealed class HostBanService : IHostBanService
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly HostBanFileStore _persistence;
	private readonly ILogger<HostBanService> _log;
	private readonly HashSet<ulong> _banned = [];

	public HostBanService(ISessionControl session, PacketSender sender, HostBanFileStore persistence, ILogger<HostBanService> log)
	{
		_session = session;
		_sender = sender;
		_persistence = persistence;
		_log = log;
		_persistence.TryLoad(out _banned);
	}

	public IReadOnlyCollection<ulong> BannedSteamIds => [.. _banned];

	public bool IsBanned(ulong steamId) => _banned.Contains(steamId);

	public bool Ban(ulong steamId, string reason)
	{
		if (_session.Role != SessionRole.Host
			|| steamId == _session.LocalSteamId
			|| !_session.TryGetMember(steamId, out _)
			|| _banned.Contains(steamId))
		{
			return false;
		}

		_banned.Add(steamId);
		if (!Persist())
		{
			// The in-memory list stays authoritative for the caller's current
			// process only if the disk write succeeds; roll back otherwise.
			_banned.Remove(steamId);
			return false;
		}

		_sender.Send(steamId, NetMsg.Banned, new BannedMsg { Reason = reason }, reliable: true);
		_log.LogWarning("Host banned member {Member}: {Reason}.", steamId, reason);
		_session.RemoveGuestMember(steamId);
		return true;
	}

	public bool Unban(ulong steamId)
	{
		if (_session.Role != SessionRole.Host || !_banned.Remove(steamId))
		{
			return false;
		}

		if (!Persist())
		{
			// Restore the in-memory state if the disk write failed so the live
			// list never diverges from what a restart would read.
			_banned.Add(steamId);
			return false;
		}

		_log.LogInformation("Host unbanned member {Member}.", steamId);
		return true;
	}

	private bool Persist() => _persistence.Save(_banned);
}
