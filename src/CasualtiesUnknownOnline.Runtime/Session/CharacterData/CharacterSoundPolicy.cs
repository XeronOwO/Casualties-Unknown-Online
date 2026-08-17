using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Pure classification for the player-character sound capture (no Unity):
/// maps a call-identity scope + the clip the game just played to a
/// <see cref="CharacterSoundKind"/>. The GameAdapter's <c>Sound.Play</c>
/// patches run this inside the call-identity scopes opened around
/// <c>Body.Attack</c> / <c>Body.ThrowItem</c> / <c>Body.TryExertSound</c> /
/// <c>Body.FootStep</c> / <c>Body.HandleGroundedState</c>; any block hit
/// sound that fires during an attack is excluded before this policy sees it,
/// because <c>WorldGeneration.DamageBlock</c> opens its own innermost
/// <c>DamageBlockOrigin</c> scope.
/// </summary>
public static class CharacterSoundPolicy
{
	/// <summary>The call-identity scopes the sound capture understands (mirrors the GameAdapter's CallContext origins).</summary>
	public enum Origin
	{
		None = 0,
		Attack = 1,
		Throw = 2,
		Exert = 3,
		Footstep = 4,
		LandingImpact = 5,
	}

	/// <summary>
	/// The kind to report, or null when the call is not a reportable character
	/// sound. An empty clip is never reportable (a null <c>AudioClip</c> load
	/// plays nothing). Inside <c>Body.Attack</c>, every non-empty string sound
	/// that reaches this policy (block sounds excluded by the innermost damage
	/// scope) is either the swing sound or the exertion sound — the exertion
	/// prefix is the discriminator. Inside the footstep scope every non-empty
	/// clip is a step (the fallback <c>BSFootstepN</c> or a material path under
	/// <c>Sounds/footstep/…</c>); inside the landing-impact scope every non-empty
	/// clip is a landing impact (the <c>bodyFallN</c> clips).
	/// </summary>
	public static CharacterSoundKind? Classify(Origin origin, string clip)
	{
		if (string.IsNullOrEmpty(clip))
		{
			return null;
		}

		return origin switch
		{
			Origin.Exert => CharacterSoundKind.Exert,
			Origin.Throw => CharacterSoundKind.ThrowSwing,
			Origin.Attack => IsExertClip(clip) ? CharacterSoundKind.Exert : CharacterSoundKind.AttackSwing,
			Origin.Footstep => CharacterSoundKind.Footstep,
			Origin.LandingImpact => CharacterSoundKind.LandingImpact,
			_ => null,
		};
	}

	private static bool IsExertClip(string clip) =>
		clip.StartsWith("exert", StringComparison.Ordinal);
}
