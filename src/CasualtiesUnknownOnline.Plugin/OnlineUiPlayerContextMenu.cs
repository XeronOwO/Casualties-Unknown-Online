using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The in-world right-click player interaction menu. It is opened by the
/// overlay when the user right-clicks near a remote player's projected body
/// position (the remote clones deliberately have no colliders — physics-off
/// render proxies — so the menu uses the authoritative entity stream positions
/// instead of Physics2D hits). The menu reuses the same projected member rows
/// and action delegates as the Players page, so it never duplicates the
/// eligibility rules.
///
/// <para>
/// Since S5 it draws nothing: it builds a <see cref="OnlineUiPanelModel"/> for the game's own controls on
/// CUO's surface, placed at the screen point the player clicked and clamped into the screen by the
/// Runtime's own rule. The gesture that opens it stays in the IMGUI pass — the right-click and the
/// click-away are the world's, not a control's — while what closes it on a pick is the action the control
/// reports.
/// </para>
/// </summary>
internal sealed class OnlineUiPlayerContextMenu
{
	/// <summary>The menu's width in the surface's canvas units — the IMGUI menu's own rect.</summary>
	internal const float Width = 240f;

	/// <summary>The width one candidate button asks for.</summary>
	private const float CandidateButtonWidth = 70f;

	/// <summary>Every id this menu registers is namespaced, so a control of the menu and a control of the
	/// window (or of the quick panel) can never be the same intent.</summary>
	private const string IdPrefix = "menu.";

	private ulong? _targetSteamId;
	private IReadOnlyList<ulong> _candidateSteamIds = [];

	/// <summary>Where the player right-clicked, in GUI space (origin top-left, Y down) — the space the
	/// IMGUI pass reads pointer events in.</summary>
	private Vector2 _position;

	internal bool IsOpen => _targetSteamId.HasValue;

	internal void Open(ulong steamId, IReadOnlyList<ulong> candidates, Vector2 guiPosition)
	{
		_targetSteamId = steamId;
		_candidateSteamIds = [.. candidates];
		_position = guiPosition;
	}

	internal void Close()
	{
		_targetSteamId = null;
		_candidateSteamIds = [];
	}

	/// <summary>
	/// The world gesture this menu answers: a right-click opens it on the nearest candidate (or closes it
	/// when the click is not near anyone), and a left-click outside it closes it. Both questions about CUO
	/// surfaces are asked of the caller, because the census is the one rule that answers them — a click
	/// inside any CUO surface belongs to that surface, never to the world menu.
	/// </summary>
	internal void HandleInput(OnlineUiContext ctx, bool pointerOverMenu, Func<bool> blockedByCuiSurface)
	{
		var evt = Event.current;
		if (evt == null || evt.type != EventType.MouseDown)
		{
			return;
		}

		var mouse = evt.mousePosition;
		if (evt.button == 1)
		{
			// Right-clicks inside a CUO surface belong to the UI, not the world: never open, re-target or
			// close the in-world menu from one.
			if (blockedByCuiSurface())
			{
				return;
			}

			if (TryFindRemoteCandidatesAt(mouse, ctx, out var candidates))
			{
				Open(candidates[0], candidates, mouse);
				evt.Use();
			}
			else if (IsOpen)
			{
				Close();
				evt.Use();
			}

			return;
		}

		// The pointer-over-menu fact is the surface's own poll (the menu is a control of CUO's canvas now),
		// which is why this no longer asks a rectangle of its own.
		if (evt.button == 0 && IsOpen && !pointerOverMenu)
		{
			Close();
		}
	}

	/// <summary>
	/// The menu's model for this frame, or null while it is closed. Every action is registered under the
	/// position it has in this frame's list, so a click lands on the action that produced its control; the
	/// menu closes on the pick, the way the IMGUI button did.
	/// </summary>
	internal OnlineUiPanelModel? Build(OnlineUiContext ctx, Dictionary<string, Action<OnlineUiIntent>> actions)
	{
		if (_targetSteamId is not { } target)
		{
			return null;
		}

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		var row = rows.FirstOrDefault(r => r.SteamId == target);
		if (row is null || row.IsLocal || !row.InWorld)
		{
			Close();
			return null;
		}

		var page = new OnlineUiPageBuilder(ctx, actions, IdPrefix);
		DrawTargetSelector(ctx, page, rows, target);

		var menuActions = BuildActions(ctx, row);
		for (var index = 0; index < menuActions.Count; index++)
		{
			var action = menuActions[index];
			page.Button($"action.{index}", action.Label, () =>
			{
				action.Action();
				Close();
			});
		}

		return OnlineUiPanelModel.AtPoint(
			BuildContextTitle(ctx, row),
			_position.x,
			Screen.height - _position.y,
			Width,
			page.Rows);
	}

	/// <summary>The selector shown only when the click was near more than one member: the current pick is
	/// marked the way a tab marks the open page.</summary>
	private void DrawTargetSelector(OnlineUiContext ctx, OnlineUiPageBuilder page, IReadOnlyList<OnlineUiMemberRow> rows, ulong selected)
	{
		if (_candidateSteamIds.Count <= 1)
		{
			return;
		}

		var elements = new List<OnlineUiElementModel>(_candidateSteamIds.Count + 1)
		{
			page.LabelElement(ctx.T("member.select_target"), OnlineUiTextStyle.Muted, OnlineUiTheme.Muted),
		};
		foreach (var candidate in _candidateSteamIds)
		{
			elements.Add(page.ButtonElement(
				$"target.{candidate:X}",
				ContextTitle(ctx, rows, candidate),
				() => _targetSteamId = candidate,
				CandidateButtonWidth,
				selected: candidate == selected));
		}

		page.Row([.. elements]);
	}

	/// <summary>
	/// Which remote players the click was near. The remote clones have no colliders, so the candidates come
	/// from the authoritative entity positions projected through the camera and a radius rule of their own
	/// (<see cref="RemoteTargetPicker"/>); the picker is the Runtime's and is unchanged by the move onto the
	/// surface.
	/// </summary>
	private static bool TryFindRemoteCandidatesAt(Vector2 guiMouse, OnlineUiContext ctx, out IReadOnlyList<ulong> steamIds)
	{
		var camera = Camera.main;
		if (camera == null)
		{
			steamIds = [];
			return false;
		}

		const float radius = 48f;
		var screenTargets = new List<RemoteScreenTarget>();
		var remotePlayers = ctx.Entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
			if (remote.IsLocal || !ctx.Session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			var world = new Vector3(remote.Position.X, remote.Position.Y, 0f);
			var screen = camera.WorldToScreenPoint(world);
			if (screen.z < 0f)
			{
				continue;
			}

			var gui = new Vector2(screen.x, Screen.height - screen.y);
			screenTargets.Add(new RemoteScreenTarget(remote.SteamId, gui.x, gui.y));
		}

		var matches = RemoteTargetPicker.Find(screenTargets, guiMouse.x, guiMouse.y, radius);
		var result = new List<ulong>(matches.Count);
		foreach (var match in matches)
		{
			result.Add(match.SteamId);
		}

		steamIds = result;
		return result.Count > 0;
	}

	private static string ContextTitle(OnlineUiContext ctx, IReadOnlyList<OnlineUiMemberRow> rows, ulong steamId)
	{
		var row = rows.FirstOrDefault(r => r.SteamId == steamId);
		return row is null ? ctx.DisplayName(steamId) : BuildContextTitle(ctx, row);
	}

	private static string BuildContextTitle(OnlineUiContext ctx, OnlineUiMemberRow row)
		=> OnlineUiMemberLabel.FormatContextTitle(row.Name, row.IsDead, ctx.T("member.context_dead"));

	private static List<MenuAction> BuildActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var actions = new List<MenuAction>();

		// The medical panel is still a remote-player action from this menu. Its
		// eligibility follows the same line-of-sight gate as the other actions;
		// it must never appear alone when the target cannot be seen.
		if (row.CanViewMedical)
		{
			actions.Add(new MenuAction(ctx.T("member.open_medical"), () => ctx.OpenRemoteMedical?.Invoke(row.SteamId, ctx.DisplayName(row.SteamId))));
		}

		if (!row.CanSee)
		{
			return actions;
		}

		if (ctx.OpenRemoteBackpack is { } open)
		{
			actions.Add(new MenuAction(ctx.T("member.open_backpack"), () => open(row.SteamId, ctx.DisplayName(row.SteamId))));
		}

		if (row.CanCarry)
		{
			actions.Add(new MenuAction(ctx.T("member.carry"), () => ctx.CarryRemote?.Invoke(row.SteamId)));
		}

		if (row.CanPiggyback)
		{
			actions.Add(new MenuAction(ctx.T("member.piggyback"), () => ctx.PiggybackRemote?.Invoke(row.SteamId)));
		}

		if (row.CanCarryOnBack)
		{
			actions.Add(new MenuAction(ctx.T("member.carry_on_back"), () => ctx.CarryOnBackRemote?.Invoke(row.SteamId)));
		}

		if (row.CanDrop)
		{
			actions.Add(new MenuAction(ctx.T("member.drop"), () => ctx.DropCarried?.Invoke(row.SteamId)));
		}

		if (row.CanRequestDropFromCarrier)
		{
			actions.Add(new MenuAction(ctx.T("member.get_down"), () => ctx.DropCarried?.Invoke(ctx.Session.LocalSteamId)));
		}

		if (row.CanHeal)
		{
			actions.Add(new MenuAction(ctx.T("member.heal"), () => ctx.HealRemote?.Invoke(row.SteamId)));
		}

		if (row.CanPush)
		{
			actions.Add(new MenuAction(ctx.T("member.push"), () => ctx.PushRemote?.Invoke(row.SteamId)));
		}

		if (row.CanRecruit)
		{
			actions.Add(new MenuAction(ctx.T("member.recruit"), () => ctx.RecruitPlayer?.Invoke(row.SteamId)));
		}

		foreach (var item in row.TakeableItems)
		{
			var itemId = item.ItemId;
			var slot = item.SlotIndex;
			var instanceId = item.InstanceId;
			actions.Add(new MenuAction(ctx.F("member.take", itemId, slot), () => ctx.TakeItem?.Invoke(row.SteamId, instanceId)));
		}

		foreach (var item in row.HealItems)
		{
			var itemId = item.ItemId;
			var instanceId = item.InstanceId;
			actions.Add(new MenuAction(ctx.F("member.heal_with", itemId), () => ctx.HealWithItem?.Invoke(row.SteamId, instanceId)));
		}

		return actions;
	}

	private sealed class MenuAction
	{
		internal MenuAction(string label, Action action)
		{
			Label = label;
			Action = action;
		}

		internal string Label { get; }

		internal Action Action { get; }
	}
}
