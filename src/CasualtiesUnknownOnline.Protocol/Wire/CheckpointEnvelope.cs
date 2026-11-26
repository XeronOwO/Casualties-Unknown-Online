using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Host -> guest: one checkpoint chunk during join/reconnect or gap recovery.
/// </summary>
[ProtoContract]
public sealed class CheckpointEnvelope
{
	[ProtoMember(1)]
	public EnvelopeHeader Header { get; set; } = new();

	[ProtoMember(2)]
	public WireCheckpoint Checkpoint { get; set; } = new();

	/// <summary>
	/// Host: this set is the baseline the host is sending AT a run change, not
	/// the answer to a join/reconnect. The receiver adopts the set's run identity
	/// before its usual "another run's set is refused" check, and treats a
	/// successful restore as the run-adoption edge (the same edge a baseline
	/// restored without any instruction raises), so the previous run's world is
	/// left before the new run's first batch is applied.
	/// </summary>
	[ProtoMember(3)]
	public bool Rebaseline { get; set; }
}
