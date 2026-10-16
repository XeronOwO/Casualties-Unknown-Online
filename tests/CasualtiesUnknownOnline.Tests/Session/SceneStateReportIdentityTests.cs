using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The scene report's identity bound, and the relay it must not break.
///
/// <para>
/// A member's own client is the only reporter of its own scene: every production
/// sender stamps the frame with its own id (<c>SessionService.SendSceneState</c>:
/// <c>SteamId = _steam.LocalSteamId</c>). The handler's old fallback
/// (<c>msg.SteamId != 0 ? msg.SteamId : sender</c>) took the payload's word for
/// whose state this is, and membership was the only check — so one report could
/// move ANOTHER member's scene state, enter it into the world and have the host
/// send that member the whole entry fan-out.
/// </para>
///
/// <para>
/// The comparison is DIRECTION-SCOPED for the reason the handler's own comment
/// gives: the host re-sends another member's change, so on a GUEST the sender is
/// the host while the payload legitimately names the third member. Only the
/// host's inbound frame — a member's own report — has to name itself.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class SceneStateReportIdentityTests
{
	[Fact]
	public void SceneReportNamingItsSender_IsAdoptedOnTheHost()
	{
		using var w = ItemSimWorld.Create();

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(5f, 6f));
		w.Driver.Tick(33);

		Assert.True(w.Host.Session.IsRemoteInWorld(w.G1.SteamId),
			"the ordinary report — a member naming its own body — must still be adopted");
	}

	[Fact]
	public void SceneReportNamingAnotherMember_IsDroppedAtTheHost()
	{
		using var w = ItemSimWorld.Create();

		// The forged frame: it arrives from G1 and claims to be G2's scene entry.
		w.Host.Transport.Deliver(w.G1.SteamId, NetPacket.Encode(NetMsg.SceneState, new SceneStateMsg
		{
			State = (byte)SceneStateType.InWorld,
			SceneName = "SampleScene",
			SteamId = w.G2.SteamId,
		}));
		w.Driver.Tick(33);

		Assert.False(w.Host.Session.IsRemoteInWorld(w.G2.SteamId),
			"the host must not move a member's scene state on another member's word");
		Assert.False(w.Host.Session.IsRemoteInWorld(w.G1.SteamId),
			"and the reporter itself enters nothing by naming somebody else");
	}

	[Fact]
	public void HostRelayNamingAnotherMember_StillLandsOnTheThirdMember()
	{
		using var w = ItemSimWorld.Create();

		// A guest learns the OTHER member's row from the entity domain's roster
		// announcement, which a scene-only scenario never runs — declare the row the
		// host's relay lands on, so the case reads the relay and not the roster. The
		// first assertion below needs no such row: the frame is counted on G2's own
		// transport, so the relay's emission is proved on its own.
		((ISessionControl)w.G2.Session).GetOrCreateMember(w.G1.SteamId);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(5f, 6f));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G2, NetMsg.SceneState) == 1, "the host must still relay a third member's entry");
		Assert.True(w.G2.Session.IsRemoteInWorld(w.G1.SteamId),
			"the relay names that member and arrives from the host — the bound must never refuse it");
	}
}
