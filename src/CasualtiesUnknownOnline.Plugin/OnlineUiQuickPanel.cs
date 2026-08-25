using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The standalone player-interaction quick panel. It is the always-available
/// alternative to the transient in-world right-click context menu and the
/// full Players page: a compact docked panel shows the selected in-world
/// remote player's status, inventory and every eligible co-op interaction
/// (carry/piggyback/drop/heal/use/push/recruit/take). The panel never opens
/// the full Online window and can be toggled from a configurable session
/// hotkey.
/// </summary>
internal sealed class OnlineUiQuickPanel
{
	private const float Width = 340f;
	private const float Height = 420f;
	private const float CloseButtonSize = 24f;
	private const float TargetButtonHeight = 24f;

	private bool _visible;
	private ulong? _target;
	private Rect _rect;

	internal bool IsVisible => _visible;

	internal bool Contains(Vector2 point) => _rect.Contains(point);

	internal void Toggle() => _visible = !_visible;

	internal void Close() => _visible = false;

	internal void Draw(OnlineUiContext ctx)
	{
		if (!_visible)
		{
			return;
		}

		var evt = Event.current;
		if (evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
		{
			Close();
			evt.Use();
			return;
		}

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		var candidates = BuildCandidates(ctx, rows);
		var local = ctx.Entities.LocalPlayer.Position;
		_target = QuickPanelTargetPicker.Resolve(_target, local.X, local.Y, candidates);

		var rect = new Rect(Screen.width - Width - 16f, Screen.height - Height - 16f, Width, Height);
		_rect = rect;
		OnlineUiTheme.DrawBackground(rect);
		GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));

		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("quick.title"), OnlineUiTheme.Section());
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("×", OnlineUiTheme.CloseButton(), GUILayout.Width(CloseButtonSize), GUILayout.Height(CloseButtonSize)))
		{
			Close();
		}

		GUILayout.EndHorizontal();

		if (_target is not { } target)
		{
			GUILayout.Label(ctx.T("quick.no_players"), OnlineUiTheme.MutedLabel());
			GUILayout.EndArea();
			return;
		}

		var targetRow = rows.First(row => row.SteamId == target);
		DrawTargetSelector(ctx, rows, target);
		GUILayout.Space(4f);
		OnlineUiMemberListDrawer.Draw(ctx, [targetRow]);

		GUILayout.EndArea();
	}

	private static IReadOnlyList<QuickPanelTargetCandidate> BuildCandidates(OnlineUiContext ctx, IReadOnlyList<OnlineUiMemberRow> rows)
	{
		var candidates = new List<QuickPanelTargetCandidate>();
		foreach (var row in rows)
		{
			if (row.IsLocal || !row.InWorld)
			{
				continue;
			}

			var remote = ctx.Entities.GetRemotePlayer(row.SteamId);
			if (remote is null)
			{
				continue;
			}

			candidates.Add(new QuickPanelTargetCandidate(row.SteamId, remote.Position.X, remote.Position.Y));
		}

		return candidates;
	}

	private void DrawTargetSelector(OnlineUiContext ctx, IReadOnlyList<OnlineUiMemberRow> rows, ulong selected)
	{
		var remoteRows = rows.Where(row => !row.IsLocal && row.InWorld).ToList();
		if (remoteRows.Count <= 1)
		{
			return;
		}

		GUILayout.Label(ctx.T("quick.target"), OnlineUiTheme.MutedLabel());
		if (remoteRows.Count <= 4)
		{
			GUILayout.BeginHorizontal();
			foreach (var row in remoteRows)
			{
				DrawTargetButton(ctx, row, selected);
			}

			GUILayout.EndHorizontal();
			return;
		}

		foreach (var row in remoteRows)
		{
			DrawTargetButton(ctx, row, selected);
		}
	}

	private void DrawTargetButton(OnlineUiContext ctx, OnlineUiMemberRow row, ulong selected)
	{
		var isSelected = row.SteamId == selected;
		var label = isSelected ? $"{row.Name} ✓" : row.Name;
		if (GUILayout.Button(label, OnlineUiTheme.Tab(isSelected), GUILayout.Height(TargetButtonHeight)))
		{
			_target = row.SteamId;
		}
	}
}
