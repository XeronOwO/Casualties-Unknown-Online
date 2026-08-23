using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavioral coverage for the minimal host-rules late-join gate: a brand-new
/// member must be rejected when the host is already in-world and late join is
/// disabled, while menu-side/new-run joins stay allowed.
/// </summary>
public class HostRulesHandshakeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void LateJoinDisabled_RejectsNewMemberWhenHostAlreadyInWorld()
	{
		var (host, guest) = CreateNodes(allowLateJoin: false, hostInWorld: true);

		guest.Steam.FireLobbyEntered(LobbyId);

		Assert.Empty(host.Session.Members.Where(m => m.SteamId == GuestId));
	}

	[Fact]
	public void LateJoinDisabled_AllowsNewMemberWhileHostIsInMenu()
	{
		var (host, guest) = CreateNodes(allowLateJoin: false, hostInWorld: false);

		guest.Steam.FireLobbyEntered(LobbyId);

		Assert.Contains(host.Session.Members, m => m.SteamId == GuestId);
	}

	[Fact]
	public void LateJoinEnabled_AllowsNewMemberWhenHostAlreadyInWorld()
	{
		var (host, guest) = CreateNodes(allowLateJoin: true, hostInWorld: true);

		guest.Steam.FireLobbyEntered(LobbyId);

		Assert.Contains(host.Session.Members, m => m.SteamId == GuestId);
	}

	private static (TestNode Host, TestNode Guest) CreateNodes(bool allowLateJoin, bool hostInWorld)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
				new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowLateJoin = allowLateJoin }))));
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true);

		host.Steam.FireLobbyCreated(LobbyId);
		if (hostInWorld)
		{
			host.Session.ReportSceneState(SceneStateType.InWorld, "test-world");
		}

		return (host, guest);
	}
}
