using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Two duties on the string <c>Sound.Play</c> overload:
/// 1. Spawn landing sound (WorldGeneration.cs:3673, the "lifePodHit" impact —
/// played at the very end of generation, right before the start gate freezes
/// the world). The AudioSource itself is unaffected by timeScale (Sound.cs
/// uses PlayOneShot), but the sound rolls into the frozen wait — the user
/// heard it as a slowed, lowered groan. Defer it: the gate release plays it,
/// together with everyone else's release.
/// 2. Character action-sound capture: inside the Body.Attack / ThrowItem /
/// TryExertSound / FootStep / PantSound.Update / PantSound.TryGrowl
/// call-identity scopes, every real string sound is
/// reported with its EXACT clip. Block hit sounds are excluded by the innermost
/// DamageBlockOrigin scope (WorldGeneration.DamageBlock opens it around the
/// native roll), and replays are excluded by the RemoteApply scope — the
/// patch is a thin adapter, the classification is the pure
/// CharacterSoundPolicy.
/// The string overload internally calls the AudioClip overload, so after a
/// successful string report the AudioClip patch must skip the same physical
/// call (SoundCaptureContext flag, cleared in the postfix).
/// </summary>
[HarmonyPatch(typeof(Sound), "Play",
	[typeof(string), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform),
		typeof(float), typeof(float), typeof(bool), typeof(bool)])]
internal static class SoundPlayPatch
{
	private static bool Prefix(string clip, Vector2 pos, bool twoDimensional, Transform follow, float volume)
	{
		// In a live session the spawn landing sound is deferred until the
		// start-gate release: it plays at the very end of generation, before
		// the gate freezes — WaitingForReady is not yet true there, so the
		// session-wide check is the reliable window (solo play is untouched).
		if (clip == "lifePodHit" && PatchBridge.Impl is { IsSessionActive: true, IsReplayingLifePodSound: false })
		{
			PatchBridge.Impl?.DeferLifePodSound();
			return false; // deferred until the start gate releases
		}

		if (CallContext.Current != CallContext.Origin.RemoteApply)
		{
			var origin = CallContext.Current switch
			{
				CallContext.Origin.CharacterAttack => CharacterSoundPolicy.Origin.Attack,
				CallContext.Origin.CharacterThrow => CharacterSoundPolicy.Origin.Throw,
				CallContext.Origin.CharacterExert => CharacterSoundPolicy.Origin.Exert,
				CallContext.Origin.CharacterFootstep => CharacterSoundPolicy.Origin.Footstep,
				CallContext.Origin.CharacterLandingImpact => CharacterSoundPolicy.Origin.LandingImpact,
				CallContext.Origin.CharacterVocalization => CharacterSoundPolicy.Origin.Yawn,
				CallContext.Origin.CharacterGrowl => CharacterSoundPolicy.Origin.Growl,
				_ => CharacterSoundPolicy.Origin.None,
			};
			if (CharacterSoundPolicy.Classify(origin, clip) is { } kind)
			{
				PatchBridge.Impl?.OnCharacterSound(kind, clip, pos, volume, follow != null, twoDimensional, 0f);
				SoundCaptureContext.SetSkipAudioClipCapture();
			}
		}

		return true;
	}

	private static void Postfix() => SoundCaptureContext.ClearSkipAudioClipCapture();
}
