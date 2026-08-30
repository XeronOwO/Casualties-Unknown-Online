using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Kernel-shaped post-combat limb snapshot carried by enemy bite/lunge result
/// events. It mirrors the limb surface the Game Adapter captures after a local
/// bite or lunge, so the projection can rebuild the exact post-event limb state
/// on every peer without a legacy result wire.
/// </summary>
public sealed record EnemyCombatLimb
{
	public int Index { get; init; }

	public float SkinHealth { get; init; }

	public float MuscleHealth { get; init; }

	public bool Broken { get; init; }

	public bool Dislocated { get; init; }

	public bool Splinted { get; init; }

	public bool Infected { get; init; }

	public float InfectionAmount { get; init; }

	public float BleedAmount { get; init; }

	public float DisinfectionTime { get; init; }

	public float Pain { get; init; }

	public float DislocationTimer { get; init; }

	public float BoneHealTimer { get; init; }

	public bool BlockedBleeding { get; init; }

	public int Shrapnel { get; init; }

	public float FurBloodAmount { get; init; }

	public float BandageSlowAmount { get; init; }

	public float SkinHealAmount { get; init; }

	public bool Dismembered { get; init; }

	public IReadOnlyList<ItemComponentState> Components { get; init; } = [];

	public bool IsHead { get; init; }

	public bool IsVital { get; init; }
}
