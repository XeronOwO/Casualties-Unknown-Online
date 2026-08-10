using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A world entity event (trap trigger / mechanism state transition).
/// Bidirectional, BlockPlaced semantics: the triggering side applies the full
/// local effect (original game behaviour) and reports; the host applies the
/// event to its own world (the TrapEffectApplier — an exploding mine destroys
/// the host's copy and rolls the host-side drops) and relays to the other
/// members (the source excluded, it already applied locally); every receiving
/// side replays the event on the entity at <see cref="Position"/> (position-keyed —
/// world entities are generated deterministically, so both sides have the same
/// object at the same place). The message carries no effect parameters: the
/// receiver derives them from <see cref="Kind"/> (shared compile-time constants).
/// </summary>
[ProtoContract]
public sealed class EntityEventMsg
{
	[ProtoMember(1)]
	public EntityEventKind Kind { get; set; }

	/// <summary>The entity's world position (the trap's own transform).</summary>
	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>
	/// Kind-specific data. LifepodHeatChanged = heatState 0/1/2, ScrapEaterProgress
	/// = the progress %, BatteryInserted = the slot. The geyser's liquid type
	/// used to ride here but is now a generation-time initial condition
	/// (GeyserStateSnapshot, #128). 0 when the kind carries nothing.
	/// </summary>
	[ProtoMember(3)]
	public byte Extra { get; set; }
}
