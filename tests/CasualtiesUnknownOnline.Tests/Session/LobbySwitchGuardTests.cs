using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The lobby-switch policy (pure): identity changes are menu-only. The one
/// carve-out is the existing solo-in-world -> host-lobby conversion, which
/// must keep working for late joiners.
/// </summary>
public class LobbySwitchGuardTests
{
	[Fact]
	public void Menu_AllowsCreateAndJoin()
	{
		Assert.True(LobbySwitchGuard.CanCreateLobby(SessionRole.None, sessionActive: false, worldFlowActive: false));
		Assert.True(LobbySwitchGuard.CanCreateLobby(SessionRole.Host, sessionActive: true, worldFlowActive: false));
		Assert.True(LobbySwitchGuard.CanCreateLobby(SessionRole.Guest, sessionActive: true, worldFlowActive: false));
		Assert.True(LobbySwitchGuard.CanJoinLobby(worldFlowActive: false));
	}

	[Fact]
	public void WorldFlow_OnlySoloConversionMayCreate()
	{
		Assert.True(LobbySwitchGuard.CanCreateLobby(SessionRole.None, sessionActive: false, worldFlowActive: true),
			"the solo-turned-lobby flow (Role=None, no session) is the one supported in-world create");

		Assert.False(LobbySwitchGuard.CanCreateLobby(SessionRole.Host, sessionActive: true, worldFlowActive: true),
			"a sessioned host may not re-create a lobby from inside a world");
		Assert.False(LobbySwitchGuard.CanCreateLobby(SessionRole.Guest, sessionActive: true, worldFlowActive: true),
			"a guest may not abandon its host from inside a world");
		Assert.False(LobbySwitchGuard.CanCreateLobby(SessionRole.None, sessionActive: true, worldFlowActive: true),
			"an active session with no role is an inconsistent state — refuse");
	}

	[Fact]
	public void WorldFlow_JoinAlwaysRefused() =>
		Assert.False(LobbySwitchGuard.CanJoinLobby(worldFlowActive: true));
}
