using System;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Thread-static capture state shared by the string and AudioClip
/// <c>Sound.Play</c> patches. The string overload internally calls the
/// AudioClip overload (Sound.cs:52-54), so when a string sound is already
/// reported by the string patch the AudioClip patch must not report the same
/// physical call a second time. The flag is set before the original string
/// method runs and cleared in the string patch's postfix.
/// </summary>
internal static class SoundCaptureContext
{
	[ThreadStatic]
	private static bool _skipAudioClipCapture;

	internal static bool SkipAudioClipCapture => _skipAudioClipCapture;

	internal static void SetSkipAudioClipCapture() => _skipAudioClipCapture = true;

	internal static void ClearSkipAudioClipCapture() => _skipAudioClipCapture = false;
}
