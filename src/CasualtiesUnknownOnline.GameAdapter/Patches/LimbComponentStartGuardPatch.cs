using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// A limb component whose <c>Start</c> cannot register its wound-view icon must not keep running.
/// <para>
/// The game's splint and tourniquet both bind themselves to the wound view in <c>Start</c>
/// (<c>WoundView.view.AddImageToLimb(...)</c>) and write that icon from <c>Update</c>. When that registration fails — no view in
/// the scene, or a view whose limb arrays do not hold this body — <c>Start</c> throws, the icon field stays null, and <c>Update</c>
/// throws on every frame afterwards. Measured on a live client: one failed registration produced 28,901 copies of
/// <c>TourniquetScript.Update</c> NullReferenceException in a single log, and the client's own loop starved until it was stopped.
/// </para>
/// <para>
/// The registration cannot be retried from outside (it is <c>Start</c>, which Unity runs after any caller-side guard returns), so
/// these finalizers take the only place a failure is visible: disable the component that failed and swallow the exception. The
/// component keeps its fields — the limb state the authority's row carries is untouched — and stops behaving, which is exactly the
/// state/behaviour split the limb-component codec keeps for a display copy. Each kind reports its first failure once, so a silent
/// catch cannot hide the cause.
/// </para>
/// </summary>
internal static class LimbComponentStartGuardPatches
{
	private static readonly HashSet<string> Reported = [];

	/// <summary>The splint's own icon registration, guarded (its `Update` writes the icon `Start` failed to set).</summary>
	[HarmonyPatch(typeof(SplintLimb), "Start")]
	internal static class SplintStartPatch
	{
		[HarmonyFinalizer]
		private static Exception? Guard(Exception? __exception, SplintLimb __instance) =>
			Disable(__exception, __instance, nameof(SplintLimb));
	}

	/// <summary>The tourniquet's own icon registration, guarded (same shape, same consequence).</summary>
	[HarmonyPatch(typeof(TourniquetScript), "Start")]
	internal static class TourniquetStartPatch
	{
		[HarmonyFinalizer]
		private static Exception? Guard(Exception? __exception, TourniquetScript __instance) =>
			Disable(__exception, __instance, nameof(TourniquetScript));
	}

	private static Exception? Disable(Exception? exception, Behaviour component, string kind)
	{
		if (exception == null)
		{
			return null;
		}

		// Disabled, so Unity stops calling Update on a component whose icon was never registered: the exception is one frame's
		// event, and its consequence is the storm that follows it.
		component.enabled = false;
		if (Reported.Add(kind))
		{
			Debug.LogWarning($"[LimbComponent] {kind}.Start could not register its wound-view icon and the component was disabled: {exception.GetType().Name}: {exception.Message}. The limb's state is unaffected; the icon will not show this session.");
		}

		return null;
	}
}
