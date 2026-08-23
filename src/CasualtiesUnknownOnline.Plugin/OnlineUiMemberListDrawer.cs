using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

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
		return OnlineUiMemberProjection.Build(
			localSteamId: ctx.Steam.LocalSteamId,
			lobbyOwner: ctx.Steam.GetLobbyOwner(),
			lobbyMembers: ctx.Steam.GetLobbyMembers(),
			members: ctx.Session.Members,
			displayName: id => DisplayName(ctx.Steam, id),
			getVitals: id => ctx.Vitals.TryGet(id, out var v) ? v : null,
			getInventory: id => ctx.Inventory.TryGet(id, out var inv) ? inv : null,
			playerInteraction: ctx.PlayerInteraction,
			hostBan: ctx.HostBan,
			canAdmin: ctx.Session.Role == Runtime.Session.SessionRole.Host && ctx.Session.SessionActive,
			localInWorld: ctx.Session.LocalInWorld,
			hasHealItem: ctx.HasHealItem?.Invoke() ?? false,
			healItems: ctx.GetLocalHealItems?.Invoke() ?? []);
	}

	internal static void Draw(OnlineUiContext ctx, IReadOnlyList<OnlineUiMemberRow> rows)
	{
		if (rows.Count == 0)
		{
			GUILayout.Label("No lobby members yet.", OnlineUiTheme.MutedLabel());
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
		var tags = row.IsLocal ? " (you)" : row.IsHost ? " (host)" : "";
		GUILayout.Label($"{row.Name}{tags}", OnlineUiTheme.Label());
		GUILayout.FlexibleSpace();
		DrawAdminActions(ctx, row);
		GUILayout.EndHorizontal();

		var status = BuildStatus(row);
		GUILayout.Label(status, OnlineUiTheme.MutedLabel());

		DrawWorldActions(ctx, row);

		if (row.Inventory is { Count: > 0 } && ctx.State.Page == OnlineUiPage.Players)
		{
			DrawInventoryToggle(ctx, row);
		}

		GUILayout.EndVertical();
	}

	private static string BuildStatus(OnlineUiMemberRow row)
	{
		var state = row.Handshaken ? "handshake" : "no handshake";
		if (row.InWorld)
		{
			state += ", in world";
		}
		else
		{
			state += ", menu";
		}

		if (row.RttMs >= 0f)
		{
			state += $", {row.RttMs:F0} ms";
		}

		if (!string.IsNullOrEmpty(row.VitalsText))
		{
			state += $" — {row.VitalsText}";
		}

		if (!string.IsNullOrEmpty(row.InventoryText))
		{
			state += $" — {row.InventoryText}";
		}

		if (row.IsBanned)
		{
			state += " [banned]";
		}

		return state;
	}

	private static void DrawAdminActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		if (row.CanKick && GUILayout.Button("Kick", OnlineUiTheme.Button(), GUILayout.Width(58f)))
		{
			ctx.KickMember?.Invoke(row.SteamId);
		}

		if (row.CanBan && GUILayout.Button("Ban", OnlineUiTheme.Button(), GUILayout.Width(58f)))
		{
			ctx.BanMember?.Invoke(row.SteamId);
		}
	}

	private static void DrawWorldActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var hasAction = row.IsCarryingThis || row.CanCarry || row.CanHeal || row.CanRecruit || row.CanTake;
		if (!hasAction)
		{
			return;
		}

		GUILayout.BeginHorizontal();
		if (row.CanCarry && GUILayout.Button("Carry", OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.CarryRemote?.Invoke(row.SteamId);
		}

		if (row.CanDrop && GUILayout.Button("Drop", OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.DropCarried?.Invoke(row.SteamId);
		}

		if (row.CanHeal && GUILayout.Button("Heal", OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.HealRemote?.Invoke(row.SteamId);
		}

		if (row.CanRecruit && GUILayout.Button("Recruit", OnlineUiTheme.Button(), GUILayout.Width(70f)))
		{
			ctx.RecruitPlayer?.Invoke(row.SteamId);
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
			if (GUILayout.Button($"Take {item.ItemId} ({item.SlotIndex})", OnlineUiTheme.Button(), GUILayout.Width(180f)))
			{
				ctx.TakeItem?.Invoke(row.SteamId, item.InstanceId);
			}
		}
	}

	private static void DrawHealItemButtons(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		foreach (var item in row.HealItems)
		{
			if (GUILayout.Button($"Heal with {item.ItemId}", OnlineUiTheme.Button(), GUILayout.Width(180f)))
			{
				ctx.HealWithItem?.Invoke(row.SteamId, item.InstanceId);
			}
		}
	}

	private static void DrawInventoryToggle(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		GUILayout.BeginHorizontal();
		if (GUILayout.Button(ctx.State.ExpandedMember == row.SteamId ? "Hide items" : "View items", OnlineUiTheme.Button(), GUILayout.Width(110f)))
		{
			ctx.State.ExpandedMember = ctx.State.ExpandedMember == row.SteamId ? null : row.SteamId;
		}

		GUILayout.EndHorizontal();

		if (ctx.State.ExpandedMember == row.SteamId)
		{
			if (row.Inventory is { } inventory)
			{
				foreach (var entry in inventory)
				{
					DrawInventoryEntry(entry, 0);
				}
			}
			else
			{
				GUILayout.Label("(empty)", OnlineUiTheme.MutedLabel());
			}
		}
	}

	private static void DrawInventoryEntry(RemoteInventoryEntry entry, int depth)
	{
		var slot = entry.SlotIndex >= 0 ? $"slot {entry.SlotIndex}" : "worn";
		var suffix = entry.ContentsCount > 0 ? $" (+{entry.ContentsCount} inside)" : "";
		var favourite = entry.Favourited ? " ★" : "";
		var indent = new string(' ', depth * 4);
		GUILayout.Label($"{indent}{slot}: {entry.ItemId}{suffix}{favourite}", OnlineUiTheme.MutedLabel());

		foreach (var child in entry.Contents)
		{
			DrawInventoryEntry(child, depth + 1);
		}
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
