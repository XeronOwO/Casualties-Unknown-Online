using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One frame-by-frame animation for a custom moodle icon. This is the contract a
/// consumer reads: <see cref="IModMoodleDefinition.IconAnimation"/> is
/// interface-typed, so a mod that computes a frame list hands over its own
/// implementation instead of filling in the framework's.
///
/// <see cref="ModMoodleAnimation"/> is the framework's ready-made implementation:
/// use it when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back a
/// filled default for the members it does not touch — never inheritance from the
/// data class, which stays <c>sealed</c>.
///
/// The shape matches <see cref="IModItemSpriteAnimation"/> because both are a
/// frame list and a playback rule; they stay two contracts because a moodle icon
/// and an item sprite are two features and either may grow a member the other
/// must not have.
///
/// The frame list is read through <see cref="ModDeclarationCollections"/>: null
/// means "no frames", which the Game Adapter refuses rather than renders.
/// </summary>
public interface IModMoodleAnimation
{
	/// <summary>
	/// Ordered Unity resource paths of the animation frames. The first valid
	/// frame is also used as the static icon fallback.
	/// </summary>
	List<string> FramePaths { get; }

	/// <summary>Playback speed in frames per second. Must be positive.</summary>
	float FramesPerSecond { get; }

	/// <summary>When true, the frame sequence repeats; otherwise it stops on the last frame.</summary>
	bool Loop { get; }
}
