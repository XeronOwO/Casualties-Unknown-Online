using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A carry relation change projected from a committed kernel carry batch. A
/// zero <see cref="CarriedSteamId"/> means the carrier is no longer carrying
/// anyone. This is now a local presentation/mirror event payload, not a wire
/// message; the wire fact rides <c>KernelEnvelope</c> as a committed batch.
/// </summary>
[ProtoContract]
public sealed class PlayerCarryStateMsg
{
	/// <summary>The SteamId of the player doing the carrying.</summary>
	[ProtoMember(1)]
	public ulong CarrierSteamId { get; set; }

	/// <summary>The SteamId of the carried player, or 0 when this carrier released everyone.</summary>
	[ProtoMember(2)]
	public ulong CarriedSteamId { get; set; }
}
