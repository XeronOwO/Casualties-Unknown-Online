using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → all authoritative result of a cross-player push/shove. The host
/// computes the force delta from the pusher's authoritative entity position to
/// the target's; every side receives the same committed fact. The target's own
/// client applies the native ragdoll + velocity on its local body, the pusher's
/// client applies the stamina/temperature cost, and every side plays the
/// one-shot push sound at the target position. The target's subsequent motion
/// continues to ride the existing 20 Hz player state stream as the fallback.
/// </summary>
[ProtoContract]
public sealed class PlayerPushResultMsg
{
	/// <summary>The player who initiated the push.</summary>
	[ProtoMember(1)]
	public ulong PusherSteamId { get; set; }

	/// <summary>The player who is pushed.</summary>
	[ProtoMember(2)]
	public ulong TargetSteamId { get; set; }

	/// <summary>Committed velocity delta applied to the target (the host-computed push force).</summary>
	[ProtoMember(3)]
	public float ForceX { get; set; }

	/// <summary>Committed velocity delta applied to the target.</summary>
	[ProtoMember(4)]
	public float ForceY { get; set; }
}
