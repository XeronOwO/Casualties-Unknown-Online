using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Either side → peer: a block was damaged at a world position (local compute,
/// remote verify/sync). Carries the block's drops when the damage BROKE the
/// block — the break and its drops are ONE message, so the host's arbitration
/// gives them one verdict: first-writer-wins (the accepted report's drops
/// register and materialize everywhere; the rejected report's drops are rolled
/// back on the breaker via ItemReject).
/// <para>
/// The key is the block CELL, not a world position: the cell is the identity
/// both sides agree on (they simulate the same layer, and the stamp below names
/// it), and every consumer of this message — the relay, the partial-damage
/// accounting, the break arbitration — is keyed by it. A world position would
/// have to be converted back on receipt, which is a conversion whose only
/// observable product was the cell.
/// </para>
/// </summary>
[ProtoContract]
public sealed class BlockDamagedMsg
{
	[ProtoMember(1)]
	public int X { get; set; }

	[ProtoMember(2)]
	public int Y { get; set; }

	/// <summary>
	/// The raw damage this side applied, for the paths that still speak raw
	/// damage: the break payload, and a partial-damage report whose
	/// <see cref="Contribution"/> is 0 (the sender could not account for the
	/// cell — the receiver then applies this raw value additively, exactly as it
	/// always has). <see cref="MetalBonus"/> rides beside it so the receiver's own
	/// <c>DamageBlock</c> applies the same ×10 metallic multiplier
	/// (WorldGeneration.cs:715).
	/// </summary>
	[ProtoMember(3)]
	public float Damage { get; set; }

	/// <summary>The drops the BREAK created on the damaging side (null/empty = damage only — the block survived).</summary>
	[ProtoMember(4)]
	public List<BlockDropEntryMsg>? Drops { get; set; }

	/// <summary>
	/// The damage source had <c>bonusMetal</c> set (e.g. the laser tool,
	/// Item.cs:4645). Only meaningful for the raw paths: a contribution is
	/// already expressed in the game's ACCUMULATED units, so its multiplier has
	/// been applied by the sender.
	/// </summary>
	[ProtoMember(5)]
	public bool MetalBonus { get; set; }

	/// <summary>
	/// Building/entity death drops caused by the same block break (a
	/// <c>requireGround</c> building loses its support block and dies in the
	/// same operation). Empty/null when the break did not destroy a building.
	/// These ride the same one-message/one-verdict path as block drops so the
	/// non-breaker sides never roll their own drop set and receive the full
	/// transient initial state (fresh flag, velocity, rotation, angular
	/// velocity).
	/// </summary>
	[ProtoMember(6)]
	public List<TrapDropEntryMsg>? BuildingDrops { get; set; }

	/// <summary>
	/// The world/layer generation this report belongs to (protocol 29). The
	/// position is LAYER-RELATIVE, so the receiver compares the stamp with its own
	/// generation before the report is arbitrated: a stale break report of the
	/// previous layer would otherwise claim a freshly generated cell, and only
	/// the generation tells that apart from a legitimate report whose air write
	/// was lost. Null = the sender has no committed run baseline yet (UNKNOWN).
	/// </summary>
	[ProtoMember(7)]
	public WorldGenerationMsg? Generation { get; set; }

	/// <summary>
	/// The damage THIS SENDER has cumulatively applied to that cell, in the
	/// game's own accumulated units (<c>BlockDamage.damage</c>, the metal
	/// multiplier already folded in) — the contribution the per-sender ledger
	/// accounts against, and the reason a sender's damage is counted exactly once
	/// whatever order live deltas and absolute re-reports arrive in
	/// (review/partial-damage-delta-report-overlap, protocol 32).
	/// <para>
	/// A live partial-damage report carries the cell's current cumulative value; a
	/// repeat carries the same value and is therefore a no-op; a later hit carries
	/// a higher one and the difference is what the receiver applies. 0 = this
	/// message is not a partial-damage contribution (a break payload, a drop
	/// re-report, or a sender that could not account for the cell): the receiver
	/// then falls back to applying <see cref="Damage"/> raw, additively, as it did
	/// before the ledger existed.
	/// </para>
	/// </summary>
	[ProtoMember(8)]
	public float Contribution { get; set; }
}
