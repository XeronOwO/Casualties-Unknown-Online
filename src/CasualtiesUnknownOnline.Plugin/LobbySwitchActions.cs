using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The lobby create/join/leave policy: one owner for "may this client switch
/// lobbies right now" and for the reason a switch was refused, shared by the Steam
/// callbacks and the Online UI (one policy, two entry points). It holds no UI
/// state — <see cref="OnlineUiHost"/> only reads <see cref="LastError"/>.
/// </summary>
internal sealed class LobbySwitchActions(
	SteamService steam,
	CuoNetworkRouter router,
	SessionService session,
	IWorldPresenceQuery? worldPresence,
	ILocalizationService localization,
	ILogger<LobbySwitchActions> log)
{
	/// <summary>Why the last switch was refused, shown on the test HUD; cleared when a lobby is entered.</summary>
	internal string? LastError { get; set; }

	/// <summary>Online UI Join (and the pending "+connect_lobby" join): the lobby id comes from the caller, the policy is this one.</summary>
	internal bool TryJoin(string lobbyId)
	{
		if (!EnsureSteamReady() || !CanJoin() || !ulong.TryParse(lobbyId, out var lobbyIdValue))
		{
			return false;
		}

		steam.JoinLobby(lobbyIdValue);
		return true;
	}

	/// <summary>Online UI Create button path.</summary>
	internal bool TryCreate()
	{
		if (!EnsureSteamReady() || !CanCreate())
		{
			return false;
		}

		steam.CreateLobby();
		return true;
	}

	/// <summary>Online UI Leave Lobby / Close Room path.</summary>
	internal bool TryLeave()
	{
		if (!EnsureSteamReady())
		{
			return false;
		}

		steam.LeaveLobby();
		return true;
	}

	/// <summary>Join policy: a lobby join always changes identity, so any active world/generation blocks it. The reason is visible on the test HUD.</summary>
	internal bool CanJoin()
	{
		if (router.IpDirectSteam.IsActive)
		{
			LastError = localization.T("ip.blocked_active_session");
			log.LogWarning("Steam lobby join refused: an IP-direct session is active.");
			return false;
		}

		if (worldPresence is not { IsInWorldOrGenerating: true })
		{
			return true;
		}

		LastError = localization.T("lobby.join_blocked_in_world");
		log.LogWarning("Lobby join refused: a world is running or generating.");
		return false;
	}

	/// <summary>Create policy: menu is always allowed; in a world only the solo->host conversion is (no session, no identity change away from another host).</summary>
	internal bool CanCreate()
	{
		if (router.IpDirectSteam.IsActive)
		{
			LastError = localization.T("ip.blocked_active_session");
			log.LogWarning("Steam lobby create refused: an IP-direct session is active.");
			return false;
		}

		if (worldPresence is not { IsInWorldOrGenerating: true })
		{
			return true;
		}

		if (LobbySwitchGuard.CanCreateLobby(session.Role, session.SessionActive, worldFlowActive: true))
		{
			return true;
		}

		LastError = localization.T("lobby.join_blocked_in_world");
		log.LogWarning("Lobby create refused: a sessioned world is running or generating.");
		return false;
	}

	private bool EnsureSteamReady() => steam.Initialize();
}
