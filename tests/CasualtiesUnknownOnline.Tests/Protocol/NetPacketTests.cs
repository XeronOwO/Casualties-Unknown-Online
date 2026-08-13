using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

/// <summary>
/// The frame layer: [msgId:1][protobuf payload]. These tests are deliberately
/// transport-free — they lock the wire format contract (the first layer the
/// FakeTransport pipeline builds on) without touching any seam.
/// </summary>
public class NetPacketTests
{
	[Fact]
	public void Encode_WritesMsgIdThenPayload()
	{
		var frame = NetPacket.Encode(NetMsg.Handshake, new HandshakeMsg { Protocol = ProtocolVersion.Current });

		Assert.Equal((byte)NetMsg.Handshake, frame[0]);
		Assert.True(frame.Length > 1, "payload must follow the msgId byte");
	}

	[Fact]
	public void EncodeDecode_RoundTripsMessage()
	{
		var msg = new HandshakeMsg { Protocol = ProtocolVersion.Current };

		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, msg));

		Assert.Equal(ProtocolVersion.Current, decoded.Protocol);
	}

	[Fact]
	public void Encode_WithoutPayload_IsSingleByteFrame()
	{
		var frame = NetPacket.Encode(NetMsg.HandshakeAckAck);

		Assert.Equal(new byte[] { (byte)NetMsg.HandshakeAckAck }, frame);
	}

	[Fact]
	public void DecodePayload_ZeroValueOmission_RoundTripsToDefault()
	{
		// protobuf-net omits zero values on the wire — a zero field
		// deserializes back to its default. Round-trip of zero must be stable.
		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, new HandshakeMsg()));

		Assert.Equal(0, decoded.Protocol);
		Assert.NotNull(decoded.Scene);
	}

	[Fact]
	public void CharacterData_PositionField_RoundTrips()
	{
		// The reconnect restore's leave-spot field (ProtoMember 7): a null
		// position (old-version sender) stays null, a real one round-trips
		// exactly — the restore's position apply depends on it.
		var msg = new CharacterDataMsg { Position = new NetVector2Msg(12.5f, -34.25f) };

		var decoded = NetPacket.DecodePayload<CharacterDataMsg>(NetPacket.Encode(NetMsg.CharacterData, msg));

		Assert.NotNull(decoded.Position);
		Assert.Equal(12.5f, decoded.Position.X);
		Assert.Equal(-34.25f, decoded.Position.Y);

		var without = NetPacket.DecodePayload<CharacterDataMsg>(NetPacket.Encode(NetMsg.CharacterData, new CharacterDataMsg()));
		Assert.Null(without.Position); // the null claim must survive the wire (message field — omitted, decodes to null)
	}

	[Fact]
	public void CraftReport_ZeroValueEnums_RoundTripTransparently()
	{
		// The craft wire discipline: Kind=Craft and Disposition=Destroyed are
		// zero (omitted on the wire by protobuf) — the omission must decode
		// back to the SAME semantic defaults (the default enum value is the
		// semantic default).
		var msg = new CraftReportMsg
		{
			Kind = CraftOperationKind.Craft,
			Entries =
			[
				new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Destroyed,
					Item = new CharacterItemMsg { InstanceId = 42, ItemId = "cloth", Condition = 0.5f },
				},
				new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Changed,
					Item = new CharacterItemMsg { InstanceId = 43, ItemId = "knife", Condition = 0.6f },
				},
			],
			Products = [new CharacterItemMsg { InstanceId = 44, ItemId = "bandage", Condition = 1f, SlotIndex = 3 }],
		};

		var decoded = NetPacket.DecodePayload<CraftReportMsg>(NetPacket.Encode(NetMsg.CraftReport, msg));

		Assert.Equal(CraftOperationKind.Craft, decoded.Kind);
		Assert.Equal(2, decoded.Entries.Count);
		Assert.Equal(CraftEntryDisposition.Destroyed, decoded.Entries[0].Disposition);
		Assert.Equal(42ul, decoded.Entries[0].Item.InstanceId);
		Assert.Equal(CraftEntryDisposition.Changed, decoded.Entries[1].Disposition);
		Assert.Equal(0.6f, decoded.Entries[1].Item.Condition);
		Assert.Single(decoded.Products);
		Assert.Equal(44ul, decoded.Products[0].InstanceId);
		Assert.Equal(3, decoded.Products[0].SlotIndex);
	}

	[Fact]
	public void RecipeUnlock_IndexZero_RoundTrips()
	{
		// RecipeIndex 0 is a VALID index (blueprints roll Range(0, Count)) —
		// protobuf omits it on the wire, and the omission decodes back to 0.
		var decoded = NetPacket.DecodePayload<RecipeUnlockMsg>(NetPacket.Encode(NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = 0 }));

		Assert.Equal(0, decoded.RecipeIndex);
	}
}
