using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one player's discrete body-level terminal latch facts.
/// </summary>
[ProtoContract]
public sealed class WirePlayerBodyTerminalState
{
	[ProtoMember(1)]
	public bool Disfigured { get; set; }

	[ProtoMember(2)]
	public bool EyeGone { get; set; }

	[ProtoMember(3)]
	public bool BothEyesGone { get; set; }

	[ProtoMember(4)]
	public bool HasPulmonaryEmbolism { get; set; }

	[ProtoMember(5)]
	public bool TriedRollingLastStand { get; set; }

	[ProtoMember(6)]
	public bool SuccesfullyRolledLastStand { get; set; }

	[ProtoMember(7)]
	public bool UsedNeuralBooster { get; set; }

	[ProtoMember(8)]
	public bool FibrillationForced { get; set; }

	[ProtoMember(9)]
	public bool MindwipeScriptPresent { get; set; }

	[ProtoMember(10)]
	public bool MindwipeScriptActive { get; set; }
}
