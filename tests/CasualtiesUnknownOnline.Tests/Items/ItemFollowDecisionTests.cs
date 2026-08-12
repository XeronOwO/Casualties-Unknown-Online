using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The guest-side follow machine (ItemFollowDecision): per-frame decisions
/// from the host's stream target and the copy's current state — frozen until
/// the first tick, settled ease-to-rest, moving velocity-sync with a hard snap
/// past the divergence threshold. The GameAdapter executes the writes; the
/// decision must carry everything it needs (mode, flags, target values).
/// </summary>
public class ItemFollowDecisionTests
{
	private const ulong Item = 42;

	[Fact]
	public void NoTarget_Frozen()
	{
		var machine = new ItemFollowDecision();

		var d = machine.Decide(Item, curX: 0f, curY: 0f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Frozen, d.Mode);
		Assert.False(d.HardSnap);
		Assert.False(d.EaseToTarget);
	}

	[Fact]
	public void FirstTarget_PlayedImmediately()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f);

		// A stream tick registers the target AND marks it playable in the same
		// call — the adapter aligns the copy on the isNew return.
		var d = machine.Decide(Item, 1f, 2f, 0f, 0.016f);

		Assert.NotEqual(FollowMode.Frozen, d.Mode);
		Assert.True(machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f) is false, "a repeated target is not new");
	}

	[Fact]
	public void Remove_BackToFrozen()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f);
		machine.Remove(Item);

		Assert.Equal(FollowMode.Frozen, machine.Decide(Item, 1f, 2f, 0f, 0.016f).Mode);
	}

	[Fact]
	public void SettledTarget_AtSpot_NoEase()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f);

		var d = machine.Decide(Item, curX: 1f, curY: 2f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Settled, d.Mode);
		Assert.False(d.EaseToTarget, "zero gap — nothing to ease");
		Assert.False(d.LogDivergence);
	}

	[Fact]
	public void SettledTarget_SmallGap_NoEase()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f);

		// Gap 0.04 ≤ 0.05 — within the settle tolerance, the local rest spot is fine.
		var d = machine.Decide(Item, curX: 0.96f, curY: 2f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Settled, d.Mode);
		Assert.False(d.EaseToTarget);
	}

	[Fact]
	public void SettledTarget_ResidualGap_Eases()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f);

		var d = machine.Decide(Item, curX: 0.9f, curY: 2f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Settled, d.Mode);
		Assert.True(d.EaseToTarget, "0.1 gap > 0.05 — ease toward the host's spot");
		Assert.False(d.LogDivergence, "0.1 ≤ 0.5 — no diagnostic");
		Assert.True(d.EaseK is > 0f and < 1f, "clamp01(dt * 12) for a 16 ms frame");
		Assert.Equal(1f, d.TargetX);
		Assert.Equal(2f, d.TargetY);
	}

	[Fact]
	public void SettledTarget_RealDivergence_Logs()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f);

		var d = machine.Decide(Item, curX: 0.4f, curY: 2f, curRot: 0f, deltaTime: 0.016f);

		Assert.True(d.EaseToTarget);
		Assert.True(d.LogDivergence, "0.6 gap > 0.5 — worth tuning on");
	}

	[Fact]
	public void Settled_EaseKClampedAtOne()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0f, 0f, 0f, 0f);

		// A huge deltaTime (a hitch frame) must not overshoot the Lerp.
		var d = machine.Decide(Item, curX: 0f, curY: 0f, curRot: 0f, deltaTime: 1f);

		Assert.True(d.EaseToTarget);
		Assert.Equal(1f, d.EaseK);
	}

	[Fact]
	public void MovingTarget_WithinThreshold_NoSnap()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0.5f, -0.5f, 0f, 1f);

		var d = machine.Decide(Item, curX: 0f, curY: 0f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Moving, d.Mode);
		Assert.False(d.HardSnap, "sqrt(5) ≈ 2.24 ≤ 3 — the local physics runs free");
		Assert.Equal(0.5f, d.VelX);
		Assert.Equal(-0.5f, d.VelY);
		Assert.Equal(1f, d.AngVel);
	}

	[Fact]
	public void MovingTarget_PastThreshold_Snaps()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 4f, 4f, 0.5f, -0.5f, 30f, 1f);

		var d = machine.Decide(Item, curX: 0f, curY: 0f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Moving, d.Mode);
		Assert.True(d.HardSnap, "sqrt(32) ≈ 5.66 > 3 — discard the local inertia");
		Assert.Equal(4f, d.TargetX);
		Assert.Equal(4f, d.TargetY);
		Assert.Equal(30f, d.TargetRot);
	}

	[Fact]
	public void SnapThreshold_ExactDistance_NoSnap()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 3f, 0f, 0.5f, 0f, 0f, 0f);

		// dist == 3.0 exactly — the comparison is strict, no snap.
		var d = machine.Decide(Item, curX: 0f, curY: 0f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Moving, d.Mode);
		Assert.False(d.HardSnap);
	}

	[Fact]
	public void SettleThreshold_ExactDistance_NoEase()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1.05f, 0f, 0f, 0f, 0f, 0f);

		var d = machine.Decide(Item, curX: 1f, curY: 0f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Settled, d.Mode);
		Assert.False(d.EaseToTarget, "dist == 0.05 exactly — strict comparison");
	}

	[Fact]
	public void MovingTarget_SettledByStream_Eases()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(Item, 1f, 2f, 0.05f, 0.05f, 0f, 0.05f); // |vel|² = 0.005 < 0.01, spin < 0.1 — settled

		var d = machine.Decide(Item, curX: 0f, curY: 0f, curRot: 0f, deltaTime: 0.016f);

		Assert.Equal(FollowMode.Settled, d.Mode);
		Assert.True(d.EaseToTarget);
	}

	[Fact]
	public void MultipleItems_Isolated()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(1, 5f, 5f, 0f, 0f, 0f, 0f);

		Assert.True(machine.Decide(2, 0f, 0f, 0f, 0.016f).Mode == FollowMode.Frozen, "the untracked item has no target");
		Assert.Equal(FollowMode.Settled, machine.Decide(1, 5f, 5f, 0f, 0.016f).Mode);
		Assert.Equal(1, machine.Count);
	}

	[Fact]
	public void Clear_RemovesAll()
	{
		var machine = new ItemFollowDecision();
		machine.UpdateTarget(1, 0f, 0f, 0f, 0f, 0f, 0f);
		machine.UpdateTarget(2, 0f, 0f, 0f, 0f, 0f, 0f);
		machine.Clear();

		Assert.Equal(0, machine.Count);
		Assert.Equal(FollowMode.Frozen, machine.Decide(1, 0f, 0f, 0f, 0.016f).Mode);
	}
}
