using CasualtiesUnknownOnline.GameAdapter.Run;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Gate;

/// <summary>
/// The one place a wait becomes a line: every kind of wait is worded here, so a new
/// kind cannot reach the screen without a line, and the DIRECTION of the wait is what
/// the line leads with. The state is built by the coordinator from the session and the
/// gate; these cases pin the wording, which is what a player actually reads.
/// </summary>
public class SessionWaitTextTests
{
	[Fact]
	public void TheHost_NamesTheMembersItWaitsFor()
	{
		var state = new SessionWaitState(SessionWaitKind.MembersLoading, ["Ada", "Bo"], 42);

		var line = SessionWaitText.Line(state);

		Assert.Contains("Waiting for Ada, Bo to load", line);
		Assert.Contains("(42s)", line);
		Assert.Contains("The group starts together", line);
	}

	[Fact]
	public void AMemberWaitingForTheHost_DoesNotClaimTheHostsCountdown()
	{
		// The member's own 30 s is an approximation of the host's gate, not a deadline this
		// side owns — so the line names the wait and says what the host is doing instead of
		// printing a number the member cannot stand behind.
		var state = new SessionWaitState(SessionWaitKind.HostLoading, [], 0);

		var line = SessionWaitText.Line(state);

		Assert.Contains("Waiting for the host to load", line);
		Assert.Contains("The host is not in the world yet", line);
		Assert.DoesNotContain("(0s)", line);
	}

	[Fact]
	public void AMemberWaitingForTheOthers_SaysTheHostIsAlreadyIn()
	{
		var state = new SessionWaitState(SessionWaitKind.OthersLoading, ["Cleo"], 12);

		var line = SessionWaitText.Line(state);

		Assert.Contains("Waiting for Cleo", line);
		Assert.Contains("(12s)", line);
		Assert.Contains("The host is already in the world", line);
	}

	[Fact]
	public void TheLastStage_NamesWhatItWaitsForWithoutACountdown()
	{
		var line = SessionWaitText.Line(new SessionWaitState(SessionWaitKind.Starting, [], 0));

		Assert.Contains("Waiting for the host to start the run", line);
		Assert.DoesNotContain("(0s)", line);
		Assert.DoesNotContain("Everyone is here", line);
	}

	[Fact]
	public void ANegativeCountdown_ReadsAsZero()
	{
		// The host's remaining time can go negative for a frame as the gate releases; a
		// negative number on screen would read as a bug rather than as a wait that ended.
		var line = SessionWaitText.Line(new SessionWaitState(SessionWaitKind.OthersLoading, ["Cleo"], -3));

		Assert.Contains("(0s)", line);
		Assert.DoesNotContain("-3", line);
	}

	[Fact]
	public void AWaitWithNobodyToName_SaysSoRatherThanLeavingAGap()
	{
		var line = SessionWaitText.Line(new SessionWaitState(SessionWaitKind.MembersLoading, [], 7));

		Assert.Contains("Waiting for the remaining players to load", line);
		Assert.Contains("(7s)", line);
	}
}
