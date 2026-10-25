using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Run;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Gate;

/// <summary>
/// The one place a wait becomes a line: every kind of wait is worded here, as a catalogue key plus its values, so a new kind cannot
/// reach the screen without a line and the DIRECTION of the wait is what the line leads with. The renderer here is the ENGLISH table's
/// shape — the real one is the local client's catalogue, which is the point of the keys: a host running the Chinese UI read an English
/// line before the user's 2026-10-25 report.
/// </summary>
public class SessionWaitTextTests
{
	/// <summary>The English sentences the keys resolve to, as the real catalogue holds them.</summary>
	private static string Render(string key, IReadOnlyList<string> args) => key switch
	{
		"wait.host_loading" => "Waiting for the host to load… The host is not in the world yet.",
		"wait.members_loading" => $"Waiting for {args[0]} to load… ({args[1]}s). The group starts together.",
		"wait.others_loading" => $"Waiting for {args[0]}… ({args[1]}s). The host is already in the world.",
		"wait.starting" => "Waiting for the host to start the run…",
		"wait.names_nobody" => "the remaining players",
		_ => $"<{key}>",
	};

	[Fact]
	public void TheHost_NamesTheMembersItWaitsFor()
	{
		var state = new SessionWaitState(SessionWaitKind.MembersLoading, ["Ada", "Bo"], 42);

		var line = SessionWaitText.Line(state, Render);

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

		var line = SessionWaitText.Line(state, Render);

		Assert.Contains("Waiting for the host to load", line);
		Assert.Contains("The host is not in the world yet", line);
		Assert.DoesNotContain("(0s)", line);
	}

	[Fact]
	public void AMemberWaitingForTheOthers_SaysTheHostIsAlreadyIn()
	{
		var state = new SessionWaitState(SessionWaitKind.OthersLoading, ["Cleo"], 12);

		var line = SessionWaitText.Line(state, Render);

		Assert.Contains("Waiting for Cleo", line);
		Assert.Contains("(12s)", line);
		Assert.Contains("The host is already in the world", line);
	}

	[Fact]
	public void TheLastStage_NamesWhatItWaitsForWithoutACountdown()
	{
		var line = SessionWaitText.Line(new SessionWaitState(SessionWaitKind.Starting, [], 0), Render);

		Assert.Contains("Waiting for the host to start the run", line);
		Assert.DoesNotContain("(0s)", line);
		Assert.DoesNotContain("Everyone is here", line);
	}

	[Fact]
	public void ANegativeCountdown_ReadsAsZero()
	{
		// The host's remaining time can go negative for a frame as the gate releases; a
		// negative number on screen would read as a bug rather than as a wait that ended.
		var line = SessionWaitText.Line(new SessionWaitState(SessionWaitKind.OthersLoading, ["Cleo"], -3), Render);

		Assert.Contains("(0s)", line);
		Assert.DoesNotContain("-3", line);
	}

	[Fact]
	public void AWaitWithNobodyToName_SaysSoRatherThanLeavingAGap()
	{
		var line = SessionWaitText.Line(new SessionWaitState(SessionWaitKind.MembersLoading, [], 7), Render);

		Assert.Contains("Waiting for the remaining players to load", line);
		Assert.Contains("(7s)", line);
	}
}
