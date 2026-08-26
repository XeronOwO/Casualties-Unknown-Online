using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Reports the player world-blood decals the native <c>BleedParticle.Update</c>
/// creates (BleedParticle.cs:18-55). The patch does not replace or alter the
/// native visual: it observes the private dying-particle loop and simulates the
/// same modulo counter so it knows exactly which dying particle triggered the
/// source's ground/wall instantiation. Only the LOCAL player's own body is
/// reported (remote render clones carry <see cref="RemoteBodyDriver"/> and are
/// skipped), so each player reports its own world blood exactly once and the
/// host relays it to the other members.
/// </summary>
[HarmonyPatch(typeof(BleedParticle), "Update")]
internal static class BleedParticleWorldBloodPatch
{
	private sealed class BloodObservation
	{
		internal byte BeforeSpawned;
		internal int Every;
		internal int[] DyingIndices = [];
		internal ParticleSystem.Particle[] Particles = [];
	}

	private static bool Prefix(BleedParticle __instance, out BloodObservation? __state)
	{
		__state = null;
		if (IsRemoteClone(__instance))
		{
			// Remote clones must not create their own local decals/drip sounds:
			// the owner's report is the only world-blood presentation source, so
			// the peers never double-spawn at the same bleeding body.
			return false;
		}

		if (!ShouldReport(__instance))
		{
			return true;
		}

		// This event is scoped to blood decals. BleedParticle is also used by
		// vomit variants (every=1, wallvomit/blockvomit); those are not part of
		// this backlog item and are left owner-local.
		if (Traverse.Create(__instance).Field("vomit").GetValue<bool>())
		{
			return true;
		}

		var particleSystem = Traverse.Create(__instance).Field("part").GetValue<ParticleSystem>();
		var particles = Traverse.Create(__instance).Field("particles").GetValue<ParticleSystem.Particle[]>();
		if (particleSystem == null || particles == null) // Unity object — ==
		{
			return true;
		}

		var count = particleSystem.GetParticles(particles);
		if (count == 0)
		{
			return true;
		}

		var every = Traverse.Create(__instance).Field("every").GetValue<int>();
		if (every <= 0)
		{
			return true;
		}

		var dying = new List<int>();
		for (var i = 0; i < count; i++)
		{
			if (particles[i].remainingLifetime <= 0.02f)
			{
				dying.Add(i);
			}
		}

		if (dying.Count == 0)
		{
			return true;
		}

		__state = new BloodObservation
		{
			BeforeSpawned = Traverse.Create(__instance).Field("spawned").GetValue<byte>(),
			Every = every,
			DyingIndices = [.. dying],
			Particles = particles,
		};
		return true;
	}

	private static void Postfix(BloodObservation? __state)
	{
		if (__state == null)
		{
			return;
		}

		var spawned = __state.BeforeSpawned;
		foreach (var index in __state.DyingIndices)
		{
			if (index < 0 || index >= __state.Particles.Length)
			{
				continue;
			}

			spawned++;
			if (spawned < __state.Every)
			{
				continue;
			}

			spawned = 0;
			var particle = __state.Particles[index];
			var position = new Vector2(particle.position.x, particle.position.y);
			var ground = IsGroundDecal(position);
			if (ground)
			{
				position = DecalGroundPosition(position);
			}

			PatchBridge.Impl?.OnWorldBloodSpawn(position, ground);
		}
	}

	private static bool IsRemoteClone(BleedParticle particle)
	{
		var body = particle.GetComponentInParent<Body>();
		if (body == null) // Unity object — == (enemy/other bleed is not a player clone)
		{
			return false;
		}

		return body.GetComponent<RemoteBodyDriver>() != null;
	}

	private static bool ShouldReport(BleedParticle particle)
	{
		if (PatchBridge.Impl is not { IsSessionActive: true })
		{
			return false;
		}

		var body = particle.GetComponentInParent<Body>();
		if (body == null) // Unity object — == (enemy/other bleed is not player world blood)
		{
			return false;
		}

		// Remote clones (both the host's simulated guest bodies and guests'
		// render proxies) are not the owner's own reporting surface.
		return body.GetComponent<RemoteBodyDriver>() == null;
	}

	private static bool IsGroundDecal(Vector2 particlePosition)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return false;
		}

		var probe = particlePosition + Vector2.down * 0.8f;
		return WorldGeneration.world.GetBlock(probe) > 0
			&& WorldGeneration.world.GetBlock(probe + Vector2.up) == 0;
	}

	private static Vector2 DecalGroundPosition(Vector2 particlePosition)
	{
		var probe = particlePosition + Vector2.down * 0.8f;
		return WorldGeneration.world.BlockToWorldPos(WorldGeneration.world.WorldToBlockPos(probe));
	}
}
