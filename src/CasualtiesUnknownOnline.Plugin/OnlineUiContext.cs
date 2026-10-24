using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;

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

	/// <summary>The host rules in force for this process — the host's own configuration on a
	/// host, the host's announced set on a member.</summary>
	internal HostRulesService HostRules = null!;

	/// <summary>The member side of the host-rule replication: the announced set, or null when this
	/// client has received none. A member's local refusal reads THIS, never its own config (decision 263).</summary>
	internal AnnouncedHostRules AnnouncedRules = null!;

	/// <summary>The in-game command/chat console surface (slash commands + chat send).</summary>
	internal ICommandControl Commands = null!;

	/// <summary>Transient middle-click location pings (local only, presentation).</summary>
	internal ILocationPingControl LocationPings = null!;

	/// <summary>The runtime monotonic clock used for ping fade/expiry.</summary>
	internal ITimeSource Time = null!;

	internal ILocalizationService Localization = null!;

	internal HostRulesConfigEditor? RulesEditor;

	internal LoggingConfigEditor? Logging;

	internal LocalizationConfigEditor? Language;

	internal ConfigurationProfileStore? Profiles;

	/// <summary>The head anchor the nameplate/off-screen-indicator overlay pins to; null when the composition has no adapter.</summary>
	internal IPlayerAnchorQuery? AnchorQuery;

	/// <summary>The world-presence fact the Worlds page gates its armed restore on; null when the composition has no adapter.</summary>
	internal IWorldPresenceQuery? WorldPresence;

	/// <summary>The world library the Worlds page manages worlds and backups through (decision 198); null when the composition has none.</summary>
	internal IWorldLibrary? WorldLibrary;

	internal string? LastJoinError;

	internal OnlineUiWindowState State = null!;

	internal Func<string, bool>? JoinLobby;

	internal Func<bool>? CreateLobby;

	internal Func<bool>? LeaveLobby;

	internal bool IpDirectActive;

	internal IpDirectConfigEditor? IpConfig;

	internal PlayerColorConfigEditor? ColorConfig;

	/// <summary>Applies a local player colour choice (persist + identity); null returns to automatic.</summary>
	internal Action<PlayerColorValue?>? ChangePlayerColor;

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

	/// <summary>Starts watching a member's body (the death-screen chooser). False when this client may not watch or the member has no body here.</summary>
	internal Func<ulong, bool>? WatchMember;

	/// <summary>Stops watching and returns to the game's death screen.</summary>
	internal Func<bool>? StopWatching;

	/// <summary>One line the player reads, for an action that was refused: the card cannot answer every term the camera checks, so it has to say when it was wrong.</summary>
	internal Action<string>? Notify;

	/// <summary>The member this client is watching right now, or 0. The member list renders it as the stop button.</summary>
	internal ulong WatchingSteamId;

	/// <summary>True while this client is in spectator mode: what the member card's watch entry is gated on (the
	/// ruling: no distance or reachability check, only the mode).</summary>
	internal bool IsSpectator;

	/// <summary>True while the free camera is wanted: the toggle's label reads it so the button says what it will
	/// do (the mode owns the capability; there is no host rule).</summary>
	internal bool IsFreeFly;

	/// <summary>Steps the watch to the previous (-1) or next (+1) watchable member in lobby order; false when there
	/// was nobody to step to (a one-member lobby, or a refusal by the camera).</summary>
	internal Func<int, bool>? CycleWatch;

	/// <summary>Turns the free camera on or off by what it is now; false when nothing is being watched.</summary>
	internal Func<bool>? ToggleFreeFly;

	internal Func<ulong, bool>? BanMember;

	internal Func<ulong, bool>? UnbanMember;

	internal Func<IReadOnlyList<LocalHealItem>>? GetLocalHealItems;

	internal Func<bool>? HasHealItem;

	/// <summary>Opens the game's native radial backpack UI focused on a remote player's render clone.</summary>
	internal Func<ulong, string, bool>? OpenRemoteBackpack;

	/// <summary>Opens the game's native WoundView medical UI for a remote player.</summary>
	internal Func<ulong, string, bool>? OpenRemoteMedical;

	internal string T(string key) => Localization.T(key);

	internal string F(string key, params object?[] args) => Localization.Format(key, args);

	internal string RoleName(SessionRole role) => role switch
	{
		SessionRole.Host => T("common.role_host"),
		SessionRole.Guest => T("common.role_guest"),
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

	/// <summary>
	/// Resolves the effective marker color for a player: the local configured
	/// palette choice or the remote member's wire-carried selection when
	/// present, otherwise the deterministic SteamId auto palette.
	/// </summary>
	internal PlayerColorValue PlayerColor(ulong id)
	{
		if (id == Session.LocalSteamId && ColorConfig is { } colorConfig)
		{
			if (colorConfig.CurrentColor is { } localColor)
			{
				return localColor;
			}
		}

		foreach (var member in Session.Members)
		{
			if (member.SteamId == id && member.SelectedColor is { } selected)
			{
				return PlayerColorValue.FromNetColorRgba(selected);
			}
		}

		return PlayerColorResolver.Resolve(id);
	}
}
