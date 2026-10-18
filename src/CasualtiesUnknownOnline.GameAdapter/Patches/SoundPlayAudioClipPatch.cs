using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Character footstep / landing-impact / PantSound vocalization capture on the
/// AudioClip <c>Sound.Play</c> overload. Footsteps and landing impacts are the
/// high-frequency, continuously occurring character sounds the string-only
/// patch cannot see: material/water steps are <c>RandomStepSound</c> AudioClips
/// (Body.cs:1175/1180), landing impacts are <c>bodyFallN</c> AudioClips
/// (Body.cs:2729-2737), and PantSound pain/bark are one-shot AudioClips
/// (PantSound.cs:22-30, PantSound.cs:55-67). A call-identity scope around
/// <c>Body.FootStep</c> / <c>Body.HandleGroundedState</c> / <c>PantSound.Update</c>
/// / <c>PantSound.Bark</c> is the discriminator; the capture is a thin adapter
/// and the classification is the pure <c>CharacterSoundPolicy</c>.
/// The string overload routes through this same physical method (Sound.cs:52-54);
/// when the string patch already reported the call it sets
/// <see cref="SoundCaptureContext.SkipAudioClipCapture"/> so this patch never
/// double-reports it. Inside <see cref="HostVitalsPass"/> the prefix returns
/// false: a direct AudioClip play on the host's simulated clone is the owner's
/// sound and is suppressed before the play.
/// </summary>
[HarmonyPatch(typeof(Sound), "Play",
	[typeof(AudioClip), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform),
		typeof(float), typeof(float), typeof(bool), typeof(bool)])]
internal static class SoundPlayAudioClipPatch
{
	private static bool Prefix(AudioClip clip, Vector2 pos, bool twoDimensional, Transform follow, float volume)
	{
		// The host's simulated clone runs the owner's native stages; an AudioClip play there is the
		// owner's sound, not the host's. Returning false both suppresses the play and skips the
		// capture — the string overload has the same guard, and this one closes the direct overload.
		if (HostVitalsPass.IsActive)
		{
			return false;
		}

		if (CallContext.IsWithin(CallContext.Origin.RemoteApply) || SoundCaptureContext.SkipAudioClipCapture)
		{
			return true;
		}

		if (clip == null || string.IsNullOrEmpty(clip.name)) // Unity object — ==
		{
			return true;
		}

		var origin = CallContext.Current switch
		{
			CallContext.Origin.CharacterFootstep => CharacterSoundPolicy.Origin.Footstep,
			CallContext.Origin.CharacterLandingImpact => CharacterSoundPolicy.Origin.LandingImpact,
			CallContext.Origin.CharacterVocalization => CharacterSoundPolicy.Origin.Pain,
			CallContext.Origin.CharacterBark => CharacterSoundPolicy.Origin.Bark,
			_ => CharacterSoundPolicy.Origin.None,
		};
		if (origin == CharacterSoundPolicy.Origin.None)
		{
			return true;
		}

		var resource = origin == CharacterSoundPolicy.Origin.Footstep && FootstepSoundCapture.StepPathPrefix is { } prefix
			? prefix + "/" + clip.name
			: clip.name;

		if (CharacterSoundPolicy.Classify(origin, resource) is { } kind)
		{
			PatchBridge.Impl?.OnCharacterSound(kind, resource, pos, volume, follow != null, twoDimensional, 0f);
		}

		return true;
	}
}
