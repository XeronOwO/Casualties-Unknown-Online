using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Where one surface's control views come from, and how a container's views stay aligned with the model it is
/// showing: one element's view built on the game's own row prefab, the reconcile that reuses a view whose kind
/// and id still match and rebuilds the one whose element changed identity, and the two things a view reports
/// about that prefab — a kind whose row could not be loaded at all, and a control the game left without anything
/// that accepts the pointer.
///
/// <para>
/// The window and the two panels each own one of these, which is why the log line names the surface it belongs
/// to and why the "reported once per kind" sets live here rather than in the caller: a box nobody can click is a
/// defect that must not pass silently, and a warning per frame is not a report. Keeping the reconcile in one
/// place is the point of the type — the three surfaces used to carry a copy each, and a copy is where the rule
/// that a control's identity decides its reuse drifts.
/// </para>
/// </summary>
internal sealed class OnlineUiViewSource
{
	private readonly ILogger _log;
	private readonly string _surface;
	private readonly OnlineUiControlTypography _typography;
	private readonly Action<OnlineUiIntent> _report;
	private readonly HashSet<OnlineUiElementKind> _reportedMissingPrefabs = [];
	private readonly HashSet<OnlineUiElementKind> _reportedPointerFixes = [];

	/// <param name="surface">What the log lines call this surface: "Online UI window", "Online UI panel".</param>
	internal OnlineUiViewSource(
		ILogger log,
		string surface,
		OnlineUiControlTypography typography,
		Action<OnlineUiIntent> report)
	{
		_log = log;
		_surface = surface;
		_typography = typography;
		_report = report;
	}

	/// <summary>
	/// Builds one element's view under <paramref name="parent"/>, reporting what the game's own prefab left
	/// broken the first time a kind shows it. Creation cannot fail: a control the game no longer ships falls back
	/// to a placeholder inside the view itself, so the slot always holds a view and the model's positions stay
	/// aligned with the list.
	/// </summary>
	internal OnlineUiControlView Create(OnlineUiElementModel element, Transform parent)
	{
		var view = OnlineUiControlView.Create(element, parent, _typography, _report);
		if (view.MissedGamePrefab && _reportedMissingPrefabs.Add(element.Kind))
		{
			_log.LogWarning(
				"{Surface}: the game's own row prefab for {Kind} could not be loaded — that control falls back to a plain placeholder.",
				_surface,
				element.Kind);
		}

		if (view.FixedPointerSurface && _reportedPointerFixes.Add(element.Kind))
		{
			_log.LogWarning(
				"{Surface}: the game's own row for {Kind} left its control without a pointer surface — CUO gave it one, because a box the player cannot click is not a control.",
				_surface,
				element.Kind);
		}

		return view;
	}

	/// <summary>
	/// Keeps <paramref name="views"/> aligned with <paramref name="elements"/>: a view whose kind and id still
	/// match is reused, a slot whose element changed identity is rebuilt, and views the model dropped are
	/// destroyed. A fresh view is created under <paramref name="parent"/> — which is where the caller's own
	/// layout will move it next — and the answer says whether the tree changed at all, so a steady frame does not
	/// dirty a layout.
	/// </summary>
	internal bool Sync(
		List<OnlineUiControlView> views,
		IReadOnlyList<OnlineUiElementModel> elements,
		Transform parent)
	{
		var changed = false;
		while (views.Count > elements.Count)
		{
			views[views.Count - 1].Destroy();
			views.RemoveAt(views.Count - 1);
			changed = true;
		}

		for (var index = 0; index < elements.Count; index++)
		{
			if (index >= views.Count)
			{
				views.Add(Create(elements[index], parent));
				changed = true;
				continue;
			}

			if (Matches(views[index], elements[index]))
			{
				continue;
			}

			views[index].Destroy();
			views.RemoveAt(index);
			views.Insert(index, Create(elements[index], parent));
			changed = true;
		}

		return changed;
	}

	/// <summary>Whether a live view can take the element over: same kind, and — for a control the model names —
	/// the same id. A control that changed identity is rebuilt, because its click must land on the action its id
	/// was registered under.</summary>
	private static bool Matches(OnlineUiControlView view, OnlineUiElementModel element) =>
		view.Kind == element.Kind
		&& (element.Id.Length == 0 || string.Equals(view.Id, element.Id, StringComparison.Ordinal));
}
