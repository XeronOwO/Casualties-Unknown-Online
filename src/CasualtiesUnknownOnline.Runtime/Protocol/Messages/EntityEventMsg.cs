using ProtoBuf;
using System.Collections.Generic;

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

	/// <summary>
	/// How long ago the event fired — the late-joiner snapshot's replay anchor.
	/// 0 on live events (the transition just happened); the trap-state snapshot
	/// carries the true elapsed so a state-family replay lands at the CURRENT
	/// state instead of re-running the whole animation (a door opened minutes
	/// ago replays as already open, not as opening — and one that opened 4 s
	/// ago replays mid-animation).
	/// </summary>
	[ProtoMember(4)]
	public float ElapsedSeconds { get; set; }

	/// <summary>
	/// Items dropped by the destructive trap/building-entity death on the
	/// triggering side. Empty on ordinary/live replay events; when present the
	/// host folds these spawns into the same atomic trap composite.
	/// </summary>
	[ProtoMember(5)]
	public List<TrapDropEntryMsg> Drops { get; set; } = [];
}
