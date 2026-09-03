using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Optional frame-by-frame animation for a custom moodle icon. The DTO carries
/// ordered Unity resource paths only; the Game Adapter resolves the frames and
/// drives the vanilla moodle UI image. No Unity or game type crosses
/// Abstractions.
/// </summary>
[DataContract]
public sealed class ModMoodleAnimation
{
	/// <summary>
	/// Ordered Unity resource paths of the animation frames. The first valid
	/// frame is also used as the static icon fallback.
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
