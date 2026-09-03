using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Optional frame-by-frame sprite animation for a custom item visual. The DTO
/// carries ordered Unity resource paths only; the Game Adapter resolves the
/// sprites and drives the renderer. No Unity or game type crosses
/// Abstractions.
/// </summary>
[DataContract]
public sealed class ModItemSpriteAnimation
{
	/// <summary>
	/// Ordered Unity resource paths of the animation frames. The first valid
	/// frame is also used as the static fallback when the animation cannot be
	/// applied.
	/// </summary>
	[DataMember(Order = 1)]
	public List<string> FramePaths { get; set; } = [];

	/// <summary>Playback speed in frames per second. Must be positive.</summary>
	[DataMember(Order = 2)]
	public float FramesPerSecond { get; set; } = 12f;

	/// <summary>When true, the frame sequence repeats; otherwise it stops on the last frame.</summary>
	[DataMember(Order = 3)]
	public bool Loop { get; set; } = true;
}
