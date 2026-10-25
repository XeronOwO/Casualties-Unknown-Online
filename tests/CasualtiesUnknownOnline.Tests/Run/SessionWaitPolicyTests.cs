using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Run;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Gate;

/// <summary>
/// Which wait a client is in, decided from plain facts. This is the layer an adversarial
/// review found untested while it held both of the mistakes it caught: a member told the
/// wait was over while someone else was still generating, and a claim about the host's
/// generation phase read off a flag that does not carry it. The cases below are those two
/// scenarios plus the roster the host prints.
/// </summary>
public class SessionWaitPolicyTests
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

	private const ulong Local = 1001;

	private const ulong Host = 1001;

	private const ulong Member = 2002;

	[Fact]
	public void TheHost_DoesNotNameAPeerWhoseHandshakeHasNotCompleted()
	{
		// The gate arms on `Handshaken && !InWorld`: an unconfirmed peer is retrying its handshake,
		// not loading, and the line promises the group starts together.
		var state = SessionWaitPolicy.Decide(AsHost(
			new SessionWaitMember(Member, Handshaken: false, InWorld: false, "Ada"),
			new SessionWaitMember(3003, Handshaken: true, InWorld: false, "Bo")));

		Assert.Equal(SessionWaitKind.MembersLoading, state.Kind);
		Assert.Equal(["Bo"], state.WaitingNames);
	}

	[Fact]
	public void TheHost_NamesEveryConfirmedPeerThatIsNotInTheWorldYet()
	{
		var state = SessionWaitPolicy.Decide(AsHost(
			new SessionWaitMember(Member, true, false, "Ada"),
			new SessionWaitMember(3003, true, true, "Bo")));

		Assert.Equal(SessionWaitKind.MembersLoading, state.Kind);
		Assert.Equal(["Ada"], state.WaitingNames);
		Assert.Equal(42, state.RemainingSeconds);
	}

	[Fact]
	public void TheHost_WithEverybodyIn_SaysItWaitsForTheRemainingPlayers()
	{
		// Reachable when the last member is removed rather than arriving: the list empties and the
		// wording has to say something rather than leaving a gap in the sentence.
		var state = SessionWaitPolicy.Decide(AsHost());

		Assert.Equal(SessionWaitKind.MembersLoading, state.Kind);
		Assert.Empty(state.WaitingNames);
		Assert.Contains("the remaining players", SessionWaitText.Line(state, Render));
	}

	[Fact]
	public void AMemberWaitingForTheHost_DoesNotClaimToKnowTheHostsPhase()
	{
		// `HostInWorld` is false for the menu, for loading and for "this client has not seen the
		// host's report" alike, so the state carries no phase and the line must not invent one.
		var state = SessionWaitPolicy.Decide(Guest(hostInWorld: false));

		Assert.Equal(SessionWaitKind.HostLoading, state.Kind);
		Assert.DoesNotContain("generating", SessionWaitText.Line(state, Render));
	}

	[Fact]
	public void AMemberWhoseTableHasNoThirdPlayer_IsNotToldTheWaitIsOver()
	{
		// The ordinary three-client case on a member: a third player's row arrives ALREADY in-world,
		// so `others` is empty while that player is still generating. The last stage must describe
		// this client's own wait, not everyone's arrival.
		var members = new List<SessionWaitMember>
		{
			new(Host, true, true, "Host"),
			new(Local, true, true, "Me"),
		};
		var state = SessionWaitPolicy.Decide(new SessionWaitInput(false, Local, Host, members, true, 0, 5_000));

		Assert.Equal(SessionWaitKind.Starting, state.Kind);
		var line = SessionWaitText.Line(state, Render);
		Assert.DoesNotContain("Everyone is here", line);
		Assert.Contains("Waiting for the host to start the run", line);
	}

	[Fact]
	public void AMemberWithAThirdPlayerStillOut_NamesThatPlayer()
	{
		var members = new List<SessionWaitMember>
		{
			new(Host, true, true, "Host"),
			new(Local, true, true, "Me"),
			new(3003, true, false, "Cleo"),
		};
		var state = SessionWaitPolicy.Decide(new SessionWaitInput(false, Local, Host, members, true, 0, 8_000));

		Assert.Equal(SessionWaitKind.OthersLoading, state.Kind);
		Assert.Equal(["Cleo"], state.WaitingNames);
		Assert.Equal(22, state.RemainingSeconds);
	}

	[Fact]
	public void AMember_DoesNotNameItselfOrTheHost()
	{
		var members = new List<SessionWaitMember>
		{
			new(Host, true, false, "Host"),
			new(Local, true, false, "Me"),
			new(3003, true, false, "Cleo"),
		};
		var state = SessionWaitPolicy.Decide(new SessionWaitInput(false, Local, Host, members, true, 0, 1_000));

		Assert.Equal(["Cleo"], state.WaitingNames);
	}

	private static SessionWaitInput AsHost(params SessionWaitMember[] others)
	{
		var members = new List<SessionWaitMember> { new(Host, true, true, "Host") };
		members.AddRange(others);
		return new SessionWaitInput(true, Local, Host, members, true, 42, 0);
	}

	private static SessionWaitInput Guest(bool hostInWorld) => new(
		false,
		Local,
		Host,
		[new SessionWaitMember(Host, true, hostInWorld, "Host"), new SessionWaitMember(Local, true, true, "Me")],
		hostInWorld,
		0,
		0);
}
