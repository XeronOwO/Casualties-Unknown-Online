using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Optional frame-by-frame sprite animation for a custom item visual. The DTO
/// carries ordered Unity resource paths only; the Game Adapter resolves the
/// sprites and drives the renderer. No Unity or game type crosses
/// Abstractions.
/// </summary>
public sealed class ModItemSpriteAnimation : IModItemSpriteAnimation
{
	/// <summary>
	/// Ordered Unity resource paths of the animation frames. The first valid
	/// frame is also used as the static fallback when the animation cannot be
	/// applied.
	/// </summary>
	public List<string> FramePaths
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Playback speed in frames per second. Must be positive.</summary>
	public float FramesPerSecond { get; set; } = 12f;

	/// <summary>When true, the frame sequence repeats; otherwise it stops on the last frame.</summary>
	public bool Loop { get; set; } = true;
}
