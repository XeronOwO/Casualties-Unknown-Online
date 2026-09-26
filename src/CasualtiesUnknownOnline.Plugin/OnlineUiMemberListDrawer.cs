using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The member card: the identity/status line and the interaction buttons that apply, for one projected
/// member row. The eligibility lives in <see cref="OnlineUiMemberProjection"/> and the card only
/// presents it.
///
/// <para>
/// It has TWO renderers during the migration (ticket online-ui-art-and-controls-overhaul, S2b): the
/// Players page takes the model this class builds for the game's own controls, and the quick panel —
/// which is IMGUI until S4 decides its fate — draws the same card itself. "Which buttons apply to this
/// member" is answered once (<see cref="AdminActions"/>, <see cref="InteractionActions"/>) and handed to
/// whichever renderer asks, so the two can never disagree about eligibility.
/// </para>
///
/// <para>
/// In the model path every action button is registered under an id naming the member and the action, so
/// the intent that comes back addresses exactly one button of one card: a click on a card the roster has
/// since rebuilt is dropped by the window rather than applied to whoever took that member's place.
/// </para>
/// </summary>
internal static class OnlineUiMemberListDrawer
{
	private const float AdminButtonWidth = 58f;
	private const float ItemButtonWidth = 180f;
	private const float BackpackButtonWidth = 130f;

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

	/// <summary>The card's identity line: the name in the member's own colour, plus the local/host tag. The
	/// colour is the one the session carries — four channels of it, so a marker its owner made translucent
	/// reads the same here as it does in the world (the eight-digit tag form the page's own status lines
	/// already use).</summary>
	internal static string Identity(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var tags = row.IsLocal ? ctx.T("member.you") : row.IsHost ? ctx.T("member.host") : "";
		var colorHex = ColorUtility.ToHtmlStringRGBA(new Color(row.Color.R, row.Color.G, row.Color.B, row.Color.A));
		return $"<color=#{colorHex}>{row.Name}{tags}</color>";
	}

	/// <summary>The card's status line: handshake, world state, RTT, vitals, inventory and the flags.</summary>
	internal static string Status(OnlineUiContext ctx, OnlineUiMemberRow row)
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

	/// <summary>The host's admin buttons for this member, in order.</summary>
	internal static IReadOnlyList<OnlineUiMemberAction> AdminActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var actions = new List<OnlineUiMemberAction>(2);
		if (row.CanKick)
		{
			actions.Add(new OnlineUiMemberAction(
				$"member.kick.{row.SteamId:X}",
				ctx.T("member.kick"),
				AdminButtonWidth,
				() => ctx.KickMember?.Invoke(row.SteamId)));
		}

		if (row.CanBan)
		{
			actions.Add(new OnlineUiMemberAction(
				$"member.ban.{row.SteamId:X}",
				ctx.T("member.ban"),
				AdminButtonWidth,
				() => ctx.BanMember?.Invoke(row.SteamId)));
		}

		return actions;
	}

	/// <summary>
	/// The interaction buttons for this member, in order: the carry/heal/push family, the explicit item
	/// transfers, the native medical surface and the remote backpack.
	/// </summary>
	internal static IReadOnlyList<OnlineUiMemberAction> InteractionActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var steamId = row.SteamId;
		var actions = new List<OnlineUiMemberAction>(12);
		if (row.CanCarry)
		{
			actions.Add(new OnlineUiMemberAction($"member.carry.{steamId:X}", ctx.T("member.carry"), 70f, () => ctx.CarryRemote?.Invoke(steamId)));
		}

		if (row.CanPiggyback)
		{
			actions.Add(new OnlineUiMemberAction($"member.piggyback.{steamId:X}", ctx.T("member.piggyback"), 90f, () => ctx.PiggybackRemote?.Invoke(steamId)));
		}

		if (row.CanCarryOnBack)
		{
			actions.Add(new OnlineUiMemberAction($"member.carry_on_back.{steamId:X}", ctx.T("member.carry_on_back"), 110f, () => ctx.CarryOnBackRemote?.Invoke(steamId)));
		}

		if (row.CanDrop)
		{
			actions.Add(new OnlineUiMemberAction($"member.drop.{steamId:X}", ctx.T("member.drop"), 70f, () => ctx.DropCarried?.Invoke(steamId)));
		}

		if (row.CanRequestDrop)
		{
			actions.Add(new OnlineUiMemberAction($"member.get_down.{steamId:X}", ctx.T("member.get_down"), 90f, () => ctx.DropCarried?.Invoke(steamId)));
		}

		if (row.CanRequestDropFromCarrier)
		{
			actions.Add(new OnlineUiMemberAction(
				$"member.get_down_carrier.{steamId:X}",
				ctx.T("member.get_down"),
				90f,
				() => ctx.DropCarried?.Invoke(ctx.Session.LocalSteamId)));
		}

		if (row.CanHeal)
		{
			actions.Add(new OnlineUiMemberAction($"member.heal.{steamId:X}", ctx.T("member.heal"), 70f, () => ctx.HealRemote?.Invoke(steamId)));
		}

		if (row.CanPush)
		{
			actions.Add(new OnlineUiMemberAction($"member.push.{steamId:X}", ctx.T("member.push"), 70f, () => ctx.PushRemote?.Invoke(steamId)));
		}

		if (row.CanRecruit)
		{
			actions.Add(new OnlineUiMemberAction($"member.recruit.{steamId:X}", ctx.T("member.recruit"), 70f, () => ctx.RecruitPlayer?.Invoke(steamId)));
		}

		if (row.CanViewMedical)
		{
			actions.Add(new OnlineUiMemberAction(
				$"member.open_medical.{steamId:X}",
				ctx.T("member.open_medical"),
				80f,
				() => ctx.OpenRemoteMedical?.Invoke(steamId, ctx.DisplayName(steamId))));
		}

		if (row.CanTake)
		{
			foreach (var item in row.TakeableItems)
			{
				actions.Add(new OnlineUiMemberAction(
					$"member.take.{steamId:X}.{item.InstanceId:X}",
					ctx.F("member.take", item.ItemId, item.SlotIndex),
					ItemButtonWidth,
					() => ctx.TakeItem?.Invoke(steamId, item.InstanceId)));
			}
		}

		foreach (var item in row.HealItems)
		{
			actions.Add(new OnlineUiMemberAction(
				$"member.heal_with.{steamId:X}.{item.InstanceId:X}",
				ctx.F("member.heal_with", item.ItemId),
				ItemButtonWidth,
				() => ctx.HealWithItem?.Invoke(steamId, item.InstanceId)));
		}

		if (row.InventoryText is not null && row.CanSee && !row.IsLocal && row.InWorld && ctx.OpenRemoteBackpack is { } open)
		{
			actions.Add(new OnlineUiMemberAction(
				$"member.open_backpack.{steamId:X}",
				ctx.T("member.open_backpack"),
				BackpackButtonWidth,
				() => open(steamId, ctx.DisplayName(steamId))));
		}

		return actions;
	}

	/// <summary>The Players page's path: the card as display-list rows for the game's own controls.</summary>
	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page, IReadOnlyList<OnlineUiMemberRow> rows)
	{
		if (rows.Count == 0)
		{
			page.Muted(ctx.T("member.no_members"));
			return;
		}

		foreach (var row in rows)
		{
			var identity = new List<OnlineUiElementModel> { page.LabelElement(Identity(ctx, row)) };
			foreach (var action in AdminActions(ctx, row))
			{
				identity.Add(page.ButtonElement(action.Id, action.Label, action.Invoke, action.Width));
			}

			page.Row([.. identity]);
			page.Muted(Status(ctx, row));

			var interactions = InteractionActions(ctx, row);
			if (interactions.Count > 0)
			{
				var elements = new List<OnlineUiElementModel>(interactions.Count);
				foreach (var action in interactions)
				{
					elements.Add(page.ButtonElement(action.Id, action.Label, action.Invoke, action.Width));
				}

				page.Row([.. elements]);
			}

			page.Space();
		}
	}

	/// <summary>
	/// The IMGUI path, for the quick panel alone: the same card, drawn by CUO while that panel is still
	/// IMGUI (S4 decides whether it moves onto the surface). It is not a second source of eligibility —
	/// both renderers read <see cref="AdminActions"/> and <see cref="InteractionActions"/>.
	/// </summary>
	internal static void BuildImgui(OnlineUiContext ctx, IReadOnlyList<OnlineUiMemberRow> rows)
	{
		if (rows.Count == 0)
		{
			GUILayout.Label(ctx.T("member.no_members"), OnlineUiTheme.MutedLabel());
			return;
		}

		foreach (var row in rows)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.Label(Identity(ctx, row), OnlineUiTheme.Label());
			GUILayout.FlexibleSpace();
			foreach (var action in AdminActions(ctx, row))
			{
				DrawImguiButton(action);
			}

			GUILayout.EndHorizontal();

			GUILayout.Label(Status(ctx, row), OnlineUiTheme.MutedLabel());

			GUILayout.BeginHorizontal();
			foreach (var action in InteractionActions(ctx, row))
			{
				DrawImguiButton(action);
			}

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUILayout.Space(4f);
		}
	}

	private static void DrawImguiButton(OnlineUiMemberAction action)
	{
		if (GUILayout.Button(action.Label, OnlineUiTheme.Button(), GUILayout.Width(action.Width)))
		{
			action.Invoke();
		}
	}
}
