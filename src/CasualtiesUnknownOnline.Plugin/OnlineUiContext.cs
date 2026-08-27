using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Steam;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The read-only runtime data plus the UI action delegates that the Online UI
/// page drawers need. Keeping it in one small object keeps the drawer methods
/// from turning into ten-parameter calls.
/// </summary>
internal sealed class OnlineUiContext
{
	internal SteamService Steam = null!;

	internal SessionService Session = null!;

	internal EntitySyncService Entities = null!;

	internal RemoteVitalsService Vitals = null!;

	internal RemoteInventoryService Inventory = null!;

	internal IPlayerInteractionControl PlayerInteraction = null!;

	/// <summary>The world-backed line-of-sight oracle used to hide direct actions behind walls.</summary>
	internal IPlayerInteractionVisibility? Visibility;

	internal IHostBanService HostBan = null!;

	internal IHostRules HostRules = null!;

	internal ILocalizationService Localization = null!;

	internal HostRulesConfigEditor? RulesEditor;

	internal LoggingConfigEditor? Logging;

	internal LocalizationConfigEditor? Language;

	internal IGameAdapter? Adapter;

	internal string? LastJoinError;

	internal OnlineUiWindowState State = null!;

	internal Func<string, bool>? JoinLobby;

	internal Func<bool>? CreateLobby;

	internal Func<bool>? LeaveLobby;

	internal bool IpDirectActive;

	internal IpDirectConfigEditor? IpConfig;

	internal Func<bool>? CreateIpHost;

	internal Func<string, int, bool>? JoinIp;

	internal Func<bool>? LeaveIp;

	internal Func<ulong, ulong, bool>? TakeItem;

	internal Func<ulong, bool>? CarryRemote;


	internal Func<ulong, bool>? PiggybackRemote;

	internal Func<ulong, bool>? CarryOnBackRemote;

	internal Func<ulong, bool>? DropCarried;

	internal Func<ulong, bool>? HealRemote;

	internal Func<ulong, ulong, bool>? HealWithItem;

	internal Func<ulong, bool>? PushRemote;

	internal Func<ulong, bool>? RecruitPlayer;

	internal Func<ulong, bool>? KickMember;

	internal Func<ulong, bool>? BanMember;

	internal Func<ulong, bool>? UnbanMember;

	internal Func<IReadOnlyList<LocalHealItem>>? GetLocalHealItems;

	internal Func<bool>? HasHealItem;

	/// <summary>Opens the standalone player-interaction quick panel pinned to a remote player (the right-click "View items" path).</summary>
	internal Action<ulong>? OpenQuickPanel;

	/// <summary>Opens the game's native radial backpack UI focused on a remote player's render clone.</summary>
	internal Func<ulong, string, bool>? OpenRemoteBackpack;

	internal string T(string key) => Localization.T(key);

	internal string F(string key, params object?[] args) => Localization.Format(key, args);

	internal string RoleName(SessionRole role) => role switch
	{
		Runtime.Session.SessionRole.Host => T("common.role_host"),
		Runtime.Session.SessionRole.Guest => T("common.role_guest"),
		_ => T("common.role_none"),
	};

	/// <summary>Resolves the best display name for a member: custom IP-direct name
	/// when present, Steam persona otherwise, and a stable player-id fallback.</summary>
	internal string DisplayName(ulong id)
	{
		if (IpDirectActive)
		{
			if (id == Session.LocalSteamId)
			{
				var local = IpConfig?.DisplayName ?? "";
				return string.IsNullOrWhiteSpace(local) ? $"player-{id:X}" : local;
			}

			foreach (var member in Session.Members)
			{
				if (member.SteamId == id && !string.IsNullOrWhiteSpace(member.DisplayName))
				{
					return member.DisplayName;
				}
			}
		}

		var name = Steam.GetPersonaName(id);
		return string.IsNullOrWhiteSpace(name) ? $"player-{id:X}" : name;
	}
}
