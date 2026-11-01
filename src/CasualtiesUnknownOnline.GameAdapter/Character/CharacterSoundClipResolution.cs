using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Resolves the clip a character-sound report names, the way the side that PLAYED it did.
///
/// <para>
/// The wire carries the clip's NAME, and the replay used to load it as <c>Resources.Load&lt;AudioClip&gt;("Sounds/" + name)</c> — the path
/// <c>Sound.Play(string, …)</c> takes. That is right for the clips the game itself plays through the string overload (footsteps, the body one-shots
/// <c>stretch</c>/<c>dogshake</c>/<c>vomit1</c>/<c>vomit2</c>, medical clips), and it is wrong for the bark and pain vocalizations: those are picked
/// from the owner body's own <c>PantSound</c> arrays and played through the <c>AudioClip</c> overload (<c>PantSound.Bark</c>:
/// <c>Sound.Play(this.barkSounds.PickRandom&lt;AudioClip&gt;(), …)</c>), and their names are not resource paths. Measured 2026-11-01: the acting client
/// plays <c>pain1</c> for a bark, the peer never does, and <c>Resources.Load("Sounds/pain1")</c> answers null while every other family's clip resolves.
/// </para>
/// </summary>
internal static class CharacterSoundClipResolution
{
	/// <summary>
	/// The clip a report names, or null when this build cannot supply it. The resource path is tried first because it is what the game's own string
	/// overload uses; the owner's <see cref="PantSound"/> arrays are the second source, which is where the bark and pain clips actually live.
	/// </summary>
	internal static AudioClip? Resolve(string clip, Body? owner)
	{
		if (string.IsNullOrEmpty(clip)) // a report with no name has no clip
		{
			return null;
		}

		var resource = Resources.Load<AudioClip>("Sounds/" + clip);
		if (resource != null) // Unity object — ==
		{
			return resource;
		}

		if (owner == null) // Unity object — ==
		{
			return null;
		}

		var pant = owner.GetComponent<PantSound>();
		if (pant == null) // Unity object — ==
		{
			return null;
		}

		return Find(pant.painSounds, clip) ?? Find(pant.barkSounds, clip) ?? null;
	}

	private static AudioClip? Find(AudioClip[]? clips, string clip)
	{
		if (clips == null)
		{
			return null;
		}

		foreach (var candidate in clips)
		{
			if (candidate != null && candidate.name == clip) // Unity object — ==
			{
				return candidate;
			}
		}

		return null;
	}
}
