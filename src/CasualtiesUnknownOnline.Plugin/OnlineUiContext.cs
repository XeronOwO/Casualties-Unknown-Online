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

	internal IHostBanService HostBan = null!;

	internal IHostRules HostRules = null!;

	internal ILocalizationService Localization = null!;

	internal HostRulesConfigEditor? RulesEditor;

	internal IGameAdapter? Adapter;

	internal string? LastJoinError;

	internal OnlineUiWindowState State = null!;

	internal Func<string, bool>? JoinLobby;

	internal Func<bool>? CreateLobby;

	internal Func<bool>? LeaveLobby;

	internal Func<ulong, ulong, bool>? TakeItem;

	internal Func<ulong, bool>? CarryRemote;

	internal Func<ulong, bool>? DropCarried;

	internal Func<ulong, bool>? HealRemote;

	internal Func<ulong, ulong, bool>? HealWithItem;

	internal Func<ulong, bool>? RecruitPlayer;

	internal Func<ulong, bool>? KickMember;

	internal Func<ulong, bool>? BanMember;

	internal Func<ulong, bool>? UnbanMember;

	internal Func<IReadOnlyList<LocalHealItem>>? GetLocalHealItems;

	internal Func<bool>? HasHealItem;

	internal string T(string key) => Localization.T(key);

	internal string F(string key, params object?[] args) => Localization.Format(key, args);

	internal string RoleName(SessionRole role) => role switch
	{
		Runtime.Session.SessionRole.Host => T("common.role_host"),
		Runtime.Session.SessionRole.Guest => T("common.role_guest"),
		_ => T("common.role_none"),
	};
}
