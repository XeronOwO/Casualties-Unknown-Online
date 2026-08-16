using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE player-character action sound (attack swing / throw swing / exert) —
/// the dedicated trigger event, star semantics. The owner's local simulation
/// already played the sound (the patch captured it from the real
/// <c>Sound.Play</c> call, so the clip string is the EXACT chosen clip); the
/// host applies it to the owner's remote clone and relays to the other
/// members (source excluded). One sound = one message, reliable: a lost event
/// is acceptable presentation degradation (there is no persistent state to
/// heal), but the event never rides the snapshot stream.
///
/// <see cref="FollowOwner"/> means the sound followed the owner's body
/// transform on the source side (attack swing + exert — <c>follow</c> is the
/// body); the receiver parents the replayed sound to the owner's render clone
/// when it exists, otherwise it falls back to <see cref="Position"/>.
/// <see cref="TwoDimensional"/> carries the source call's spatial mode.
/// Pitch is not carried: every covered call passes pitch 1 with pitch-shift
/// enabled (the receiver's local <c>Random</c> may pick a different shift —
/// sound variation is presentation, not state).
/// </summary>
[ProtoContract]
public sealed class CharacterSoundMsg
{
	/// <summary>The acting player's SteamId (stamped by the reporter; the host stamps its own on broadcast).</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	[ProtoMember(2)]
	public CharacterSoundKind Kind { get; set; }

	/// <summary>The exact sound resource name the source played ("BSSwing3", "laser", "exert2", …).</summary>
	[ProtoMember(3)]
	public string Clip { get; set; } = "";

	/// <summary>The source call's world position (the owner body / hit point).</summary>
	[ProtoMember(4)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The source call's volume (attack swing = AttackInfo.volume, exert = Body.TryExertSound's argument).</summary>
	[ProtoMember(5)]
	public float Volume { get; set; } = 1f;

	/// <summary>True when the source call followed the owner's body transform.</summary>
	[ProtoMember(6)]
	public bool FollowOwner { get; set; }

	/// <summary>True when the source call was 2D (exert is; the swing sounds are 3D).</summary>
	[ProtoMember(7)]
	public bool TwoDimensional { get; set; }
}
