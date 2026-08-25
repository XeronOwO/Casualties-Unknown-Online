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
/// </summary>
internal sealed class OnlineUiPlayerContextMenu
{
	private const float Width = 240f;
	private const float RowHeight = 28f;
	private const float TitleHeight = 30f;
	private const float SelectorHeight = 28f;

	private ulong? _targetSteamId;
	private IReadOnlyList<ulong> _candidateSteamIds = [];
	private Vector2 _position;
	private Rect _lastRect;

	internal bool IsOpen => _targetSteamId.HasValue;

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

		var actionCount = CountActions(row);
		var selectorHeight = _candidateSteamIds.Count > 1 ? SelectorHeight : 0f;
		var height = TitleHeight + selectorHeight + actionCount * RowHeight + 8f;
		var x = Mathf.Clamp(_position.x + 8f, 4f, Mathf.Max(4f, Screen.width - Width - 4f));
		var y = Mathf.Clamp(_position.y - 8f, 4f, Mathf.Max(4f, Screen.height - height - 4f));
		var rect = new Rect(x, y, Width, height);
		_lastRect = rect;

		OnlineUiTheme.DrawBackground(rect);
		GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f));
		GUILayout.Label(row.Name, OnlineUiTheme.Section(), GUILayout.Height(TitleHeight - 4f));

		if (_candidateSteamIds.Count > 1)
		{
			DrawTargetSelector(ctx);
		}

		// Always offer the read-only player page as the fallback interaction so
		// a right-click on any in-world remote has a visible menu even when no
		// carry/heal/recruit/take action is currently eligible.
		if (ActionButton(ctx.T("member.view_items"), () => OpenPlayerDetails(ctx, row.SteamId)))
		{
			Close();
		}

		if (row.CanCarry && ActionButton(ctx.T("member.carry"), () => ctx.CarryRemote?.Invoke(row.SteamId)))
		{
			Close();
		}

		if (row.CanPiggyback && ActionButton(ctx.T("member.piggyback"), () => ctx.PiggybackRemote?.Invoke(row.SteamId)))
		{
			Close();
		}

		if (row.CanDrop && ActionButton(ctx.T("member.drop"), () => ctx.DropCarried?.Invoke(row.SteamId)))
		{
			Close();
		}

		if (row.CanHeal && ActionButton(ctx.T("member.heal"), () => ctx.HealRemote?.Invoke(row.SteamId)))
		{
			Close();
		}

		if (row.CanUseItem && ActionButton(ctx.T("member.use"), () => ctx.UseItemOnRemote?.Invoke(row.SteamId)))
		{
			Close();
		}

		if (row.CanPush && ActionButton(ctx.T("member.push"), () => ctx.PushRemote?.Invoke(row.SteamId)))
		{
			Close();
		}

		if (row.CanRecruit && ActionButton(ctx.T("member.recruit"), () => ctx.RecruitPlayer?.Invoke(row.SteamId)))
		{
			Close();
		}

		foreach (var item in row.TakeableItems)
		{
			var itemId = item.ItemId;
			var slot = item.SlotIndex;
			var instanceId = item.InstanceId;
			if (ActionButton(ctx.F("member.take", itemId, slot), () => ctx.TakeItem?.Invoke(row.SteamId, instanceId)))
			{
				Close();
			}
		}

		foreach (var item in row.HealItems)
		{
			var itemId = item.ItemId;
			var instanceId = item.InstanceId;
			if (ActionButton(ctx.F("member.heal_with", itemId), () => ctx.HealWithItem?.Invoke(row.SteamId, instanceId)))
			{
				Close();
			}
		}

		foreach (var item in row.UseItems)
		{
			var itemId = item.ItemId;
			var instanceId = item.InstanceId;
			if (ActionButton(ctx.F("member.use_with", itemId), () => ctx.UseItemOnRemoteWith?.Invoke(row.SteamId, instanceId)))
			{
				Close();
			}
		}

		GUILayout.EndArea();
	}

	private void DrawTargetSelector(OnlineUiContext ctx)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("member.select_target"), OnlineUiTheme.MutedLabel(), GUILayout.Width(54f));
		foreach (var candidate in _candidateSteamIds)
		{
			if (GUILayout.Button(ctx.DisplayName(candidate), OnlineUiTheme.Button(), GUILayout.Height(SelectorHeight - 4f)))
			{
				_targetSteamId = candidate;
			}
		}

		GUILayout.EndHorizontal();
	}

	private static int CountActions(OnlineUiMemberRow row)
	{
		var count = 1; // the always-available "view items" fallback
		if (row.CanCarry)
		{
			count++;
		}

		if (row.CanPiggyback)
		{
			count++;
		}

		if (row.CanDrop)
		{
			count++;
		}

		if (row.CanHeal)
		{
			count++;
		}

		if (row.CanUseItem)
		{
			count++;
		}

		if (row.CanPush)
		{
			count++;
		}

		if (row.CanRecruit)
		{
			count++;
		}

		count += row.TakeableItems.Count;
		count += row.HealItems.Count;
		count += row.UseItems.Count;
		return count;
	}

	private static void OpenPlayerDetails(OnlineUiContext ctx, ulong steamId)
	{
		ctx.State.Visible = true;
		ctx.State.Page = OnlineUiPage.Players;
		ctx.State.ExpandedMember = steamId;
	}

	private static bool ActionButton(string label, System.Action action)
	{
		var clicked = GUILayout.Button(label, OnlineUiTheme.Button(), GUILayout.Height(RowHeight));
		if (clicked)
		{
			action();
		}

		return clicked;
	}
}
