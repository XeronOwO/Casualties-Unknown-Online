using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel-shaped post-interaction limb snapshot. It carries both the game's
/// continuous limb state and the typed component payload needed to apply the
/// exact host-authoritative result on the target's local body. Like
/// <see cref="PlayerInteractionHealth"/>, it is not a terminal kernel fact and
/// is not reduced into the player table.
/// </summary>
public sealed record PlayerInteractionLimb
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
