using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: explicit "enter the world" instruction. Sent at run-start
/// entry (the moment the host clicks start — the run baseline is captured at
/// that same moment), at the host's world entry (members that are not in the
/// world are invited back), and at handshake time when the host is already in
/// a world. Carries the entry kind so the guest starts the right run
/// immediately; the guest's generation boundary then waits for the baseline
/// before any random is consumed. It is also the run identity's announcement
/// point: this is the edge at which a NEW run legitimately begins, so the
/// epoch it carries is what the guest validates checkpoint chunk sets against
/// until the next instruction.
/// </summary>
[ProtoContract]
public sealed class WorldJoinMsg
{
	/// <summary>The host entered via StartTutorial (tutorial world) — the guest must follow via StartTutorial (it nulls runSettings itself, PreRunScript.cs:307-314).</summary>
	[ProtoMember(1)]
	public bool IsTutorial { get; set; }

	/// <summary>
	/// The kernel run epoch the host is serving at this instruction — the
	/// identity this member's checkpoint sets belong to. A set whose chunks
	/// carry another epoch is refused before any chunk is buffered, so a
	/// straggler from a previous run cannot move this side onto a run whose
	/// live streams it would then reject. Zero means "no announcement": the
	/// receiver keeps the identity it already holds (adopted from the first
	/// restored checkpoint when none was announced).
	/// </summary>
	[ProtoMember(2)]
	public ulong RunEpoch { get; set; }

	/// <summary>
	/// True when the checkpoint set carrying this run's baseline FOLLOWS this
	/// instruction — the host sends it right after the instruction, to the very
	/// member it invites. The receiver must hold its generation until that set
	/// restores: a member that stayed in the session still holds its PREVIOUS
	/// run's params, and its generation boundary only waits when it holds none,
	/// so without the hold it regenerates its own world before the host's
	/// baseline arrives. False on the handshake path, whose entry group
	/// (checkpoint included) is sent BEFORE this instruction — there the
	/// baseline is already in hand and that first set defines the identity.
	/// </summary>
	[ProtoMember(3)]
	public bool RunBaselineFollows { get; set; }
}
