using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;
using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Renders the projected member rows for both the Lobby and Players pages.
/// One row is a compact card: identity/status line first, then the interaction
/// buttons that apply. The button eligibility already lives in
/// <see cref="OnlineUiMemberProjection"/>; this class only projects the booleans
/// into Unity IMGUI controls.
/// </summary>
internal static class OnlineUiMemberListDrawer
{
	internal static IReadOnlyList<OnlineUiMemberRow> BuildRows(OnlineUiContext ctx)
	{
		IReadOnlyList<ulong> lobbyMembers = ctx.IpDirectActive
			? [ctx.Session.LocalSteamId, .. ctx.Session.Members.Select(m => m.SteamId)]
			: ctx.Steam.GetLobbyMembers();
		var lobbyOwner = ctx.IpDirectActive ? ctx.Session.HostSteamId : ctx.Steam.GetLobbyOwner();
		return OnlineUiMemberProjection.Build(
			localSteamId: ctx.Steam.LocalSteamId,
			lobbyOwner: lobbyOwner,
			lobbyMembers: lobbyMembers,
			members: ctx.Session.Members,
			displayName: ctx.DisplayName,
			getVitals: id => ctx.Vitals.TryGet(id, out var v) ? v : null,
			getInventory: id => ctx.Inventory.TryGet(id, out var inv) ? inv : null,
			playerInteraction: ctx.PlayerInteraction,
			hostBan: ctx.HostBan,
			canAdmin: ctx.Session.Role == SessionRole.Host && ctx.Session.SessionActive,
			localInWorld: ctx.Session.LocalInWorld,
			hasHealItem: ctx.HasHealItem?.Invoke() ?? false,
			healItems: ctx.GetLocalHealItems?.Invoke() ?? [],
			allowRemoteInventoryTake: ctx.HostRules.AllowRemoteInventoryTake,
			hasLineOfSight: ctx.Visibility is null
				? null
				: id => ctx.Visibility!.HasLineOfSight(ctx.Session.LocalSteamId, id),
			getColor: ctx.PlayerColor);
	}

	internal static void Draw(OnlineUiContext ctx, IReadOnlyList<OnlineUiMemberRow> rows)
	{
		if (rows.Count == 0)
		{
			GUILayout.Label(ctx.T("member.no_members"), OnlineUiTheme.MutedLabel());
			return;
		}

		foreach (var row in rows)
		{
			DrawRow(ctx, row);
			GUILayout.Space(4f);
		}
	}

	private static void DrawRow(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		GUILayout.BeginVertical();

		GUILayout.BeginHorizontal();
		var tags = row.IsLocal ? ctx.T("member.you") : row.IsHost ? ctx.T("member.host") : "";
		var colorHex = ColorUtility.ToHtmlStringRGB(ToColor(row.Color));
		GUILayout.Label($"<color=#{colorHex}>{row.Name}{tags}</color>", OnlineUiTheme.Label());
		GUILayout.FlexibleSpace();
		DrawAdminActions(ctx, row);
		GUILayout.EndHorizontal();

		var status = BuildStatus(ctx, row);
		GUILayout.Label(status, OnlineUiTheme.MutedLabel());

		DrawWorldActions(ctx, row);

		if (row.InventoryText is not null)
		{
			DrawInventoryActions(ctx, row);
		}

		GUILayout.EndVertical();
	}

	private static string BuildStatus(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var state = ctx.T(row.Handshaken ? "member.status_handshake" : "member.status_no_handshake");
		if (row.InWorld)
		{
			state += ", " + ctx.T("member.status_in_world");
		}
		else
		{
			state += ", " + ctx.T("member.status_menu");
		}

		if (row.RttMs >= 0f)
		{
			state += $", {row.RttMs:F0} ms";
		}

		if (!string.IsNullOrEmpty(row.VitalsText))
		{
			state += $" — {row.VitalsText}";
		}

		if (row.CanSee && !string.IsNullOrEmpty(row.InventoryText))
		{
			state += $" — {row.InventoryText}";
		}

		if (row.IsDead)
		{
			state += ctx.T("member.status_dead");
		}
		else if (row.IsUnconscious)
		{
			state += ctx.T("member.status_unconscious");
		}

		if (row.IsCarryingSomeone)
		{
			state += ctx.T("member.status_carrying");
		}

		if (row.IsCarried)
		{
			state += ctx.T("member.status_carried");
		}

		if (row.IsBanned)
		{
			state += ctx.T("member.banned");
		}

		if (!string.IsNullOrEmpty(row.PeerIdHex))
		{
			state += ctx.F("member.peer_id", row.PeerIdHex);
		}

		return state;
	}

	private static void DrawAdminActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		if (row.CanKick && GUILayout.Button(ctx.T("member.kick"), OnlineUiTheme.Button(), GUILayout.Width(58f)))
		{
			ctx.KickMember?.Invoke(row.SteamId);
		}

		if (row.CanBan && GUILayout.Button(ctx.T("member.ban"), OnlineUiTheme.Button(), GUILayout.Width(58f)))
		{
			ctx.BanMember?.Invoke(row.SteamId);
		}
	}

	private static void DrawWorldActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var hasAction = row.IsCarryingThis
			|| row.CanCarry
			|| row.CanPiggyback
			|| row.CanCarryOnBack
			|| row.CanDrop
			|| row.CanRequestDrop
			|| row.CanRequestDropFromCarrier
			|| row.CanHeal
			|| row.CanPush
			|| row.CanRecruit
			|| row.CanTake
			|| row.CanViewMedical;
		if (!hasAction)
		{
			return;
		}

		GUILayout.BeginHorizontal();
		if (row.CanCarry && GUILayout.Button(ctx.T("member.carry"), OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.CarryRemote?.Invoke(row.SteamId);
		}

		if (row.CanPiggyback && GUILayout.Button(ctx.T("member.piggyback"), OnlineUiTheme.Button(), GUILayout.Width(90f)))
		{
			ctx.PiggybackRemote?.Invoke(row.SteamId);
		}

		if (row.CanCarryOnBack && GUILayout.Button(ctx.T("member.carry_on_back"), OnlineUiTheme.Button(), GUILayout.Width(110f)))
		{
			ctx.CarryOnBackRemote?.Invoke(row.SteamId);
		}

		if (row.CanDrop && GUILayout.Button(ctx.T("member.drop"), OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.DropCarried?.Invoke(row.SteamId);
		}

		if (row.CanRequestDrop && GUILayout.Button(ctx.T("member.get_down"), OnlineUiTheme.Button(), GUILayout.Width(90f)))
		{
			ctx.DropCarried?.Invoke(row.SteamId);
		}

		if (row.CanRequestDropFromCarrier && GUILayout.Button(ctx.T("member.get_down"), OnlineUiTheme.Button(), GUILayout.Width(90f)))
		{
			ctx.DropCarried?.Invoke(ctx.Session.LocalSteamId);
		}

		if (row.CanHeal && GUILayout.Button(ctx.T("member.heal"), OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.HealRemote?.Invoke(row.SteamId);
		}

		if (row.CanPush && GUILayout.Button(ctx.T("member.push"), OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.PushRemote?.Invoke(row.SteamId);
		}

		if (row.CanRecruit && GUILayout.Button(ctx.T("member.recruit"), OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.RecruitPlayer?.Invoke(row.SteamId);
		}

		if (row.CanViewMedical && GUILayout.Button(ctx.T("member.open_medical"), OnlineUiTheme.Button(), GUILayout.Width(80f)))
		{
			ctx.OpenMedical?.Invoke(row.SteamId);
		}

		if (row.CanTake)
		{
			DrawTakeButtons(ctx, row);
		}

		GUILayout.EndHorizontal();

		if (row.HealItems.Count > 0)
		{
			DrawHealItemButtons(ctx, row);
		}
	}

	private static void DrawTakeButtons(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		foreach (var item in row.TakeableItems)
		{
			if (GUILayout.Button(ctx.F("member.take", item.ItemId, item.SlotIndex), OnlineUiTheme.Button(), GUILayout.Width(180f)))
			{
				ctx.TakeItem?.Invoke(row.SteamId, item.InstanceId);
			}
		}
	}

	private static void DrawHealItemButtons(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		foreach (var item in row.HealItems)
		{
			if (GUILayout.Button(ctx.F("member.heal_with", item.ItemId), OnlineUiTheme.Button(), GUILayout.Width(180f)))
			{
				ctx.HealWithItem?.Invoke(row.SteamId, item.InstanceId);
			}
		}
	}

	private static void DrawInventoryActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		if (!row.CanSee || row.IsLocal || !row.InWorld || ctx.OpenRemoteBackpack is not { } open)
		{
			return;
		}

		GUILayout.BeginHorizontal();
		if (GUILayout.Button(ctx.T("member.open_backpack"), OnlineUiTheme.Button(), GUILayout.Width(130f)))
		{
			open(row.SteamId, ctx.DisplayName(row.SteamId));
		}

		GUILayout.EndHorizontal();
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}

	private static Color ToColor(PlayerColorValue value) => new(value.R, value.G, value.B, value.A);
}
