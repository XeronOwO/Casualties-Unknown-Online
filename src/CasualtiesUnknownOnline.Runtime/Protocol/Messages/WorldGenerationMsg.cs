using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The world/layer generation a direct world report belongs to (protocol 29).
/// The identity is the kernel run baseline's own: the session/restore epoch (a
/// per-process counter that advances on session end and on a restore, so no fact
/// can cross a session) plus the run's layer index (bumped for every
/// generation, so no fact can cross a descent; a new run inside one epoch
/// restarts it at 0).
/// <para>
/// Direct world reports are keyed by block cell or world position, and those
/// keys are LAYER-RELATIVE: after a descent the same <c>(x, y)</c> addresses a
/// freshly generated block. A receiver therefore cannot tell a stale report of
/// the previous generation from a legitimate one by looking at the report
/// alone — the kernel envelope has carried <c>RunEpoch</c> all along, and this
/// is the same vocabulary for the <c>NetMsg</c> world channel, which has no
/// envelope of its own. A report with no stamp (this peer has no committed run
/// baseline yet) is compared as UNKNOWN, never as fresh.
/// </para>
/// </summary>
[ProtoContract]
public sealed class WorldGenerationMsg
{
	[ProtoMember(1)]
	public ulong RunEpoch { get; set; }

	[ProtoMember(2)]
	public int LayerIndex { get; set; }
}
