using System;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Character footstep / landing-impact capture on the AudioClip <c>Sound.Play</c>
/// overload. Footsteps and landing impacts are the high-frequency, continuously
/// occurring character sounds the string-only patch cannot see: material/water
/// steps are <c>RandomStepSound</c> AudioClips (Body.cs:1175/1180) and landing
/// impacts are <c>bodyFallN</c> AudioClips (Body.cs:2729-2737). A call-identity
/// scope around <c>Body.FootStep</c> / <c>Body.HandleGroundedState</c> is the
/// discriminator; the capture is a thin adapter and the classification is the
/// pure <c>CharacterSoundPolicy</c>.
/// The string overload routes through this same physical method (Sound.cs:52-54);
/// when the string patch already reported the call it sets
/// <see cref="SoundCaptureContext.SkipAudioClipCapture"/> so this patch never
/// double-reports it.
/// </summary>
[HarmonyPatch(typeof(Sound), "Play",
	new Type[] { typeof(AudioClip), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform),
		typeof(float), typeof(float), typeof(bool), typeof(bool) })]
internal static class SoundPlayAudioClipPatch
{
	private static void Prefix(AudioClip clip, Vector2 pos, bool twoDimensional, Transform follow, float volume)
	{
		if (CallContext.Current == CallContext.Origin.RemoteApply || SoundCaptureContext.SkipAudioClipCapture)
		{
			return;
		}

		if (clip == null || string.IsNullOrEmpty(clip.name)) // Unity object — ==
		{
			return;
		}

		var origin = CallContext.Current switch
		{
			CallContext.Origin.CharacterFootstep => CharacterSoundPolicy.Origin.Footstep,
			CallContext.Origin.CharacterLandingImpact => CharacterSoundPolicy.Origin.LandingImpact,
			_ => CharacterSoundPolicy.Origin.None,
		};
		if (origin == CharacterSoundPolicy.Origin.None)
		{
			return;
		}

		var resource = origin == CharacterSoundPolicy.Origin.Footstep && FootstepSoundCapture.StepPathPrefix is { } prefix
			? prefix + "/" + clip.name
			: clip.name;

		if (CharacterSoundPolicy.Classify(origin, resource) is { } kind)
		{
			PatchBridge.Impl?.OnCharacterSound(kind, resource, pos, volume, follow != null, twoDimensional, 0f);
		}
	}
}
