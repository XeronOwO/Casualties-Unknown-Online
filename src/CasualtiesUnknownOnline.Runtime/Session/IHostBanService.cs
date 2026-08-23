using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The host-only ban control surface. The host may ban a current guest from
/// the session (the target receives a dedicated <see cref="Protocol.Messages.BannedMsg"/>),
/// and the ban list is persisted so the same SteamID cannot handshake into
/// future sessions on this host. Guests see only the read-only queries via
/// their own <see cref="ISessionControl"/> context; <see cref="Ban"/> and
/// <see cref="Unban"/> are host-only.
/// </summary>
public interface IHostBanService
{
	/// <summary>Whether this SteamID is currently on the host's persisted ban list.</summary>
	bool IsBanned(ulong steamId);

	/// <summary>Read-only snapshot of the current banned SteamIDs (used by admin/testing surfaces).</summary>
	IReadOnlyCollection<ulong> BannedSteamIds { get; }

	/// <summary>
	/// Host only: ban a current non-local guest, send it the dedicated Banned
	/// message, remove it from the session and persist the ban. Returns false
	/// for a non-host caller, the local SteamID, an unknown member, or a
	/// SteamID that is already banned.
	/// </summary>
	bool Ban(ulong steamId, string reason);

	/// <summary>Host only: remove a SteamID from the persisted ban list. Returns false if it was not banned.</summary>
	bool Unban(ulong steamId);
}
