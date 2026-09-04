using System.Reflection;
using UnityEngine;
using System;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The render-proxy wall-slide presentation surface. A remote clone never runs
/// <c>Body.HandleGroundedState</c>, so the private <c>slidingLeft</c> /
/// <c>slidingRight</c> flags, the <c>Wall</c> animator clip and the wall-slide
/// particle/audio are absent. This helper owns the cached field access and the
/// continuous particle/audio fallback so <c>SessionStatePump</c> /
/// <c>BodyPatches</c> stay thin and the side has one focused place to update
/// when the game's internals change.
/// </summary>
internal static class WallSlidePresentation
{
	private static readonly FieldInfo SlidingLeftField =
		typeof(Body).GetField("slidingLeft", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Body.slidingLeft not found.");

	private static readonly FieldInfo SlidingRightField =
		typeof(Body).GetField("slidingRight", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Body.slidingRight not found.");

	private static readonly FieldInfo SlideSourceField =
		typeof(Body).GetField("slideSource", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Body.slideSource not found.");

	/// <summary>Re-assert the synced wall-slide direction onto the clone's private body fields before HandleVisuals reads them.</summary>
	internal static void Apply(Body body, bool slidingLeft, bool slidingRight)
	{
		SlidingLeftField.SetValue(body, slidingLeft);
		SlidingRightField.SetValue(body, slidingRight);
	}

	/// <summary>
	/// Mirror the native wall-slide particle/audio latch (Body.cs:2610-2632)
	/// using the clone's own synced grounded/velocity facts. Display-only, no
	/// simulation state.
	/// </summary>
	internal static void UpdateEffects(Body body, bool slidingLeft, bool slidingRight)
	{
		var sliding = slidingLeft || slidingRight;
		var source = SlideSourceField.GetValue(body) as AudioSource;
		if (sliding && !body.grounded && body.rb.velocity.y < -2f)
		{
			if (body.wallSlideParticle != null && !body.wallSlideParticle.isPlaying) // Unity object — ==
			{
				body.wallSlideParticle.Play();
			}

			if (source != null && !source.isPlaying)
			{
				source.Play();
			}

			if (source != null)
			{
				source.volume = Mathf.Min(1f, source.volume + Time.deltaTime * 10f);
			}
		}
		else
		{
			if (body.wallSlideParticle != null && body.wallSlideParticle.isPlaying) // Unity object — ==
			{
				body.wallSlideParticle.Stop();
			}

			if (source != null)
			{
				source.volume -= Time.deltaTime * 10f;
				if (source.volume <= 0f)
				{
					source.Stop();
				}
			}
		}
	}
}
