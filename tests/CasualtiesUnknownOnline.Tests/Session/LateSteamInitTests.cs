using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Steam may initialize on a later F8 retry (the load-time init failed while
/// Steam was still starting). The entity sync's local player was snapshotted
/// from the session at Initialize (then 0) — the per-frame refresh must pick
/// the real id up, or the self-activation PlayerJoin never matches and the
/// host's 20 Hz stream is dropped as "no member with that entity id"
/// (observed 2026-08-15, sandbox guest whose Steam client started later).
/// </summary>
public class LateSteamInitTests
{
	[Fact]
	public void LocalPlayerEntity_AdoptsTheSteamIdWhenInitCompletesLater()
	{
		var steam = new FakeSteamService(0); // Steam init failed at plugin load
		var node = TestNode.Create(0, new FakeNetwork(), steam, pumpFirstFrame: true);
		var entities = node.Services.GetRequiredService<EntitySyncService>();
		Assert.True(entities.LocalPlayer.SteamId == 0, "the local entity started with the pre-init id 0");

		steam.LocalSteamId = 2001; // F8 retry succeeded — the session now knows the real id
		node.Update();

		Assert.True(entities.LocalPlayer.SteamId == 2001, "the local entity must adopt the late SteamId");
	}
}
