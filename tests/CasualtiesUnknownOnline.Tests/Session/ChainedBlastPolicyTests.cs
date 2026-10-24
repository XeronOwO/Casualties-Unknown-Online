using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Which damaged mines the host replays inside the blast's own scope (ticket
/// `host-blast-rolls-a-second-drop-set.md`, slice 2). The game's mine fires from its own <c>OnDestroy</c> when its
/// health is below half and it has not fired; outside the scope that chain's victims are never captured or marked, so
/// the host rolls a second set for a death the member already rolled.
/// </summary>
[Trait("Category", "Unit")]
public class ChainedBlastPolicyTests
{
	[Fact]
	public void AMineBelowItsFiringLine_ThatHasNotFired_IsReplayed() =>
		Assert.True(ChainedBlastPolicy.ShouldReplay(hasMineScript: true, health: 0.2f, alreadyExploded: false));

	[Fact]
	public void TheFiringLineItself_DoesNotChain() =>
		Assert.False(ChainedBlastPolicy.ShouldReplay(hasMineScript: true, health: ChainedBlastPolicy.FiringLine, alreadyExploded: false));

	[Fact]
	public void AMineThatAlreadyFired_IsNotReplayed() =>
		Assert.False(ChainedBlastPolicy.ShouldReplay(hasMineScript: true, health: 0.1f, alreadyExploded: true));

	[Fact]
	public void AnEntityThatIsNotAMine_IsNotReplayed() =>
		Assert.False(ChainedBlastPolicy.ShouldReplay(hasMineScript: false, health: 0.1f, alreadyExploded: false));

	[Fact]
	public void AHealthyMine_DoesNotChainYet() =>
		Assert.False(ChainedBlastPolicy.ShouldReplay(hasMineScript: true, health: 1f, alreadyExploded: false));
}
