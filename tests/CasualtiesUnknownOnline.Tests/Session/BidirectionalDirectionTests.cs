using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Direction contract, bidirectional half: a two-way message is accepted at
/// both roles. Backed by <see cref="NetMessageRegistry"/> through the real
/// composed receivers of <see cref="DirectionProbe"/>; these rows lock the
/// classification. Split from the former single DirectionTests class so xUnit
/// v2 (serial inside a class) does not serialize all three direction families.
/// </summary>
public class BidirectionalDirectionTests(DirectionProbe probe) : IClassFixture<DirectionProbe>
{
	public static TheoryData<NetMsg> BidirectionalMessages => new()
	{
		NetMsg.Ping,
		NetMsg.Pong,
		NetMsg.SceneState,
		NetMsg.BlockDamaged,
		NetMsg.CharacterData,
		NetMsg.BlockPlaced,
		NetMsg.BuildingEntityDamaged,
		NetMsg.BuildingEntityOpened,
		NetMsg.ItemIdWatermark,
		NetMsg.EntityEvent,
		NetMsg.EntitySpawned,
		NetMsg.FluidInteraction,
		NetMsg.ModMessage,
		NetMsg.CraftReport,
		NetMsg.RecipeUnlock,
		NetMsg.SpeechMsg,
		NetMsg.LimbStateEvent,
		NetMsg.CharacterSound,
		NetMsg.CharacterAttackAnim,
		NetMsg.CharacterLandingVisual,
		NetMsg.CharacterRagdoll,
		NetMsg.WorldBloodSpawn,
		NetMsg.DynamiteExplosion,
		NetMsg.Chat,
		NetMsg.TraderSwing,
		NetMsg.KernelEnvelope,
		NetMsg.PlayerColorUpdate,
		NetMsg.LocationPing,
	};

	[Theory]
	[MemberData(nameof(BidirectionalMessages))]
	public void Bidirectional_AllowedOnBothSides(NetMsg msg)
	{
		Assert.True(probe.HostAccepts(msg), $"{msg} must be valid at the host");
		Assert.True(probe.GuestAccepts(msg), $"{msg} must be valid at the guest");
	}
}
