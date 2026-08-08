using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Spawn landing sound (WorldGeneration.cs:3673, the "lifePodHit" impact —
/// played at the very end of generation, right before the start gate freezes
/// the world). The AudioSource itself is unaffected by timeScale (Sound.cs
/// uses PlayOneShot), but the sound rolls into the frozen wait — the user
/// heard it as a slowed, lowered groan. Defer it: the gate release plays it,
/// together with everyone else's release.
/// </summary>
[HarmonyPatch(typeof(Sound), "Play",
	new Type[] { typeof(string), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform),
		typeof(float), typeof(float), typeof(bool), typeof(bool) })]
internal static class SoundPlayPatch
{
	private static bool Prefix(string clip)
	{
		// In a live session the spawn landing sound is deferred until the
		// start-gate release: it plays at the very end of generation, before
		// the gate freezes — WaitingForReady is not yet true there, so the
		// session-wide check is the reliable window (solo play is untouched).
		if (clip == "lifePodHit" && PatchBridge.Impl is { IsSessionActive: true })
		{
			PatchBridge.Impl?.DeferLifePodSound();
			return false; // deferred until the start gate releases
		}

		return true;
	}
}
