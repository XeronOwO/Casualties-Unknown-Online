using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One frame-by-frame sprite animation of a custom item visual. This is the
/// contract a consumer reads: <see cref="IModItemVisual.BaseSpriteAnimation"/>
/// and its two siblings are interface-typed, so a mod that computes a frame list
/// hands over its own implementation instead of filling in the framework's.
///
/// <see cref="ModItemSpriteAnimation"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed. Partial customisation is composition — an
/// implementation hands back a filled default for the members it does not touch —
/// never inheritance from the data class, which stays <c>sealed</c>.
///
/// The frame list is read through <see cref="ModDeclarationCollections"/>: null
/// means "no frames", which the Game Adapter refuses rather than renders.
/// </summary>
public interface IModItemSpriteAnimation
{
	/// <summary>
	/// Ordered Unity resource paths of the animation frames. The first valid
	/// frame is also used as the static fallback when the animation cannot be
	/// applied.
	/// </summary>
	List<string> FramePaths { get; }

	/// <summary>Playback speed in frames per second. Must be positive.</summary>
	float FramesPerSecond { get; }

	/// <summary>When true, the frame sequence repeats; otherwise it stops on the last frame.</summary>
	bool Loop { get; }
}
