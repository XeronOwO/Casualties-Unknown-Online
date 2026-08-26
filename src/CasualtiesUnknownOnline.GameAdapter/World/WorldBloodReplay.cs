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
				decal.AddComponent<GroundBlood>();
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
}
