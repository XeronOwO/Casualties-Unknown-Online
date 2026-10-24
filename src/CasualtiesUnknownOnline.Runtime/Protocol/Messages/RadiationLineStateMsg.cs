using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the authoritative radiation-line state. The line's
/// <c>active</c> flag and <c>timeGone</c> descent are world state owned by the
/// host (the boundary must be the same on every side — a late joiner or a
/// guest whose own layer timer diverged would otherwise see a different
/// radiation boundary).
/// <para>
/// <see cref="Rate"/> is the speed the host's own line descends at, in world units per second: the native formula
/// multiplies the frame time by `(body.conscious ? 1 : 0.2) * 1.5`, and that factor is the LOCAL body's, so a member
/// continuing its own line between resends advanced at a different speed than the host and then snapped back (the
/// user's 2026-10-24 finding: the descent looked wrong on a member's screen). With the rate on the wire the member
/// extrapolates from <see cref="TimeGone"/> at the HOST's speed instead.
/// </para>
/// </summary>
[ProtoContract]
public sealed class RadiationLineStateMsg
{
	[ProtoMember(1)]
	public bool Active { get; set; }

	[ProtoMember(2)]
	public float TimeGone { get; set; }

	/// <summary>The host line's descent rate in world units per second (the native `deltaTime * (conscious ? 1 : 0.2) * 1.5` per second).</summary>
	[ProtoMember(3)]
	public float Rate { get; set; }
}
