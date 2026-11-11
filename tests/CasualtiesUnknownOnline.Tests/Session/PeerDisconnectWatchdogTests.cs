using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// A peer whose connection ended must leave the session exactly when the session still counts it:
/// the host drops a member it knows, the guest ends its own session when the host link is the one
/// that ended, and every other edge answers nothing.
/// </summary>
public sealed class PeerDisconnectWatchdogTests
{
	private const ulong Host = 1;
	private const ulong Member = 2;
	private const ulong Other = 3;

	[Fact]
	public void AHostDropsTheMemberItStillKnows()
	{
		Assert.Equal(
			PeerLossAction.DropMember,
			PeerDisconnectWatchdog.Decide(SessionRole.Host, Host, 0, Member, knownMember: true));
	}

	[Fact]
	public void AHostIgnoresAStranger()
	{
		Assert.Equal(
			PeerLossAction.None,
			PeerDisconnectWatchdog.Decide(SessionRole.Host, Host, 0, Other, knownMember: false));
	}

	[Fact]
	public void AHostIgnoresItsOwnEdge()
	{
		Assert.Equal(
			PeerLossAction.None,
			PeerDisconnectWatchdog.Decide(SessionRole.Host, Host, 0, Host, knownMember: true));
	}

	[Fact]
	public void AGuestEndsTheSessionWhenItsHostLinkEnds()
	{
		Assert.Equal(
			PeerLossAction.EndSession,
			PeerDisconnectWatchdog.Decide(SessionRole.Guest, Member, Host, Host, knownMember: true));
	}

	[Fact]
	public void AGuestIgnoresAnotherMembersEdge()
	{
		Assert.Equal(
			PeerLossAction.None,
			PeerDisconnectWatchdog.Decide(SessionRole.Guest, Member, Host, Other, knownMember: true));
	}

	[Fact]
	public void ASessionlessClientIgnoresEveryEdge()
	{
		Assert.Equal(
			PeerLossAction.None,
			PeerDisconnectWatchdog.Decide(SessionRole.None, Member, Host, Host, knownMember: true));
	}
}
