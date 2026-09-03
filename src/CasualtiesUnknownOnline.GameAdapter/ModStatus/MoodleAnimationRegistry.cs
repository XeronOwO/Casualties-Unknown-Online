using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.ModStatus;

/// <summary>
/// Local mapping from synthetic moodle icon keys to resolved frame animations.
/// <see cref="ModStatusMoodleProjection"/> registers the key when it feeds a
/// custom animated moodle, and the <c>Moodle.Start</c> patch looks the key up to
/// drive the vanilla moodle UI image. This is a local presentation registry
/// only: no wire message and no Unity type in Abstractions.
/// </summary>
internal static class MoodleAnimationRegistry
{
	private static readonly Dictionary<string, AnimationData> Animations = [];

	internal static void Register(string iconKey, Sprite[] frames, float framesPerSecond, bool loop)
	{
		if (string.IsNullOrWhiteSpace(iconKey) || frames == null || frames.Length == 0) // Unity object — ==
		{
			return;
		}

		Animations[iconKey] = new AnimationData(frames, framesPerSecond, loop);
	}

	internal static bool TryGet(string iconKey, out Sprite[] frames, out float framesPerSecond, out bool loop)
	{
		if (Animations.TryGetValue(iconKey ?? string.Empty, out var animation))
		{
			frames = animation.Frames;
			framesPerSecond = animation.FramesPerSecond;
			loop = animation.Loop;
			return true;
		}

		frames = [];
		framesPerSecond = 0f;
		loop = true;
		return false;
	}

	private sealed class AnimationData(Sprite[] frames, float framesPerSecond, bool loop)
	{
		internal Sprite[] Frames { get; } = frames;
		internal float FramesPerSecond { get; } = framesPerSecond;
		internal bool Loop { get; } = loop;
	}
}
