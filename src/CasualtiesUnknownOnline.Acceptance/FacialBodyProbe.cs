using System;
using System.Collections.Generic;
using HarmonyLib;
using UDebug = UnityEngine.Debug;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// Names the facial that keeps updating after its body is gone, and stops it from spamming the log
/// in an acceptance build. The reading it exists for (2026-11-14): the host's log grew to 94 MB on a
/// per-frame <c>Body.get_peekingBehind</c> NullReferenceException — a <see cref="FacialExpression"/>
/// still ran while the <see cref="Body"/> it points at had been destroyed (a field read on a destroyed
/// object returns stale data, a property read throws). This prefix logs the facial's root and its ids
/// once per second and skips the update; the production build never carries it.
/// </summary>
internal static class FacialBodyProbe
{
	private static readonly Dictionary<int, int> LastLogMs = [];
	private static readonly AccessTools.FieldRef<FacialExpression, Body> BodyField =
		AccessTools.FieldRefAccess<FacialExpression, Body>("body");

	internal static void Install(Harmony harmony)
	{
		var target = AccessTools.Method(typeof(FacialExpression), "Update", Type.EmptyTypes);
		if (target == null)
		{
			UDebug.LogWarning("[FacialProbe] no FacialExpression.Update to patch; a body that outlives its facial stays unread.");
			return;
		}

		harmony.Patch(target, prefix: new HarmonyMethod(typeof(FacialBodyProbe), nameof(Prefix)));
	}

	private static bool Prefix(FacialExpression __instance)
	{
		Body body;
		try
		{
			body = BodyField(__instance);
		}
		catch (Exception error)
		{
			UDebug.LogWarning($"[FacialProbe] the facial's body field could not be read: {error.GetType().Name}");
			return true;
		}

		if (body != null)
		{
			return true;
		}

		var nowMs = (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
		var id = __instance.GetInstanceID();
		if (!LastLogMs.TryGetValue(id, out var last) || nowMs - last >= 1000)
		{
			LastLogMs[id] = nowMs;
			var root = __instance.transform != null ? __instance.transform.root.name : "<destroyed>";
			UDebug.LogWarning($"[FacialProbe] facial {id} on root '{root}' has no body (destroyed) — its Update is skipped.");
		}

		return false;
	}
}
