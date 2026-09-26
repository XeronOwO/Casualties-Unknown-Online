using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Replays the impact presentation the authority's native call made, through
/// the game's OWN calls rather than a re-implementation of them: the same
/// <c>drop</c> clip, the landing block's step sound as THIS world picks it (the
/// native body calls <c>WorldGeneration.world.RandomStepSound</c> for the block
/// under the CONTACT POINT, Item.cs:243-244; the event carries no contact point,
/// so the receiver runs the same pick for the block under the item's position —
/// the same block except on an edge), and the same <c>DustMini</c> object at the
/// same position. A plush
/// replays its own squeak clip — the exact one, by the index the event carries,
/// because the receiver's copy rolls its own index.
///
/// <para>
/// The replay is a PRESENTATION: it runs under <c>RemoteApply</c>, the scope the
/// capture patches refuse, so nothing replayed here can ever be reported back
/// as this member's own action.
/// </para>
/// </summary>
internal static class WorldItemImpactReplay
{
	internal static void Play(ItemImpactMsg msg)
	{
		var presentation = ItemImpactPresentation.Of(msg.Kind);
		var position = new Vector2(msg.Position.X, msg.Position.Y);

		using var scope = CallContext.Enter(CallContext.Origin.RemoteApply);

		if (presentation.PlaysDropClip)
		{
			Sound.Play("drop", position, false, true, null, 1f, 1f, false, false);
		}

		if (presentation.PlaysLandingStepClip && WorldGeneration.world != null) // Unity object — ==
		{
			Sound.Play(
				WorldGeneration.world.RandomStepSound(WorldGeneration.world.GetBlockInfo(WorldGeneration.world.GetBlock(position)).stepsound),
				position, false, true, null, 1f, 1f, false, false);
		}

		if (presentation.SpawnsDust)
		{
			Object.Instantiate(Resources.Load<GameObject>("DustMini"), position, Quaternion.identity);
		}

		if (presentation.PlaysOwnClip)
		{
			PlayOwnClip(msg.SoundIndex, position);
		}
	}

	/// <summary>The plush's own squeak, played on the receiver's copy of that plush (the game's own clip collection, the exact index the authority played).</summary>
	private static void PlayOwnClip(byte soundIndex, Vector2 position)
	{
		var plush = FindPlush(position);
		if (plush == null) // Unity object — ==
		{
			return;
		}

		if (soundIndex < plush.possibleSounds.Length && plush.possibleSounds[soundIndex] != null) // Unity object — ==
		{
			Sound.Play(plush.possibleSounds[soundIndex], position, false, false, null, 1f, 1f, false, false);
			return;
		}

		plush.Squeak();
	}

	private static PlushScript? FindPlush(Vector2 position)
	{
		foreach (var hit in Physics2D.OverlapPointAll(position))
		{
			if (hit.GetComponent<PlushScript>() is { } plush) // Unity object — ==
			{
				return plush;
			}
		}

		return null;
	}
}
