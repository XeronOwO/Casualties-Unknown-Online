using System;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Thread-static step-surface prefix for the AudioClip <c>Sound.Play</c>
/// capture inside <c>Body.FootStep</c>. The material/water footstep clips are
/// loaded from <c>Sounds/footstep/&lt;step&gt;/…</c> (WorldGeneration.cs:132),
/// so the captured <c>AudioClip.name</c> alone is not loadable by the string
/// overload. The Body.FootStep patch knows the step surface (<c>Water</c> or
/// <c>standingOn.stepsound</c>) and stores the matching resource prefix here;
/// the AudioClip patch turns <c>"footstep/Rock/" + clip.name</c> into the
/// message's <c>Clip</c>, which the receiver replays via
/// <c>Sound.Play(string)</c> → <c>Resources.Load("Sounds/" + clip)</c>.
/// </summary>
internal static class FootstepSoundCapture
{
	[ThreadStatic]
	private static string? _stepPathPrefix;

	internal static string? StepPathPrefix => _stepPathPrefix;

	internal static void SetStepPathPrefix(string? prefix) => _stepPathPrefix = prefix;

	internal static void ClearStepPathPrefix() => _stepPathPrefix = null;
}
