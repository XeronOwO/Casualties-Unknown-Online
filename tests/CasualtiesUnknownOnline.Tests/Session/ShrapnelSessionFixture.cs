using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The stateless three-node fixture helpers shared by the shrapnel-session test classes: the handshaken host + two guests, the character snapshot with a shrapnel limb, the operation-control accessor and the in-world report. Every test builds its own world — this class holds no state.
/// </summary>
internal static class ShrapnelSessionFixture
{
	internal const ulong HostId = 1001;

	internal const ulong Guest1Id = 2001;

	internal const ulong Guest2Id = 2002;

	internal const ulong LobbyId = 9001;

	internal sealed record World(TestNode Host, TestNode Guest1, TestNode Guest2);

	internal static World CreateThreeNode()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var g1Steam = new FakeSteamService(Guest1Id) { LobbyOwner = HostId, LobbyMembers = [HostId, Guest1Id, Guest2Id] };
		var g2Steam = new FakeSteamService(Guest2Id) { LobbyOwner = HostId, LobbyMembers = [HostId, Guest1Id, Guest2Id] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var g1 = TestNode.Create(Guest1Id, network, g1Steam, clock, pumpFirstFrame: true);
		var g2 = TestNode.Create(Guest2Id, network, g2Steam, clock, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, Guest1Id, Guest2Id];
		g1.Steam.FireLobbyEntered(LobbyId);
		g2.Steam.FireLobbyEntered(LobbyId);
		host.Update();
		g1.Update();
		g2.Update();
		return new World(host, g1, g2);
	}

	internal static CharacterDataMsg Snapshot(ulong owner, bool conscious, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items],
		Health = new CharacterHealthMsg
		{
			Alive = true,
			Conscious = conscious,
			BrainHealth = conscious ? 80f : 5f,
		},
		Limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 50f, MuscleHealth = 50f },
			new CharacterLimbMsg { Index = 1, SkinHealth = 50f, MuscleHealth = 50f, Shrapnel = 3 },
			new CharacterLimbMsg { Index = 2, SkinHealth = 80f, MuscleHealth = 80f },
		],
	};

	internal static IMedicalOperationControl Ops(TestNode node) =>
		node.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;

	internal static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
}
