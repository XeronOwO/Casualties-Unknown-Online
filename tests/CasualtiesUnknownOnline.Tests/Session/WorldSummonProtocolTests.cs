using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Wire contract for the console <c>/summon</c> pair: the member's request carries the prefab, the type word, the
/// resolved point, the item state text and the world/layer stamp; the host's answer carries the created identity,
/// the verdict and the sentence. Both round-trip through the real packet codec.
/// </summary>
[Trait("Category", "Integration")]
public class WorldSummonProtocolTests
{
	[Fact]
	public void SummonRequest_RoundTripsTheTypePrefabPointStateAndGeneration()
	{
		var msg = new WorldSummonRequestMsg
		{
			PrefabId = "waterbottle",
			Kind = WorldSummonKind.Item,
			State = "{Condition:0.5f}",
			X = 12.5f,
			Y = -3.25f,
			Generation = new WorldGenerationMsg { RunEpoch = 7UL, LayerIndex = 2 },
		};

		var decoded = NetPacket.DecodePayload<WorldSummonRequestMsg>(
			NetPacket.Encode(NetMsg.WorldSummonRequest, msg));

		Assert.Equal("waterbottle", decoded.PrefabId);
		Assert.Equal(WorldSummonKind.Item, decoded.Kind);
		Assert.Equal("{Condition:0.5f}", decoded.State);
		Assert.Equal(12.5f, decoded.X);
		Assert.Equal(-3.25f, decoded.Y);
		Assert.NotNull(decoded.Generation);
		Assert.Equal(7UL, decoded.Generation!.RunEpoch);
		Assert.Equal(2, decoded.Generation.LayerIndex);
	}

	/// <summary>A request whose type word was never set reads as the entity family — the safe half, refused by the seam when the id is an item.</summary>
	[Fact]
	public void SummonRequest_WithoutATypeWord_ReadsAsAnEntity()
	{
		var decoded = NetPacket.DecodePayload<WorldSummonRequestMsg>(
			NetPacket.Encode(NetMsg.WorldSummonRequest, new WorldSummonRequestMsg { PrefabId = "beartrap" }));

		Assert.Equal(WorldSummonKind.Entity, decoded.Kind);
	}

	[Fact]
	public void SummonResult_RoundTripsTheAppliedIdentityAndAnswer()
	{
		var msg = new WorldSummonResultMsg
		{
			PrefabId = "bandage",
			ItemInstanceId = 42,
			Applied = true,
			Answer = "Summoned bandage (#42) at (1.0, 2.0).",
		};

		var decoded = NetPacket.DecodePayload<WorldSummonResultMsg>(
			NetPacket.Encode(NetMsg.WorldSummonResult, msg));

		Assert.Equal("bandage", decoded.PrefabId);
		Assert.Equal(42UL, decoded.ItemInstanceId);
		Assert.True(decoded.Applied);
		Assert.Equal("Summoned bandage (#42) at (1.0, 2.0).", decoded.Answer);
	}
}
