using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;
using System;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The in-world right-click player interaction menu. It is opened by the
/// overlay when the user right-clicks near a remote player's projected body
/// position (the remote clones deliberately have no colliders — physics-off
/// render proxies — so the menu uses the authoritative entity stream positions
/// instead of Physics2D hits). The menu reuses the same projected member rows
/// and action delegates as the Players page, so it never duplicates the
/// eligibility rules.
/// </summary>
internal sealed class OnlineUiPlayerContextMenu
{
	private const float Width = 240f;
	private const float RowHeight = 28f;
	private const float TitleHeight = 30f;
	private const float SelectorHeight = 28f;
	private const float FramePadding = 6f;
	private const float Gap = 4f;
	private const float TargetLabelWidth = 54f;
	private const float BottomPadding = 8f;

	private ulong? _targetSteamId;
	private IReadOnlyList<ulong> _candidateSteamIds = [];
	private Vector2 _position;
	private Rect _lastRect;
	private static GUIStyle? _menuButton;

	internal bool IsOpen => _targetSteamId.HasValue;

	internal Rect Bounds => _lastRect;

	internal void Open(ulong steamId, IReadOnlyList<ulong> candidates, Vector2 screenPosition)
	{
		_targetSteamId = steamId;
		_candidateSteamIds = [.. candidates];
		_position = screenPosition;
	}

	internal void Close()
	{
		_targetSteamId = null;
		_candidateSteamIds = [];
	}

	internal bool Contains(Vector2 point) => _lastRect.Contains(point);

	internal void Draw(OnlineUiContext ctx)
	{
		if (_targetSteamId is not { } target)
		{
			return;
		}

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		var row = rows.FirstOrDefault(r => r.SteamId == target);
		if (row is null || row.IsLocal || !row.InWorld)
		{
			Close();
			return;
		}

		var actions = BuildActions(ctx, row);
		var contentWidth = Width - (FramePadding * 2f);
		var buttonStyle = MenuButton();
		var titleStyle = OnlineUiTheme.Section();

		var titleHeight = Mathf.Max(TitleHeight - 4f, titleStyle.CalcHeight(new GUIContent(row.Name), contentWidth));
		var selectorHeight = MeasureTargetSelectorHeight(ctx, contentWidth);
		var actionsHeight = 0f;
		foreach (var action in actions)
		{
			actionsHeight += ButtonHeight(action.Label, buttonStyle, contentWidth);
		}

		var contentHeight = titleHeight + selectorHeight + actionsHeight;
		var height = contentHeight + (FramePadding * 2f) + BottomPadding;
		var x = Mathf.Clamp(_position.x + 8f, 4f, Mathf.Max(4f, Screen.width - Width - 4f));
		var y = Mathf.Clamp(_position.y - 8f, 4f, Mathf.Max(4f, Screen.height - height - 4f));
		var rect = new Rect(x, y, Width, height);
		_lastRect = rect;

		OnlineUiTheme.DrawBackground(rect);

		var left = rect.x + FramePadding;
		var yCursor = rect.y + FramePadding;
		GUI.Label(new Rect(left, yCursor, contentWidth, titleHeight), row.Name, titleStyle);
		yCursor += titleHeight;

		if (_candidateSteamIds.Count > 1)
		{
			yCursor = DrawTargetSelector(ctx, left, yCursor, contentWidth, selectorHeight, buttonStyle);
		}

		foreach (var action in actions)
		{
			var rowHeight = ButtonHeight(action.Label, buttonStyle, contentWidth);
			if (GUI.Button(new Rect(left, yCursor, contentWidth, rowHeight), action.Label, buttonStyle))
			{
				action.Action();
				Close();
				return;
			}

			yCursor += rowHeight;
		}
	}

	private float DrawTargetSelector(
		OnlineUiContext ctx,
		float left,
		float y,
		float width,
		float height,
		GUIStyle buttonStyle)
	{
		var count = _candidateSteamIds.Count;
		var available = width - TargetLabelWidth - (Gap * (count - 1));
		var buttonWidth = Mathf.Max(40f, available / count);

		GUI.Label(new Rect(left, y, TargetLabelWidth, height), ctx.T("member.select_target"), OnlineUiTheme.MutedLabel());

		var bx = left + TargetLabelWidth;
		foreach (var candidate in _candidateSteamIds)
		{
			var label = ctx.DisplayName(candidate);
			var rowHeight = Mathf.Max(SelectorHeight - 4f, buttonStyle.CalcHeight(new GUIContent(label), buttonWidth) + 4f);
			if (GUI.Button(new Rect(bx, y, buttonWidth, rowHeight), label, buttonStyle))
			{
				_targetSteamId = candidate;
			}

			bx += buttonWidth + Gap;
		}

		return y + height;
	}

	private float MeasureTargetSelectorHeight(OnlineUiContext ctx, float width)
	{
		if (_candidateSteamIds.Count <= 1)
		{
			return 0f;
		}

		var count = _candidateSteamIds.Count;
		var available = width - TargetLabelWidth - (Gap * (count - 1));
		var buttonWidth = Mathf.Max(40f, available / count);
		var buttonStyle = MenuButton();
		var height = SelectorHeight;
		foreach (var candidate in _candidateSteamIds)
		{
			var label = ctx.DisplayName(candidate);
			height = Mathf.Max(height, buttonStyle.CalcHeight(new GUIContent(label), buttonWidth) + 4f);
		}

		return height;
	}

	private static float ButtonHeight(string label, GUIStyle style, float width)
	{
		var textHeight = style.CalcHeight(new GUIContent(label), width);
		return Mathf.Max(RowHeight, textHeight + 4f);
	}

	private static List<MenuAction> BuildActions(OnlineUiContext ctx, OnlineUiMemberRow row)
	{
		var actions = new List<MenuAction>();

		// The medical panel is read-only display, not a physical interaction, so
		// it remains available even when line-of-sight hides the action buttons.
		if (row.CanViewMedical)
		{
			actions.Add(new MenuAction(ctx.T("member.open_medical"), () => ctx.OpenMedical?.Invoke(row.SteamId)));
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

	private static GUIStyle MenuButton()
	{
		_menuButton ??= new GUIStyle(OnlineUiTheme.Button())
		{
			margin = new RectOffset(0, 0, 0, 0),
		};

		return _menuButton;
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
