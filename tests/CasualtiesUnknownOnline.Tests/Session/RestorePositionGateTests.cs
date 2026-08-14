using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The restore-position gate: a reconnect restore's position applies exactly
/// ONCE per body. The host sends the saved character from BOTH the handshake
/// and the InWorld edge, so a re-sent restore must not teleport the same body
/// a second time (the observed 0.5 s double teleport). The position reset
/// binds to the body leaving the world, never to a restore arriving.
/// </summary>
public class RestorePositionGateTests
{
	[Fact]
	public void PositionAppliesOncePerBody_ReSentRestoreDoesNotReapply()
	{
		var gate = new RestorePositionGate();

		// The first restore: the body applies the position on its first frame.
		Assert.True(gate.ShouldApplyPosition, "the first restore applies");
		gate.MarkPositionApplied();

		// A re-sent restore (handshake + InWorld edge) arrives while the SAME
		// body is still alive — no reapply.
		Assert.False(gate.ShouldApplyPosition, "a re-sent restore must not reapply to the same body");
	}

	[Fact]
	public void BodyLeavingTheWorld_ReArmsThePositionForTheNextBody()
	{
		var gate = new RestorePositionGate();
		gate.MarkPositionApplied();
		Assert.False(gate.ShouldApplyPosition, "the position already landed on this body");

		gate.OnBodyLeft(); // death / menu / disconnect

		Assert.True(gate.ShouldApplyPosition, "the next body applies its restore position again");
	}

	[Fact]
	public void FreshGate_AppliesByDefault()
	{
		var gate = new RestorePositionGate();
		Assert.True(gate.ShouldApplyPosition, "a fresh gate (new body) applies its restore position");
	}
}
