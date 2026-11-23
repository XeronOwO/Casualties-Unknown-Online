using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Replays a player's one-shot world-blood decal on a receiver-side world. The
/// owner side already ran the native <c>BleedParticle.Update</c> decal branch;
/// this helper reproduces the same prefab/position/randomised visual on the
/// peer, including the ground decal's chunk-aware <see cref="GroundBlood"/>
/// component and the 120 s lifetime. The random scale/flip/alpha/rotation are
/// intentionally receiver-side (presentation-only; the meaningful sync fact is
/// the decal position and ground/wall kind).
/// </summary>
internal static class WorldBloodReplay
{
	internal static void Play(WorldBloodSpawnMsg msg)
	{
		var prefabName = msg.Ground ? "Special/blockblood" : "wallblood";
		var prefab = Resources.Load<GameObject>(prefabName);
		if (prefab == null) // Unity object — ==
		{
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var decal = Object.Instantiate(prefab);
			decal.transform.position = new Vector3(msg.Position.X, msg.Position.Y, 0f);
			Sound.Play("drip" + Random.Range(1, 25).ToString(), decal.transform.position, false, true, null, 0.3f, 1f, false, false);

			if (msg.Ground)
			{
				// GroundBlood.Start listens on its chunk's ChunkUpdated event, so the component only
				// belongs on a decal whose chunk this receiver already has: a decal replayed ahead of
				// the receiver's generation throws inside the game's own Start (measured 2026-11-24,
				// the member's log: NullReferenceException at GroundBlood.Start while a continue
				// replayed decals). The decal itself still lands; only the chunk listener is skipped.
				if (ChunkIsReady(msg.Position))
				{
					decal.AddComponent<GroundBlood>();
				}

				decal.transform.localScale = new Vector2(Random.Range(0.7f, 1.3f), Random.Range(0.94f, 1.06f));
				var sprite = decal.GetComponent<SpriteRenderer>();
				if (sprite != null) // Unity object — ==
				{
					sprite.flipX = Random.value > 0.5f;
					sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, Random.Range(0.2f, 0.8f));
				}
			}
			else
			{
				decal.transform.localScale = new Vector2(Random.Range(0.3f, 1.2f), Random.Range(0.3f, 1.2f));
				var sprite = decal.GetComponent<SpriteRenderer>();
				if (sprite != null) // Unity object — ==
				{
					sprite.color = new Color(1f, 1f, 1f, Random.Range(0.4f, 1f));
				}
			}

			Object.Destroy(decal, 120f);
		}
	}

	/// <summary>Does this receiver's world already carry the chunk the decal sits in (its listener array entry)?</summary>
	private static bool ChunkIsReady(NetVector2Msg position)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return false;
		}

		var cell = world.WorldToBlockPos(new Vector2(position.X, position.Y)) / WorldGeneration.CHUNKSIZE;
		var chunks = world.ChunkUpdated;
		return chunks is not null
			&& cell.x >= 0 && cell.y >= 0
			&& cell.x < chunks.GetLength(0) && cell.y < chunks.GetLength(1)
			&& chunks[cell.x, cell.y] is not null;
	}
}

