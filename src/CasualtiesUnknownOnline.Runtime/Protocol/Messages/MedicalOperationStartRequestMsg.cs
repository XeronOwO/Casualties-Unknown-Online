using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: begin a remote medical operation. The host reserves the item
/// (and optional target limb), accepts or rejects, and replies with a
/// <see cref="MedicalOperationStartAckMsg"/>. No medical state changes until
/// the host has accepted the session.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationStartRequestMsg
{
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The target limb; -1 when the operation carries none. Stored through
	/// <see cref="MedicalLimbWire"/> so limb 0 is not omitted by protobuf's default-zero rule.</summary>
	public int LimbIndex
	{
		get => MedicalLimbWire.Decode(_limbIndexWire);
		set => _limbIndexWire = MedicalLimbWire.Encode(value);
	}

	[ProtoMember(3)]
	private int _limbIndexWire;

	/// <summary>The operation category; the builder always names it (the default is <see cref="MedicalOperationKind.None"/>).</summary>
	[ProtoMember(4)]
	public MedicalOperationKind Kind { get; set; }
}
