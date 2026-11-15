using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The drill pod's team gate (ticket `1.0-trigger-events-wait-for-the-whole-team.md`): the native pod fires after one
/// body crouches under it for 5 s, but a session must not take the rest of the party down from one player's press.
/// Every living, connected member has to be inside the pod's own 1.5 x 5 area; dead and disconnected members do not
/// count, and a living member whose body the host cannot place yet counts as missing.
/// </summary>
[Trait("Category", "Unit")]
public class PodTeamGateTests
{
	private static readonly NetVector2 Pod = new(10f, 20f);

	private static PodMember Alive(ulong id, float x, float y) =>
		new(id, "p" + id, InWorld: true, Alive: true, new NetVector2(x, y));

	[Fact]
	public void EveryoneLivingAtThePod_CanFire()
	{
		var result = PodTeamGate.Evaluate(Pod,
		[
			Alive(1, 10f, 20f),
			Alive(2, 11f, 24f),
			Alive(3, 9f, 16f),
		]);

		Assert.True(result.CanFire);
		Assert.Equal(3, result.Living);
		Assert.Equal(3, result.AtPod);
		Assert.Empty(result.Missing);
	}

	[Fact]
	public void OneLivingMemberAway_BlocksAndNamesThem()
	{
		var result = PodTeamGate.Evaluate(Pod,
		[
			Alive(1, 10f, 20f),
			Alive(2, 20f, 20f),
		]);

		Assert.False(result.CanFire);
		Assert.Equal(2, result.Living);
		Assert.Equal(1, result.AtPod);
		Assert.Equal(["p2"], result.Missing);
	}

	[Fact]
	public void ADeadMemberAway_DoesNotBlock()
	{
		var result = PodTeamGate.Evaluate(Pod,
		[
			Alive(1, 10f, 20f),
			new PodMember(2, "p2", InWorld: true, Alive: false, new NetVector2(20f, 20f)),
		]);

		Assert.True(result.CanFire);
		Assert.Equal(1, result.Living);
		Assert.Empty(result.Missing);
	}

	[Fact]
	public void ADisconnectedMemberAway_DoesNotBlock()
	{
		var result = PodTeamGate.Evaluate(Pod,
		[
			Alive(1, 10f, 20f),
			new PodMember(2, "p2", InWorld: false, Alive: true, new NetVector2(20f, 20f)),
		]);

		Assert.True(result.CanFire);
		Assert.Equal(1, result.Living);
		Assert.Empty(result.Missing);
	}

	[Fact]
	public void ALivingMemberWithNoBodyYet_Blocks()
	{
		var result = PodTeamGate.Evaluate(Pod,
		[
			Alive(1, 10f, 20f),
			new PodMember(2, "p2", InWorld: true, Alive: true, Position: null),
		]);

		Assert.False(result.CanFire);
		Assert.Equal(["p2"], result.Missing);
	}

	[Fact]
	public void TheNativeEdgesAreExclusive()
	{
		// DrillPod.cs:20 uses `< 1.5` and `< 5`; standing exactly on the edge is not inside.
		var edge = PodTeamGate.Evaluate(Pod,
		[
			Alive(1, 10f, 20f),
			Alive(2, 11.5f, 20f),
		]);

		Assert.False(edge.CanFire);

		var inside = PodTeamGate.Evaluate(Pod,
		[
			Alive(1, 10f, 20f),
			Alive(2, 11.49f, 24.99f),
		]);

		Assert.True(inside.CanFire);
	}

	[Fact]
	public void NobodyAlive_CannotFire()
	{
		var result = PodTeamGate.Evaluate(Pod,
		[
			new PodMember(1, "p1", InWorld: true, Alive: false, Pod),
		]);

		Assert.False(result.CanFire);
		Assert.Equal(0, result.Living);
	}
}
