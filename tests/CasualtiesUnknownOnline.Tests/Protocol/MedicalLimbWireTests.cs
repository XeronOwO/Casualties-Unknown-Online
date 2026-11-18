using System.IO;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

/// <summary>
/// The medical-operation messages must carry limb 0 across the wire. Protobuf writes no field
/// whose value is the type default, so a limb index stored as itself loses limb 0 and the
/// receiver keeps its -1 initializer — measured 2026-11-18, when a shrapnel start on limb 0
/// reached the target's body check as "limb -1" and was refused with "Target limb not found".
/// The five messages therefore store <c>index + 1</c> (zero means no limb), the rule
/// <see cref="PlayerHealRequestMsg"/> already states.
/// </summary>
public sealed class MedicalLimbWireTests
{
	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(15)]
	public void EveryMedicalMessage_CarriesTheLimbAcrossTheWire(int limb)
	{
		Assert.Equal(limb, RoundTrip(new MedicalOperationStartRequestMsg { LimbIndex = limb }).LimbIndex);
		Assert.Equal(limb, RoundTrip(new MedicalOperationTargetCheckRequestMsg { LimbIndex = limb }).LimbIndex);
		Assert.Equal(limb, RoundTrip(new MedicalOperationStartAckMsg { LimbIndex = limb }).LimbIndex);
		Assert.Equal(limb, RoundTrip(new MedicalOperationStateMsg { LimbIndex = limb }).LimbIndex);
		Assert.Equal(limb, RoundTrip(new MedicalOperationEndCommittedMsg { LimbIndex = limb }).LimbIndex);
	}

	[Fact]
	public void APayloadThatOmitsTheFieldReadsAsNoLimb()
	{
		// A sender that never set the limb produces no field at all; the receiver must read -1.
		var decoded = RoundTrip(new MedicalOperationStartRequestMsg());

		Assert.Equal(-1, decoded.LimbIndex);
	}

	private static T RoundTrip<T>(T message)
	{
		using var stream = new MemoryStream();
		ProtoBuf.Serializer.Serialize(stream, message);
		stream.Position = 0;
		return ProtoBuf.Serializer.Deserialize<T>(stream);
	}
}
