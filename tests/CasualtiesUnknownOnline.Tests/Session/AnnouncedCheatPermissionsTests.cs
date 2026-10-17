using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The MEMBER side of the cheat roster: the store the announcement writes and the
/// local check the console reads. Three states have to stay distinct, because they
/// mean three different things to an operator:
/// <list type="bullet">
/// <item>a list that arrived and does not name this client — the verb is refused
/// locally, and nothing is applied or sent;</item>
/// <item>a list that arrived and DOES name it — the verb proceeds, and the host still
/// judges it;</item>
/// <item>NOTHING announced — the local check is skipped entirely and the host's own
/// verdict is the only one, exactly the behaviour before the roster was replicated.</item>
/// </list>
///
/// <para>
/// The wire message's own shape is pinned here too (the round trip and the one value
/// it refuses whole), because the receiver's three states can only be told apart from
/// what the message carries.
/// </para>
/// </summary>
public class AnnouncedCheatPermissionsTests
{
	private const ulong Host = 1001;
	private const ulong Guest = 2001;
	private const ulong Other = 3001;

	[Fact]
	public void WithNothingAnnounced_TheLocalCheckIsSkipped()
	{
		var announced = New(out _);

		Assert.Null(announced.Current);
		Assert.False(announced.Refuses(Guest), "no announcement is not 'nobody is granted' — it is 'no local opinion'");
	}

	[Fact]
	public void AnEmptyAnnouncedList_RefusesEveryone_AndIsNotAnAbsence()
	{
		var announced = New(out _);

		Assert.True(announced.Apply([]));

		Assert.NotNull(announced.Current);
		Assert.Empty(announced.Current!);
		Assert.True(announced.Refuses(Guest));
		Assert.True(announced.Refuses(Host));
	}

	[Fact]
	public void AnAnnouncedList_RefusesOnlyTheClientsItDoesNotName()
	{
		var announced = New(out _);

		announced.Apply([Guest, Other]);

		Assert.False(announced.Refuses(Guest));
		Assert.False(announced.Refuses(Other));
		Assert.True(announced.Refuses(9999));
	}

	[Fact]
	public void ARepeatAnnouncement_IsAbsorbedSilently_AndAnOrderOnlyEditIsNotAChange()
	{
		var announced = New(out _);
		Assert.True(announced.Apply([Other, Guest]));

		Assert.False(announced.Apply([Guest, Other]), "the list is stored sorted, so a grant order that moved is the same list");
	}

	[Fact]
	public void ASessionEnd_DropsTheList_SoTheNextHostStartsFromNoOpinion()
	{
		var announced = New(out var session);
		announced.Apply([Guest]);

		session.FireSessionEnded();

		Assert.Null(announced.Current);
		Assert.False(announced.Refuses(Guest));
	}

	[Fact]
	public void TheMessage_RoundTripsTheList()
	{
		var msg = CheatPermissionsMsg.From([Guest, Other]);

		Assert.Equal([Guest, Other], msg.ToList());
		Assert.Null(msg.Malformed());
	}

	[Fact]
	public void TheMessage_RefusesAPlayerZeroEntryWhole()
	{
		var msg = new CheatPermissionsMsg { SteamIds = [Guest, 0] };

		Assert.Contains("player 0", msg.Malformed()!);
	}

	private static AnnouncedCheatPermissions New(out FakeSessionControl session)
	{
		session = new FakeSessionControl { Role = SessionRole.Guest, LocalSteamId = Guest, HostSteamId = Host, SessionActive = true };
		return new AnnouncedCheatPermissions(session, NullLogger<AnnouncedCheatPermissions>.Instance);
	}
}
