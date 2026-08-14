using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The local attack-swing presentation window (AttackSwingState): the swing is
/// held for its visible span so the peer's clone can replay the ArmsSwing clip
/// on the IsAttacking flag's rising edge, and the hold covers the unreliable
/// 20 Hz stream. Time is an explicit input.
/// </summary>
public class AttackSwingStateTests
{
	[Fact]
	public void MarkAttack_SetsAttacking()
	{
		var state = new AttackSwingState();

		state.MarkAttack(nowMs: 1000);

		Assert.True(state.IsAttacking);
	}

	[Fact]
	public void Tick_BeforeWindowEnd_StaysAttacking()
	{
		var state = new AttackSwingState();
		state.MarkAttack(nowMs: 1000);

		state.Tick(nowMs: 1000 + AttackSwingState.SwingDurationMs - 1);

		Assert.True(state.IsAttacking, "one ms before the window end — still swinging");
	}

	[Fact]
	public void Tick_AtWindowEnd_ClearsAttacking()
	{
		var state = new AttackSwingState();
		state.MarkAttack(nowMs: 1000);

		state.Tick(nowMs: 1000 + AttackSwingState.SwingDurationMs);

		Assert.False(state.IsAttacking, "the boundary is inclusive — the swing ends exactly at its span");
	}

	[Fact]
	public void RapidSecondAttack_RestartsTheWindow()
	{
		var state = new AttackSwingState();
		state.MarkAttack(nowMs: 1000);

		state.Tick(nowMs: 1200); // 200 ms in — still swinging
		Assert.True(state.IsAttacking);

		state.MarkAttack(nowMs: 1250); // a rapid follow-up attack restarts the window

		state.Tick(nowMs: 1250 + AttackSwingState.SwingDurationMs - 1);
		Assert.True(state.IsAttacking, "the window measures from the LAST swing");
	}

	[Fact]
	public void NoAttack_NeverAttacking()
	{
		var state = new AttackSwingState();

		state.Tick(nowMs: 0);
		state.Tick(nowMs: 1_000_000);

		Assert.False(state.IsAttacking);
	}

	[Fact]
	public void Reset_ClearsAttacking()
	{
		var state = new AttackSwingState();
		state.MarkAttack(nowMs: 1000);
		Assert.True(state.IsAttacking);

		state.Reset();

		Assert.False(state.IsAttacking);
	}

	[Fact]
	public void Reset_ThenTick_StaysCleared()
	{
		var state = new AttackSwingState();
		state.MarkAttack(nowMs: 1000);
		state.Reset();

		state.Tick(nowMs: 2000);

		Assert.False(state.IsAttacking, "after a reset a stale swing must not re-assert");
	}
}
