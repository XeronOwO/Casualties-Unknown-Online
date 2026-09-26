using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The standalone player-interaction quick panel. It is the always-available
/// alternative to the transient in-world right-click context menu and the
/// full Players page: a compact docked panel shows the selected in-world
/// remote player's status and every eligible co-op interaction
/// (carry/piggyback/drop/heal/use/push/recruit/take). The panel never opens
/// the full Online window; it can be toggled from a configurable session
/// hotkey.
///
/// <para>
/// Since S5 it draws nothing: it builds a <see cref="OnlineUiPanelModel"/> for the game's own controls on
/// CUO's surface (docked in the canvas's bottom-right corner, which is the rect the IMGUI panel occupied),
/// and the click that comes back is dispatched to the action registered under the id the control carried.
/// The one gesture that stays here is ESC, because the frame's keys are read by the IMGUI pass the console
/// still uses — the panel owns no keys of its own.
/// </para>
/// </summary>
internal sealed class OnlineUiQuickPanel
{
	/// <summary>The panel's width in the surface's canvas units — the IMGUI panel's own rect.</summary>
	internal const float Width = 340f;

	/// <summary>The width one target button asks for; the surface wraps the row by the Runtime's rule when
	/// the candidates do not fit one line.</summary>
	private const float TargetButtonWidth = 96f;

	/// <summary>Every id this panel registers is namespaced, so a control of the panel and a control of the
	/// window (or of the context menu) can never be the same intent.</summary>
	private const string IdPrefix = "quick.";

	private bool _visible;
	private ulong? _target;

	internal bool IsVisible => _visible;

	internal void Toggle() => _visible = !_visible;

	internal void Close() => _visible = false;

	/// <summary>
	/// ESC closes the panel. It is read in the IMGUI pass, not by the panel's controls, and only while the
	/// command console is closed — the console is the other surface with a claim on the key.
	/// </summary>
	internal void HandleInput()
	{
		if (!_visible)
		{
			return;
		}

		var evt = Event.current;
		if (evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
		{
			Plugin.Logger.LogInfo("Quick panel ESC consumed; closing panel.");
			Close();
			evt.Use();
		}
	}

	/// <summary>
	/// The panel's model for this frame, or null while it is hidden. The rows are the same ones the Players
	/// page renders — the panel's single target's member card, built by
	/// <see cref="OnlineUiMemberListDrawer.Build"/> — so the eligibility rules are answered once for both
	/// surfaces, and the target selector plus the local "get down" row are the panel's own.
	/// </summary>
	internal OnlineUiPanelModel? Build(OnlineUiContext ctx, Dictionary<string, Action<OnlineUiIntent>> actions)
	{
		if (!_visible)
		{
			return null;
		}

		var page = new OnlineUiPageBuilder(ctx, actions, IdPrefix);
		actions[OnlineUiControlIds.QuickPanelClose] = _ => Close();

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		var candidates = BuildCandidates(ctx, rows);
		var local = ctx.Entities.LocalPlayer.Position;
		_target = QuickPanelTargetPicker.Resolve(_target, local.X, local.Y, candidates);

		if (_target is not { } target)
		{
			page.Muted(ctx.T("quick.no_players"));
			return OnlineUiPanelModel.Docked(ctx.T("quick.title"), OnlineUiControlIds.QuickPanelClose, Width, page.Rows);
		}

		DrawTargetSelector(ctx, page, rows, target);
		var targetRow = rows.First(row => row.SteamId == target);
		OnlineUiMemberListDrawer.Build(ctx, page, [targetRow]);

		var localRow = rows.FirstOrDefault(r => r.IsLocal);
		if (localRow is { CanRequestDrop: true, InWorld: true })
		{
			page.Button("get_down", ctx.T("member.get_down"), () => ctx.DropCarried?.Invoke(localRow.SteamId));
		}

		return OnlineUiPanelModel.Docked(ctx.T("quick.title"), OnlineUiControlIds.QuickPanelClose, Width, page.Rows);
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

	/// <summary>
	/// The target selector: one button per in-world remote member, the current target marked the way a tab
	/// marks the open page. The IMGUI panel laid four candidates out on one line and the rest one per line;
	/// the surface wraps them by the Runtime's own rule instead, which is the same shape one rule further
	/// down.
	/// </summary>
	private void DrawTargetSelector(OnlineUiContext ctx, OnlineUiPageBuilder page, IReadOnlyList<OnlineUiMemberRow> rows, ulong selected)
	{
		var remoteRows = rows.Where(row => !row.IsLocal && row.InWorld).ToList();
		if (remoteRows.Count <= 1)
		{
			return;
		}

		page.Muted(ctx.T("quick.target"));
		var elements = new List<OnlineUiElementModel>(remoteRows.Count);
		foreach (var row in remoteRows)
		{
			elements.Add(page.ButtonElement(
				$"target.{row.SteamId:X}",
				row.Name,
				() => _target = row.SteamId,
				TargetButtonWidth,
				selected: row.SteamId == selected));
		}

		page.Row([.. elements]);
	}
}
