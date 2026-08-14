using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An enemy bit a player (SpiderHandler.CheckForLimbDamage → DamageLimb): the
/// victim's local body already applied the damage (local compute); this event
/// carries the post-bite terminal state — the bitten limb plus the body's
/// venom/adrenaline/happiness — so every peer applies the exact same state
/// (exact rebuild, never a delta). Bidirectional: guest → host report (the
/// victim is the reporter); host → guest broadcast relay (the victim is
/// <see cref="VictimSteamId"/>). The 1 Hz character snapshot stays the fallback.
/// </summary>
[ProtoContract]
public sealed class EnemyBiteMsg
{
	/// <summary>The bitten player (the reporter's own SteamId for a guest report).</summary>
	[ProtoMember(1)]
	public ulong VictimSteamId { get; set; }

	/// <summary>The bitten limb's post-bite terminal state (Index = the limb in Body.limbs).</summary>
	[ProtoMember(2)]
	public CharacterLimbMsg Limb { get; set; } = new();

	/// <summary>Post-bite body venom (accumulated — the bite adds the enemy's venomAmount).</summary>
	[ProtoMember(3)]
	public float VenomTotal { get; set; }

	/// <summary>Post-bite body adrenaline (the bite adds 75f).</summary>
	[ProtoMember(4)]
	public float Adrenaline { get; set; }

	/// <summary>Post-bite body happiness (the bite subtracts the enemy's happinessLoss).</summary>
	[ProtoMember(5)]
	public float Happiness { get; set; }
}
