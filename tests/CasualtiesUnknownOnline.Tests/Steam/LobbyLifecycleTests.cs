using CasualtiesUnknownOnline.Runtime.Steam;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Steam;

/// <summary>
/// The lobby-lifecycle verdict: creating a NEW lobby must first leave the
/// current one. Steam requires the leave — a re-create without it strands the
/// old lobby (observed live: F8 re-create left a residual member and the guest
/// joined the dead lobby, unable to connect).
/// </summary>
public class LobbyLifecycleTests
{
	[Fact]
	public void FreshLifecycle_HasNoLobbyToLeave()
	{
		var lobby = new LobbyLifecycle();
		Assert.False(lobby.IsInLobby, "no lobby yet — nothing to leave");
		Assert.True(lobby.CurrentLobbyId == 0, "the current lobby id starts at 0");
	}

	[Fact]
	public void AfterAcquiringALobby_ACreateMustFirstLeaveIt()
	{
		var lobby = new LobbyLifecycle();
		lobby.OnLobbyAcquired(9001);

		Assert.True(lobby.CurrentLobbyId == 9001, "the lobby is now current");
		Assert.True(lobby.IsInLobby, "a re-create must first leave the current lobby");
	}

	[Fact]
	public void LeavingTheLobby_ClearsTheLeaveBeforeCreateVerdict()
	{
		var lobby = new LobbyLifecycle();
		lobby.OnLobbyAcquired(9001);
		lobby.OnLobbyLeft();

		Assert.True(lobby.CurrentLobbyId == 0, "no current lobby after leaving");
		Assert.False(lobby.IsInLobby, "a fresh create no longer needs a leave");
	}
}
