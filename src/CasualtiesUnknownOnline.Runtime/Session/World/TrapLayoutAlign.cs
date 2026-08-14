using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The guest's layout alignment result: what to materialize (the host
/// has it, the local world does not — or no same-kind match within the radius)
/// and what to destroy (the LOCAL entries the host's layout does not claim —
/// indices into the local list, so the adapter resolves the live components).</summary>
internal readonly struct TrapLayoutAlignment
{
	internal IReadOnlyList<TrapLayoutEntryMsg> ToSpawn { get; init; }

	internal IReadOnlyList<int> ToDestroy { get; init; }
}

/// <summary>
/// The trap-layout alignment judgment — pure (extracted from the adapter's
/// application so the matrix is unit-testable): every host entry claims the
/// closest same-kind local entity within the match radius (greedy nearest
/// neighbour); the unmatched host entries spawn, the unmatched local entities
/// destroy. The radius is <see cref="MatchRadius"/> — the same radius the
/// position-key replay's entity lookup uses (TrapEffectApplier.FindTrap), so
/// an entity the replay could not reach is never "kept" by the alignment.
/// </summary>
internal static class TrapLayoutAlign
{
	internal const float MatchRadius = 3f;

	internal static TrapLayoutAlignment Align(IReadOnlyList<TrapLayoutEntryMsg> hostLayout, IReadOnlyList<TrapLayoutEntryMsg> localLayout)
	{
		var toSpawn = new List<TrapLayoutEntryMsg>();
		var toDestroy = new List<int>();
		var localClaimed = new bool[localLayout.Count];

		foreach (var host in hostLayout)
		{
			var best = -1;
			var bestDistance = MatchRadius;
			for (var i = 0; i < localLayout.Count; i++)
			{
				if (localClaimed[i] || localLayout[i].Kind != host.Kind)
				{
					continue;
				}

				var dx = localLayout[i].X - host.X;
				var dy = localLayout[i].Y - host.Y;
				var distance = (float)System.Math.Sqrt((dx * dx) + (dy * dy));
				if (distance < bestDistance)
				{
					best = i;
					bestDistance = distance;
				}
			}

			if (best >= 0)
			{
				localClaimed[best] = true; // kept — the position key resolves to it
			}
			else
			{
				toSpawn.Add(host);
			}
		}

		for (var i = 0; i < localLayout.Count; i++)
		{
			if (!localClaimed[i])
			{
				toDestroy.Add(i);
			}
		}

		return new TrapLayoutAlignment { ToSpawn = toSpawn, ToDestroy = toDestroy };
	}
}
